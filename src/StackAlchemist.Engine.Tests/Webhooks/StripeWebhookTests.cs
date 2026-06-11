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
    public async Task DuplicateCheckoutEvent_IsSkipped()
    {
        // The RPC reports is_new=false → handler short-circuits without enqueueing.
        var (sut, http, orchestrator, _) = BuildSut(
            (HttpStatusCode.OK, "[{\"is_new\":false}]"));

        var stripeEvent = new Event
        {
            Id = "evt_dup_1",
            Type = "checkout.session.completed",
            Data = new EventData { Object = new Session { Id = "cs_dup" } },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("duplicate");
        result.Retry.Should().BeFalse();
        http.Requests.Should().HaveCount(1);
        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateNonCheckoutEvent_IsSkipped()
    {
        // 409 Conflict on stripe_events insert → handler short-circuits.
        var (sut, http, _, _) = BuildSut(
            (HttpStatusCode.Conflict, ""));

        var stripeEvent = new Event
        {
            Id = "evt_dup_2",
            Type = "charge.refunded",
            Data = new EventData
            {
                Object = new Charge { Id = "ch_dup", PaymentIntentId = "pi_dup" },
            },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Reason.Should().Be("duplicate");
        http.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task NonCheckoutEvent_IdempotencyUnavailable_RequestsRetry()
    {
        // stripe_events insert fails (5xx) → no fail-open: ask Stripe to redeliver
        // instead of processing unlogged (concurrent duplicates double-processed before).
        var (sut, http, _, _) = BuildSut(
            (HttpStatusCode.ServiceUnavailable, ""));

        var stripeEvent = new Event
        {
            Id = "evt_unavail_1",
            Type = "charge.refunded",
            Data = new EventData
            {
                Object = new Charge { Id = "ch_x", PaymentIntentId = "pi_x" },
            },
        };

        var result = await sut.HandleAsync(stripeEvent, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Retry.Should().BeTrue();
        http.Requests.Should().HaveCount(1, "the transaction PATCH must not run unlogged");
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

    private static Event CheckoutCompletedEvent(string eventId = "evt_completed_1") => new()
    {
        Id = eventId,
        Type = "checkout.session.completed",
        Data = new EventData
        {
            Object = new Session
            {
                Id = "cs_77",
                PaymentIntentId = "pi_77",
                AmountTotal = 59_900,
                Metadata = new Dictionary<string, string>
                {
                    ["generationId"] = "gen-77",
                    ["tier"] = "2",
                },
            },
        },
    };

    [Fact]
    public async Task CheckoutCompleted_RunsAtomicRpcThenEnqueues()
    {
        // The single RPC call replaces the old insert→load→tier-PATCH→upsert sequence:
        // event recording, tier update, and transaction upsert are one Postgres
        // transaction, so a partial failure can no longer strand a paid generation.
        var (sut, http, orchestrator, _) = BuildSut(
            (HttpStatusCode.OK,
             "[{\"is_new\":true,\"mode\":\"advanced\",\"prompt\":null," +
             "\"project_type\":\"DotNetNextJs\",\"schema_json\":null,\"personalization_json\":null}]"));

        var result = await sut.HandleAsync(CheckoutCompletedEvent(), CancellationToken.None);

        result.Processed.Should().BeTrue();
        result.Retry.Should().BeFalse();

        http.Requests.Should().HaveCount(1);
        var rpc = http.Requests[0];
        rpc.Method.Should().Be(HttpMethod.Post);
        rpc.Url.Should().Contain("/rest/v1/rpc/process_checkout_completed");
        rpc.Body.Should().Contain("\"p_event_id\":\"evt_completed_1\"");
        rpc.Body.Should().Contain("\"p_session_id\":\"cs_77\"");
        rpc.Body.Should().Contain("\"p_generation_id\":\"gen-77\"");
        rpc.Body.Should().Contain("\"p_tier\":2");
        rpc.Body.Should().Contain("\"p_amount\":59900");

        await orchestrator.Received(1).EnqueueAsync(
            Arg.Is<GenerateRequest>(r =>
                r.GenerationId == "gen-77" && r.Tier == 2 && r.Mode == "advanced"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckoutCompleted_RpcFailure_RequestsRetryWithoutEnqueue()
    {
        var (sut, http, orchestrator, _) = BuildSut(
            (HttpStatusCode.InternalServerError, ""));

        var result = await sut.HandleAsync(CheckoutCompletedEvent(), CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Retry.Should().BeTrue("the RPC rolled back, so Stripe's redelivery re-processes cleanly");
        http.Requests.Should().HaveCount(1);
        await orchestrator.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckoutCompleted_EnqueueThrows_CompensatesEventAndRetries()
    {
        var (sut, http, orchestrator, _) = BuildSut(
            (HttpStatusCode.OK,
             "[{\"is_new\":true,\"mode\":\"advanced\",\"prompt\":null," +
             "\"project_type\":\"DotNetNextJs\",\"schema_json\":null,\"personalization_json\":null}]"),
            (HttpStatusCode.NoContent, ""));

        orchestrator.EnqueueAsync(Arg.Any<GenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<GenerateResponse>>(_ => throw new InvalidOperationException("queue full"));

        var result = await sut.HandleAsync(CheckoutCompletedEvent("evt_comp_2"), CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.Retry.Should().BeTrue();

        // Compensation: the event row is deleted so the RPC sees a fresh insert on
        // Stripe's redelivery instead of short-circuiting as a duplicate.
        http.Requests.Should().HaveCount(2);
        http.Requests[1].Method.Should().Be(HttpMethod.Delete);
        http.Requests[1].Url.Should().Contain("stripe_events?id=eq.evt_comp_2");
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
