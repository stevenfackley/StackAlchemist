using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// The <see cref="IDeliveryService"/> registered outside Production when DATABASE_URL is unset
/// (local runs, unit hosts). There is no store, so every write is dropped and every read finds
/// nothing: null for a single row, empty for a list, false for a compare-and-set. The one
/// exception is <see cref="TryBeginExtractionAsync"/>, which returns true so local schema
/// extraction keeps working without a row to guard. Logs once, when the host constructs it.
/// </summary>
public sealed partial class NoOpDeliveryService : IDeliveryService
{
    public NoOpDeliveryService(ILogger<NoOpDeliveryService> logger) => LogNoStore(logger);

    public Task UpdateStatusAsync(
        string generationId,
        GenerationState state,
        string? downloadUrl = null,
        string? errorMessage = null,
        string? errorCategory = null,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task UpdateStatusAsync(
        string generationId,
        string status,
        CancellationToken ct,
        string? errorMessage = null,
        string? errorCategory = null) => Task.CompletedTask;

    public Task<GenerationSnapshot?> GetGenerationSnapshotAsync(string generationId, CancellationToken ct) =>
        Task.FromResult<GenerationSnapshot?>(null);

    public Task CompletePreviewAsync(
        string generationId,
        IReadOnlyDictionary<string, string> files,
        CancellationToken ct) => Task.CompletedTask;

    public Task UpdateSchemaAsync(string generationId, GenerationSchema schema, CancellationToken ct) =>
        Task.CompletedTask;

    public Task UpdateTokenUsageAsync(
        string generationId,
        int inputTokens,
        int outputTokens,
        string model,
        CancellationToken ct) => Task.CompletedTask;

    public Task AppendBuildLogAsync(string generationId, string logChunk, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<string?> GetGenerationOwnerEmailAsync(string generationId, CancellationToken ct) =>
        Task.FromResult<string?>(null);

    public Task<ProfileCredential?> GetGenerationCredentialAsync(string generationId, CancellationToken ct) =>
        Task.FromResult<ProfileCredential?>(null);

    public Task<IReadOnlyList<GenerationSnapshot>> GetStaleNonTerminalAsync(TimeSpan olderThan, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GenerationSnapshot>>([]);

    public Task<bool> TryClaimForRequeueAsync(GenerationSnapshot row, TimeSpan olderThan, CancellationToken ct) =>
        Task.FromResult(false);

    public Task<bool> TryFailStaleRowAsync(
        GenerationSnapshot row,
        TimeSpan olderThan,
        string errorMessage,
        string errorCategory,
        CancellationToken ct) => Task.FromResult(false);

    public Task<bool> TryBeginExtractionAsync(string generationId, CancellationToken ct) =>
        Task.FromResult(true);

    public Task<bool> TryPatchOnceAsync(string generationId, Dictionary<string, object?> payload, CancellationToken ct) =>
        Task.FromResult(false);

    [LoggerMessage(EventId = 520, Level = LogLevel.Warning, Message = "DATABASE_URL is not set: generation state is not persisted (NoOpDeliveryService) and the billing store is disabled")]
    private static partial void LogNoStore(ILogger logger);
}
