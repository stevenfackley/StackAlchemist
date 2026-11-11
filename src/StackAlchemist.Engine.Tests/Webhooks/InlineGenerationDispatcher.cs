using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Webhooks;

/// <summary>
/// Hands the request to the orchestrator on the calling thread, so a test can assert on the
/// orchestrator as soon as the handler returns. The real dispatcher's background behaviour is
/// covered in <c>BackgroundGenerationDispatcherTests</c>.
/// </summary>
internal sealed class InlineGenerationDispatcher(IGenerationOrchestrator orchestrator) : IGenerationDispatcher
{
    public void Dispatch(GenerateRequest request) => _ = orchestrator.EnqueueAsync(request, CancellationToken.None);
}
