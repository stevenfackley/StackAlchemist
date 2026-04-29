using System.Diagnostics.Metrics;

namespace StackAlchemist.Engine.Telemetry;

public static class Meters
{
    public const string GenerationMeterName = "StackAlchemist.Engine.Generation";

    public static readonly Meter Generation = new(GenerationMeterName, "1.0.0");

    public static readonly Counter<long> Started =
        Generation.CreateCounter<long>(
            "generation.started",
            description: "Number of generation jobs accepted by the orchestrator");

    public static readonly Counter<long> Succeeded =
        Generation.CreateCounter<long>(
            "generation.succeeded",
            description: "Number of generation jobs that produced a successful download");

    public static readonly Counter<long> Failed =
        Generation.CreateCounter<long>(
            "generation.failed",
            description: "Number of generation jobs that ended in a failure terminal state");

    public static readonly Counter<long> BuildRetried =
        Generation.CreateCounter<long>(
            "generation.build_retried",
            description: "Number of LLM-repair retry attempts triggered by build failures");

    public static readonly Histogram<double> DurationMs =
        Generation.CreateHistogram<double>(
            "generation.duration_ms",
            unit: "ms",
            description: "End-to-end generation duration from orchestrator enqueue to terminal state");
}
