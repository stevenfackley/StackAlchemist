using System.Collections.Concurrent;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Tracks generation ids this Engine instance is actively processing (orchestration
/// or compile-worker job). The periodic reconciler skips registered ids so a slow but
/// alive job — e.g. a multi-minute LLM call that hasn't pinged Supabase recently —
/// is never reaped out from under the running process.
/// </summary>
public interface IInFlightGenerationRegistry
{
    void Add(string generationId);
    void Remove(string generationId);
    bool Contains(string generationId);
}

public sealed class InFlightGenerationRegistry : IInFlightGenerationRegistry
{
    private readonly ConcurrentDictionary<string, byte> _inFlight = new();

    public void Add(string generationId) => _inFlight.TryAdd(generationId, 0);

    public void Remove(string generationId) => _inFlight.TryRemove(generationId, out _);

    public bool Contains(string generationId) => _inFlight.ContainsKey(generationId);
}
