using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Services;
using Stripe;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Unit tests for <see cref="StripeRefundService"/>. Supabase calls are intercepted
/// via a fake message handler (no real network calls); the Stripe SDK call is
/// intercepted by substituting <see cref="RefundService"/> itself — its
/// <c>CreateAsync</c> is virtual and the class has a public parameterless
/// constructor, so NSubstitute can proxy it directly without touching the
/// network or the ambient <see cref="StripeConfiguration.ApiKey"/>.
/// </summary>
public sealed class StripeRefundServiceTests
{
    private static IConfiguration Config(string? stripeKey = "sk_test_123", string? supabaseUrl = "https://proj.supabase.co", string? supabaseKey = "service-role-key") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = stripeKey,
                ["Supabase:Url"] = supabaseUrl,
                ["Supabase:ServiceRoleKey"] = supabaseKey,
            })
            .Build();

    private static (StripeRefundService Sut, RecordingHttpHandler Http, RefundService StripeRefunds) BuildSut(
        IConfiguration? config = null,
        params (HttpStatusCode Status, string Body)[] responses)
    {
        var http = new RecordingHttpHandler(responses);
        var httpClient = new HttpClient(http);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(StripeRefundService.HttpClientName).Returns(httpClient);

        var stripeRefunds = Substitute.For<RefundService>();

        var sut = new StripeRefundService(
            factory, config ?? Config(), stripeRefunds, NullLogger<StripeRefundService>.Instance);

        return (sut, http, stripeRefunds);
    }

    [Fact]
    public async Task NoCompletedTransaction_ReturnsNotEligible_AndNeverCallsStripe()
    {
        var (sut, http, stripeRefunds) = BuildSut(responses: [(HttpStatusCode.OK, "[]")]);

        var outcome = await sut.RefundFailedGenerationAsync("gen-free-tier", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.NotEligible);
        http.Requests.Should().HaveCount(1);
        http.Requests[0].Url.Should().Contain("status=eq.completed");
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletedTransaction_ClaimsRowAndIssuesFullStripeRefund()
    {
        var (sut, http, stripeRefunds) = BuildSut(
            responses:
            [
                (HttpStatusCode.OK, "[{\"id\":\"tx-1\",\"stripe_payment_intent\":\"pi_123\"}]"),
                (HttpStatusCode.OK, "[{\"id\":\"tx-1\",\"status\":\"refund_pending\"}]"),
            ]);
        stripeRefunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .Returns(new Refund { Id = "re_1" });

        var outcome = await sut.RefundFailedGenerationAsync("gen-42", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.Issued);

        http.Requests.Should().HaveCount(2);
        http.Requests[0].Method.Should().Be(HttpMethod.Get);
        http.Requests[0].Url.Should().Contain("generation_id=eq.gen-42");

        http.Requests[1].Method.Should().Be(HttpMethod.Patch);
        http.Requests[1].Url.Should().Contain("id=eq.tx-1");
        http.Requests[1].Url.Should().Contain("status=eq.completed");
        http.Requests[1].Body.Should().Contain("\"refund_pending\"");

        await stripeRefunds.Received(1).CreateAsync(
            Arg.Is<RefundCreateOptions>(o =>
                o.PaymentIntent == "pi_123" &&
                o.Metadata["generation_id"] == "gen-42" &&
                o.Metadata["reason"] == "compile_guarantee"),
            Arg.Any<RequestOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimLosesRace_ReturnsNotEligible_AndNeverCallsStripe()
    {
        // GET finds a 'completed' row, but the claim PATCH matches zero rows —
        // a concurrent caller (or a status change in between) already moved it
        // out of 'completed'.
        var (sut, http, stripeRefunds) = BuildSut(
            responses:
            [
                (HttpStatusCode.OK, "[{\"id\":\"tx-2\",\"stripe_payment_intent\":\"pi_456\"}]"),
                (HttpStatusCode.OK, "[]"),
            ]);

        var outcome = await sut.RefundFailedGenerationAsync("gen-race", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.NotEligible);
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StripeCallThrows_RevertsClaimBackToCompleted_AndReturnsFailed()
    {
        var (sut, http, stripeRefunds) = BuildSut(
            responses:
            [
                (HttpStatusCode.OK, "[{\"id\":\"tx-3\",\"stripe_payment_intent\":\"pi_789\"}]"),
                (HttpStatusCode.OK, "[{\"id\":\"tx-3\",\"status\":\"refund_pending\"}]"),
                (HttpStatusCode.NoContent, ""), // revert PATCH
            ]);
        stripeRefunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .Returns<Refund>(_ => throw new StripeException("card issuer unreachable"));

        var outcome = await sut.RefundFailedGenerationAsync("gen-stripe-fail", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.Failed);

        http.Requests.Should().HaveCount(3);
        http.Requests[2].Method.Should().Be(HttpMethod.Patch);
        http.Requests[2].Url.Should().Contain("status=eq.refund_pending");
        http.Requests[2].Body.Should().Contain("\"completed\"");
    }

    [Fact]
    public async Task CalledTwiceForSameGeneration_SecondCallIsNoOp()
    {
        // First call: happy path (claims + Stripe succeeds). Second call: the GET
        // this time returns nothing because the row is no longer 'completed' —
        // exactly what happens for a real repeat invocation, since the first
        // call already flipped the status. This is the idempotency guarantee:
        // no second Stripe refund is ever created for the same generation.
        var (sut, http, stripeRefunds) = BuildSut(
            responses:
            [
                (HttpStatusCode.OK, "[{\"id\":\"tx-4\",\"stripe_payment_intent\":\"pi_dup\"}]"),
                (HttpStatusCode.OK, "[{\"id\":\"tx-4\",\"status\":\"refund_pending\"}]"),
                (HttpStatusCode.OK, "[]"),
            ]);
        stripeRefunds.CreateAsync(Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .Returns(new Refund { Id = "re_dup" });

        var first = await sut.RefundFailedGenerationAsync("gen-dup", CancellationToken.None);
        var second = await sut.RefundFailedGenerationAsync("gen-dup", CancellationToken.None);

        first.Should().Be(RefundOutcome.Issued);
        second.Should().Be(RefundOutcome.NotEligible);

        await stripeRefunds.Received(1).CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StripeNotConfigured_ReturnsNotEligible_AndMakesNoHttpCalls()
    {
        var (sut, http, stripeRefunds) = BuildSut(config: Config(stripeKey: null));

        var outcome = await sut.RefundFailedGenerationAsync("gen-no-stripe", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.NotEligible);
        http.Requests.Should().BeEmpty();
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SupabaseNotConfigured_ReturnsNotEligible_AndMakesNoHttpCalls()
    {
        var (sut, http, stripeRefunds) = BuildSut(config: Config(supabaseUrl: null));

        var outcome = await sut.RefundFailedGenerationAsync("gen-no-supabase", CancellationToken.None);

        outcome.Should().Be(RefundOutcome.NotEligible);
        http.Requests.Should().BeEmpty();
        await stripeRefunds.DidNotReceive().CreateAsync(
            Arg.Any<RefundCreateOptions>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>());
    }

    // ── Fake HTTP handler ────────────────────────────────────────────────────

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
