using StackAlchemist.Engine.Middleware;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Endpoints;

/// <summary>The endpoints that start work on a generation: <c>/api/generate</c> and <c>/api/extract-schema</c>.</summary>
public static class GenerationEndpoints
{
    public static IEndpointRouteBuilder MapGenerationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/generate", GenerateAsync)
            .WithName("Generate")
            .WithSummary("Enqueue a code generation job.")
            .RequireRateLimiting(RateLimitPolicies.Generate);

        // ── Schema extraction (Simple Mode) ──────────────────────────────────────
        app.MapPost("/api/extract-schema", ExtractSchemaAsync)
            .WithName("ExtractSchema")
            .WithSummary("Extract a structured schema from a natural-language prompt via LLM.")
            .RequireRateLimiting(RateLimitPolicies.Extract);

        return app;
    }

    /// <summary>
    /// The message for a prompt over <c>Generation:MaxPromptChars</c>, or null when it fits. The
    /// frontend caps prompts at 2,000 chars; a direct API call has no such cap and every unchecked
    /// character is paid LLM context.
    /// </summary>
    private static string? PromptTooLongMessage(IConfiguration config, string? prompt)
    {
        var maxPromptChars = config.GetValue("Generation:MaxPromptChars", 5_000);
        return prompt is { Length: var len } && len > maxPromptChars
            ? $"Prompt exceeds the {maxPromptChars:N0} character limit. Shorten the description and retry."
            : null;
    }

    private static async Task<IResult> GenerateAsync(
        GenerateRequest request,
        IGenerationOrchestrator orchestrator,
        IDeliveryService delivery,
        IPaidGenerationGate paidGate,
        IConfiguration config,
        IHostApplicationLifetime lifetime,
        ILogger<Program> logger)
    {
        // Fail the row (which also refunds the free-quota slot — the trigger excludes failed rows)
        // and reject.
        if (PromptTooLongMessage(config, request.Prompt) is { } tooLong)
        {
            await delivery.UpdateStatusAsync(
                request.GenerationId, "failed", lifetime.ApplicationStopping, tooLong, ErrorCategorizer.Schema);
            return Results.BadRequest(new { error = tooLong });
        }

        // The tier in the request came from the browser by way of the web app. A paid tier is built
        // only for a completed payment: without this, calling the web's submit action with tier 3, or
        // retrying a build that was refunded, got a paid build for nothing.
        switch (await paidGate.CheckAsync(request.GenerationId, request.Tier, lifetime.ApplicationStopping))
        {
            case PaymentCheck.Unpaid:
                logger.UnpaidGenerationRefused(request.GenerationId, request.Tier);
                // Failing the row takes it out of pending, where it would otherwise sit until the
                // reconciler gave up on it a day later.
                await delivery.UpdateStatusAsync(
                    request.GenerationId, "failed", lifetime.ApplicationStopping,
                    PaidGenerationGate.UnpaidMessage, ErrorCategorizer.Internal);
                return Results.Json(
                    new { error = PaidGenerationGate.UnpaidMessage },
                    statusCode: StatusCodes.Status402PaymentRequired);

            case PaymentCheck.Unavailable:
                // The row is left alone: if it is paid, the reconciler re-enqueues it once it goes stale.
                return Results.Json(
                    new { error = "Payment status could not be checked. Please try again shortly." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        // Dispatch codegen to the background on the app-lifetime token — NOT the request's
        // CancellationToken (which binds to HttpContext.RequestAborted). A client or CF-tunnel
        // disconnect must not cancel an in-flight generation: a one-shot whole-app LLM call can
        // outlast the ~100s proxy timeout, and the frontend tracks progress by polling the generation
        // row, not this HTTP response. So we fire-and-forget and return Accepted immediately. Safe
        // because every engine service is registered AddSingleton (no scoped disposal at request
        // end). EnqueueAsync owns its own failure handling (marks the row failed); the catch here
        // only guards against an unexpected throw before that runs.
        _ = Task.Run(async () =>
        {
            try
            {
                await orchestrator.EnqueueAsync(request, lifetime.ApplicationStopping);
            }
            catch (Exception ex)
            {
                logger.GenerationDispatchFailed(ex, request.GenerationId);
            }
        });

        return Results.Accepted(
            $"/api/generate/{request.GenerationId}",
            new GenerateResponse
            {
                JobId = request.GenerationId,
                Status = "generating_code",
                ProjectType = request.ProjectType,
            });
    }

    private static async Task<IResult> ExtractSchemaAsync(
        ExtractSchemaRequest request,
        IPromptBuilderService promptBuilder,
        ILlmClient llmClient,
        ISchemaExtractionService schemaExtractor,
        IDeliveryService delivery,
        IConfiguration config,
        ILogger<Program> logger,
        IHostApplicationLifetime lifetime)
    {
        // Run on the app-lifetime token, NOT HttpContext.RequestAborted. The frontend aborts
        // this request at 45s; binding the LLM call to RequestAborted made that abort cancel the
        // extraction and flip the row to "failed" at ~46s — the schema-extraction error screen
        // the user kept hitting. This is one short LLM turn: let it finish and persist the schema
        // even if the client already walked away.
        var ct = lifetime.ApplicationStopping;

        logger.SchemaExtractRequested(request.GenerationId);

        // Length is the only pre-LLM scale check, by design: the post-LLM size validation
        // (20 entities / 30 rels / 30 fields) is authoritative, and guessing entity counts
        // from prose is unreliable in both directions. The cap bounds the paid LLM context.
        if (PromptTooLongMessage(config, request.Prompt) is { } tooLong)
        {
            // Failing the row matters: it un-sticks pending and refunds the free-quota
            // slot (the quota trigger excludes failed rows).
            await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, tooLong, ErrorCategorizer.Schema);
            return Results.BadRequest(new { error = tooLong });
        }

        // CAS: pending/failed → extracting_schema. A double-submit (network retry, two
        // tabs) loses the claim and must not trigger a second paid LLM call.
        if (!await delivery.TryBeginExtractionAsync(request.GenerationId, ct))
        {
            return Results.Conflict(new
            {
                error = "Schema extraction is already in progress or this generation has completed.",
            });
        }

        try
        {
            var systemPrompt = promptBuilder.BuildSchemaExtractionPrompt(request.Prompt);
            // Schema extraction is a StackAlchemist-internal preprocessing step (prose → schema); it
            // stays on the global Anthropic config (options: null). BYOK/per-user model routing applies
            // to the generation itself, resolved per-generation inside the orchestrator.
            var llmResponse = await llmClient.GenerateAsync(
                systemPrompt,
                request.Prompt,
                options: null,
                ct);

            await delivery.UpdateTokenUsageAsync(
                request.GenerationId,
                llmResponse.InputTokens,
                llmResponse.OutputTokens,
                llmResponse.Model,
                ct);

            // A max_tokens cutoff means the schema JSON is incomplete — parsing it would
            // produce a misleading "invalid JSON" error; fail with the real cause instead.
            // (After token recording: truncated calls still consumed tokens.)
            LlmResponseGuard.ThrowIfTruncated(llmResponse, "extracting the schema");

            var schema = schemaExtractor.ParseExtractionResponse(llmResponse.Text);

            // Persist the extracted schema to the generation row
            await delivery.UpdateSchemaAsync(request.GenerationId, schema, ct);

            logger.SchemaExtracted(request.GenerationId, schema.Entities.Count);

            return Results.Ok(new ExtractSchemaResponse
            {
                GenerationId = request.GenerationId,
                Schema = schema,
            });
        }
        catch (Exception ex) when (ex is TruncatedLlmResponseException or SchemaExtractionException)
        {
            logger.SchemaExtractFailed(request.GenerationId, ex.Message);
            await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, ex.Message, ErrorCategorizer.Schema);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (SchemaValidationException ex)
        {
            logger.SchemaValidationFailed(request.GenerationId, ex.Message);
            await delivery.UpdateStatusAsync(request.GenerationId, "failed", ct, ex.Message, ErrorCategorizer.Schema);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            // Any unforeseen failure must still flip the row out of "extracting_schema",
            // otherwise it sits there forever and the UI never resolves. CancellationToken.None:
            // a cancelled/aborted request must still record its terminal state.
            logger.SchemaExtractFailed(request.GenerationId, ex.Message);
            await delivery.UpdateStatusAsync(
                request.GenerationId, "failed", CancellationToken.None, ex.Message, ErrorCategorizer.Categorize(ex));
            return Results.Problem("Schema extraction failed unexpectedly.");
        }
    }
}
