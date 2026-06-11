using System.Collections.Concurrent;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// A critical generation-row write (success/failed) that exhausted its HTTP retries.
/// Buffered in memory and re-flushed by the periodic reconciler so a transient
/// Supabase outage no longer strands a finished generation in a non-terminal state.
/// </summary>
public sealed record PendingGenerationWrite(
    string GenerationId,
    Dictionary<string, object?> Payload,
    DateTimeOffset EnqueuedAtUtc);

public interface IPendingWriteBuffer
{
    void Enqueue(PendingGenerationWrite write);
    bool TryDequeue(out PendingGenerationWrite? write);
    int Count { get; }
}

public sealed class PendingWriteBuffer : IPendingWriteBuffer
{
    // In-memory only by design: this is a rescue path for transient outages, not a
    // durable queue — the periodic reconciler's stale sweep is the backstop when the
    // process dies with writes still buffered.
    private const int Capacity = 256;

    private readonly ConcurrentQueue<PendingGenerationWrite> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(PendingGenerationWrite write)
    {
        _queue.Enqueue(write);
        while (_queue.Count > Capacity && _queue.TryDequeue(out _))
        {
            // Drop-oldest on overflow; the stale sweep eventually fails dropped rows.
        }
    }

    public bool TryDequeue(out PendingGenerationWrite? write)
    {
        var ok = _queue.TryDequeue(out var item);
        write = item;
        return ok;
    }
}
