using FluentAssertions;
using Microsoft.Extensions.Logging;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// <see cref="NoOpDeliveryService"/>: the delivery service of a host without a store. It logs once
/// when constructed, drops every write, finds nothing on every read, loses every compare-and-set,
/// and lets schema extraction begin.
/// </summary>
public sealed class NoOpDeliveryServiceTests
{
    private readonly EventRecorder _log = new();

    private static GenerationSnapshot Row() =>
        new("gen-1", "generating", 1, "simple", "prompt", ProjectType.DotNetNextJs, null, null, 0, DateTimeOffset.UtcNow);

    [Fact]
    public void Construction_logs_one_warning()
    {
        _ = new NoOpDeliveryService(_log);

        _log.Events.Should().ContainSingle()
            .Which.Should().Be((LogLevel.Warning, 520));
    }

    [Fact]
    public async Task Writes_complete_without_logging()
    {
        var sut = new NoOpDeliveryService(_log);

        await sut.UpdateStatusAsync("gen-1", GenerationState.Success, downloadUrl: "https://r2.example/x.zip");
        await sut.UpdateStatusAsync("gen-1", "extracting_schema", CancellationToken.None);
        await sut.CompletePreviewAsync("gen-1", new Dictionary<string, string> { ["a.txt"] = "a" }, CancellationToken.None);
        await sut.UpdateSchemaAsync("gen-1", new GenerationSchema(), CancellationToken.None);
        await sut.UpdateTokenUsageAsync("gen-1", 10, 20, "claude-sonnet-5-5", CancellationToken.None);
        await sut.AppendBuildLogAsync("gen-1", "log", CancellationToken.None);

        _log.Events.Should().ContainSingle("only the constructor logs");
    }

    [Fact]
    public async Task Reads_find_nothing()
    {
        var sut = new NoOpDeliveryService(_log);

        (await sut.GetGenerationSnapshotAsync("gen-1", CancellationToken.None)).Should().BeNull();
        (await sut.GetGenerationOwnerEmailAsync("gen-1", CancellationToken.None)).Should().BeNull();
        (await sut.GetGenerationCredentialAsync("gen-1", CancellationToken.None)).Should().BeNull();
        (await sut.GetStaleNonTerminalAsync(TimeSpan.FromMinutes(15), CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Compare_and_set_writes_lose_and_extraction_may_begin()
    {
        var sut = new NoOpDeliveryService(_log);

        (await sut.TryClaimForRequeueAsync(Row(), TimeSpan.FromMinutes(15), CancellationToken.None)).Should().BeFalse();
        (await sut.TryFailStaleRowAsync(Row(), TimeSpan.FromMinutes(15), "stale", "timeout", CancellationToken.None)).Should().BeFalse();
        (await sut.TryPatchOnceAsync("gen-1", new Dictionary<string, object?> { ["status"] = "success" }, CancellationToken.None)).Should().BeFalse();
        (await sut.TryBeginExtractionAsync("gen-1", CancellationToken.None)).Should().BeTrue("local schema extraction has no row to guard");
    }

    /// <summary>Records the level and EventId of every log call.</summary>
    private sealed class EventRecorder : ILogger<NoOpDeliveryService>
    {
        public List<(LogLevel Level, int EventId)> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Events.Add((logLevel, eventId.Id));
    }
}
