using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using StackAlchemist.Engine.Telemetry;

namespace StackAlchemist.Engine.Tests.Fixtures;

/// <summary>
/// Records what the generation meters (<see cref="Meters"/>) are given while it is alive. The
/// meters are static and other tests record on them in parallel, so a measurement is kept only
/// when its <c>mode</c> tag is this capture's <see cref="Mode"/>: give the job under test that mode.
/// </summary>
public sealed class GenerationMeterCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<(string Instrument, double Value, string Tags)> _measurements = new();

    public string Mode { get; } = "meter-capture-" + Guid.NewGuid().ToString("N");

    public GenerationMeterCapture()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == Meters.GenerationMeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.Start();
    }

    /// <summary>
    /// Each measurement <paramref name="instrument"/> was given, as its tags without <c>mode</c>,
    /// sorted by key and written <c>key=value</c> joined by <c>;</c>.
    /// </summary>
    public IReadOnlyList<string> TagSets(string instrument) =>
        [.. _measurements.Where(m => m.Instrument == instrument).Select(m => m.Tags)];

    /// <summary>The sum of the values <paramref name="instrument"/> was given.</summary>
    public double Total(string instrument) =>
        _measurements.Where(m => m.Instrument == instrument).Sum(m => m.Value);

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        object? mode = null;
        var rest = new List<string>();
        foreach (var (key, tagValue) in tags)
        {
            if (key == "mode")
                mode = tagValue;
            else
                rest.Add($"{key}={tagValue}");
        }

        if (!Equals(mode, Mode))
            return;

        rest.Sort(StringComparer.Ordinal);
        _measurements.Enqueue((instrument.Name, value, string.Join(";", rest)));
    }

    public void Dispose() => _listener.Dispose();
}
