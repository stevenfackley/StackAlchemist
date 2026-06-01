using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;
using Stripe;
using Stripe.Checkout;

namespace StackAlchemist.Engine.Tests.Webhooks;

/// <summary>
/// Unit tests for <see cref="StripeWebhookHandler"/>. HTTP calls to Supabase are
/// intercepted via a fake message handler — no real network calls.
/// </summary>
public sealed class StripeWebhookTests
{
    private static IConfiguration Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"]            = "https://proj.supabase.co",
                ["Supabase:ServiceRoleKey"] = "service-role-key",
            })
            .Build();

    private static (StripeWebhookHandler Sut, RecordingHttpHandler Http, IGenerationOrchestrator Orchestrator, IEmailService Email)
        BuildSut(params (HttpStatusCode Status, string Body)[] responses)
    {
        var http = new RecordingHttpHandler(responses);
        var httpClient = new HttpClient(http);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(StripeWebhookHandler.HttpClientName).Returns(httpClient);

        var orchestrator = Substitute.For<IGenerationOrchestrator>();
        var email = Substitute.For<IEmailService>();
        var sut = new StripeWebhookHandler(
            orchestrator, Config(), factory, email, NullLogger<StripeWebhookHandler>.Instance);
        return (sut, http, orchestrator, email);
    }

    [Fact]
    public async Task ChargeRefunded_MarksTransactionRefundedAndCancelsGeneration()
    {
        var (sut, http, _, _) = BuildSut(
            (HttpStatusCode.Created, ""),
            (HttpStatusCode.OK, "[{\"generation_id\":\"gen-42\"}]"),
            (HttpStatusCode.NoContent, ""));

        var stripeEvent = new Event
        {
            Id = "evt_refund_1",
            Type = "charge.refunded",
            Data = new EventData
            {
                Object = new Charge { Id = "ch_1", PaymentIntentId = "pi_42", Refunded = true },
            },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeTrue();
        http.Requests.Should().HaveCount(3);
        http.Requests[1].Method.Should().Be(HttpMethod.Patch);
        http.Requests[1].Url.Should().Contain("transactions");
        http.Requests[1].Url.Should().Contain("stripe_payment_intent=eq.pi_42");
        http.Requests[1].Body.Should().Contain("\"refunded\"");
        http.Requests[2].Url.Should().Contain("generations?id=eq.gen-42");
    }

    [Fact]
    public async Task CheckoutAsyncPaymentFailed_MarksTransactionFailed()
    {
        var (sut, http, orchestrator, _) = BuildSut(
            (HttpStatusCode.Created, ""),
            (HttpStatusCode.NoContent, ""));

        var stripeEvent = new Event
        {
            Id = "evt_failed_1",
            Type = "checkout.session.async_payment_failed",
            Data = new EventData { Object = new Session { Id = "cs_1" } },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeTrue();
        http.Requests.Should().HaveCount(2);
        http.Requests[1].Method.Should().Be(HttpMethod.Patch);
        http.Requests[1].Url.Should().Contain("stripe_session_id=eq.cs_1");
        http.Requests[1].Body.Should().Contain("\"failed\"");
        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisputeCreated_MarksTransactionDisputed()
    {
        var (sut, http, _, _) = BuildSut(
            (HttpStatusCode.Created, ""),
            (HttpStatusCode.NoContent, ""));

        var stripeEvent = new Event
        {
            Id = "evt_dispute_1",
            Type = "charge.dispute.created",
            Data = new EventData { Object = new Dispute { ChargeId = "ch_99", Reason = "fraudulent" } },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeTrue();
        http.Requests[1].Url.Should().Contain("stripe_charge_id=eq.ch_99");
        http.Requests[1].Body.Should().Contain("\"disputed\"");
    }

    [Fact]
    public async Task DuplicateEvent_IsSkipped()
    {
        // 409 Conflict on stripe_events insert → handler short-circuits.
        var (sut, http, orchestrator, _) = BuildSut(
            (HttpStatusCode.Conflict, ""));

        var stripeEvent = new Event
        {
            Id = "evt_dup_1",
            Type = "checkout.session.completed",
            Data = new EventData { Object = new Session { Id = "cs_dup" } },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("duplicate");
        http.Requests.Should().HaveCount(1);
        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnhandledEventType_ReturnsNotProcessed()
    {
        var (sut, _, _, _) = BuildSut((HttpStatusCode.Created, ""));

        var stripeEvent = new Event
        {
            Id = "evt_other_1",
            Type = "invoice.payment_succeeded",
            Data = new EventData { Object = new Invoice() },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().StartWith("unhandled:");
    }

    [Fact]
    public async Task CheckoutCompleted_PersistsPaidTierThenEnqueuesUpgrade()
    {
        // Request order: [0] stripe_events insert (idempotency) → [1] load existing
        // payload → [2] tier PATCH (the fix) → [3] transaction upsert.
        var (sut, http, orchestrator, _) = BuildSut(
            (HttpStatusCode.Created, ""),
            (HttpStatusCode.OK, "[]"),
            (HttpStatusCode.NoContent, ""),
            (HttpStatusCode.NoContent, ""));

        var stripeEvent = new Event
        {
            Id = "evt_completed_1",
            Type = "checkout.session.completed",
            Data = new EventData
            {
                Object = new Session
                {
                    Id = "cs_77",
                    Metadata = new Dictionary<string, string>
                    {
                        ["generationId"] = "gen-77",
                        ["tier"] = "2",
                    },
                },
            },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeTrue();

        // The purchased tier is persisted on the existing generation row. Without this a
        // try-before-buy upgrade leaves the row at tier 0, so the result page keeps the
        // free "Upgrade to Download" panel and the paid build burns a free-quota slot.
        var tierPatch = http.Requests.Single(r =>
            r.Method == HttpMethod.Patch && r.Url.Contains("generations?id=eq.gen-77"));
        tierPatch.Body.Should().Contain("\"tier\":2");

        // …and the same generation id is re-enqueued at the paid tier for the full pipeline.
        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r => r.GenerationId == "gen-77" && r.Tier == 2),
            Arg.Any<CancellationToken>());
    }

    public sealed record CapturedRequest(HttpMethod Method, string Url, string Body);

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;
        public List<CapturedRequest> Requests { get; } = [];

        public RecordingHttpHandler(IEnumerable<(HttpStatusCode, string)> responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                body));

            var (status, responseBody) = _responses.Count > 0
                ? _responses.Dequeue()
                : (HttpStatusCode.OK, "");

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
