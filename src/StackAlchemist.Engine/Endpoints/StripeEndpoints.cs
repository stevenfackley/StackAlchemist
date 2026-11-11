using Stripe;
using Stripe.Checkout;
using StackAlchemist.Engine.Middleware;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Endpoints;

/// <summary>Checkout-session creation for the paid tiers, and the Stripe webhook.</summary>
public static class StripeEndpoints
{
    public static IEndpointRouteBuilder MapStripeEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Stripe Checkout Session creation (paid tiers 1–3) ────────────────────────
        // Called by the Next.js server action before redirecting the user to Stripe.
        // Returns the hosted Checkout URL so the frontend can redirect without any
        // client-side Stripe SDK dependency.
        app.MapPost("/api/stripe/create-session", CreateSessionAsync)
            .WithName("CreateStripeSession")
            .WithSummary("Creates a Stripe Checkout Session for a paid tier and returns the hosted URL.")
            .RequireRateLimiting(RateLimitPolicies.StripeSession);

        // ── Stripe webhook ────────────────────────────────────────────────────────────
        // Reads the raw body before routing so Stripe signature verification always
        // receives the unmodified bytes.
        app.MapPost("/api/webhooks/stripe", WebhookAsync)
            .WithName("StripeWebhook")
            .WithSummary("Handles Stripe webhook events: checkout completion, payment failure, refund, and dispute.");

        return app;
    }

    private static async Task<IResult> CreateSessionAsync(
        CreateCheckoutSessionRequest req,
        IConfiguration config,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var secretKey = config["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
            return Results.BadRequest(new { error = "Stripe is not configured on this server." });

        StripeConfiguration.ApiKey = secretKey;

        if (!TierCatalog.TryGetPaidTier(req.Tier, req.ProjectType, out var pricing))
            return Results.BadRequest(new { error = $"Invalid tier: {req.Tier}. Must be 1, 2, or 3." });

        try
        {
            var options = new SessionCreateOptions
            {
                Mode       = "payment",
                SuccessUrl = req.SuccessUrl,
                CancelUrl  = req.CancelUrl,
                LineItems  =
                [
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency   = "usd",
                            UnitAmount = pricing.Cents,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name        = pricing.Name,
                                Description = pricing.Description,
                            },
                        },
                        Quantity = 1,
                    },
                ],
                Metadata = new Dictionary<string, string>
                {
                    ["generationId"] = req.GenerationId,
                    ["tier"]         = req.Tier.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["projectType"]  = req.ProjectType.ToString(),
                    // Stripe caps metadata values at 500 chars; the webhook reads the full prompt from the row.
                    ["prompt"]       = StripeMetadata.Clip(req.Prompt),
                },
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options, cancellationToken: ct);

            logger.StripeSessionCreated(session.Id, req.GenerationId, req.Tier);

            return Results.Ok(new CreateCheckoutSessionResponse
            {
                SessionId = session.Id,
                Url       = session.Url,
                ProjectType = req.ProjectType,
            });
        }
        catch (StripeException ex)
        {
            // Stripe's own message can name price ids and describe the API key, and the web app shows
            // this body to the buyer, so the detail goes to the log only.
            logger.StripeSessionCreationFailed(ex, req.GenerationId);
            return Results.Json(
                new { error = "We could not start checkout. Please try again in a moment." },
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> WebhookAsync(
        HttpRequest req,
        IStripeWebhookHandler webhookHandler,
        IConfiguration config,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var webhookSecret = config["Stripe:WebhookSecret"] ?? string.Empty;
        var signature = req.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

        string json;
        using (var reader = new StreamReader(req.Body))
            json = await reader.ReadToEndAsync(ct);

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signature,
                webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.StripeSignatureVerifyFailed(ex.Message);
            return Results.Unauthorized();
        }

        logger.StripeEventReceived(stripeEvent.Type, stripeEvent.Id);

        var result = await webhookHandler.HandleAsync(stripeEvent, ct);

        // A retryable side-effect failure (idempotency log down, RPC failure, a failed status
        // write) must surface as 5xx — Stripe only redelivers on non-2xx responses.
        if (result.Retry)
            return Results.StatusCode(StatusCodes.Status500InternalServerError);

        if (!result.Processed)
            logger.StripeEventSkipped(stripeEvent.Id, result.Reason);

        return Results.Ok();
    }
}
