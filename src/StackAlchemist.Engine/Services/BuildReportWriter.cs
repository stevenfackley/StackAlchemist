using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Produces the <c>build-report.json</c> that <c>docs/advanced-docs/compile-guarantee.md</c>
/// promises ships in every customer archive, and the compact summary block appended to the
/// streamed <c>generations.build_log</c> so the delivery UI can badge each half honestly
/// instead of asserting "Compile Verified" from a hardcoded string.
///
/// Written at the archive root, right before pack+upload, so it describes the code that is
/// actually in the zip — including a failed final attempt, which reaches the customer
/// alongside the automatic refund.
/// </summary>
public static class BuildReportWriter
{
    /// <summary>Archive-root filename. Part of the published contract — see the doc.</summary>
    public const string FileName = "build-report.json";

    /// <summary>Opening fence of the build_log summary block the web UI parses.</summary>
    public const string SummaryBeginMarker = "=== COMPILE GUARANTEE REPORT ===";

    /// <summary>Closing fence of the build_log summary block.</summary>
    public const string SummaryEndMarker = "=== END COMPILE GUARANTEE REPORT ===";

    /// <summary>
    /// Per-attempt output cap. Three attempts of an npm install transcript can run to
    /// megabytes; the tail carries the errors, so overflow is trimmed from the FRONT.
    /// </summary>
    private const int MaxOutputCharsPerAttempt = 200_000;

    private static readonly JsonSerializerOptions ReportJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static readonly JsonSerializerOptions SummaryJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static BuildReport Create(GenerationContext job, int maxAttempts, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(job);

        var attempts = job.BuildAttempts;
        var finalAttempt = attempts.Count > 0 ? attempts[^1] : null;

        return new BuildReport
        {
            GenerationId = job.GenerationId,
            ProjectType = job.ProjectType.ToString(),
            Tier = job.Tier,
            GeneratedAt = generatedAt,
            Status = finalAttempt?.Passed == true ? "verified" : "failed",
            AttemptsUsed = attempts.Count,
            MaxAttempts = maxAttempts,
            // Per the job's OWN stack. Hardcoding .NET + Next.js shipped a FastAPI + React
            // customer a report about two halves of a stack they never chose.
            Halves = [.. BuildHalves.For(job.ProjectType).Select(half => SummariseHalf(finalAttempt, half))],
            Attempts = [.. attempts.Select(ToAttemptReport)],
        };
    }

    /// <summary>
    /// Serializes <paramref name="report"/> into <paramref name="outputDirectory"/>. Returns
    /// the path written.
    /// </summary>
    public static async Task<string> WriteAsync(
        string outputDirectory,
        BuildReport report,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, FileName);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, ReportJson), ct);
        return path;
    }

    /// <summary>
    /// The fenced block appended to <c>build_log</c>. Deliberately excludes the per-attempt
    /// transcripts already streamed above it — this is the verdict, not a second copy of the
    /// evidence.
    /// </summary>
    public static string ToLogSummary(BuildReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var summary = new BuildReportSummary
        {
            SchemaVersion = report.SchemaVersion,
            Status = report.Status,
            AttemptsUsed = report.AttemptsUsed,
            MaxAttempts = report.MaxAttempts,
            Halves = [.. report.Halves.Select(half => new BuildHalfSummaryLine
            {
                Half = half.Half,
                Label = half.Label,
                Status = half.Status,
            })],
        };

        var builder = new StringBuilder();
        builder.AppendLine(SummaryBeginMarker);
        builder.AppendLine(JsonSerializer.Serialize(summary, SummaryJson));
        builder.Append(SummaryEndMarker);
        return builder.ToString();
    }

    /// <summary>
    /// Final-attempt verdict for one half.
    ///
    /// The four states are distinct on purpose. <c>not_run</c> (the half was never reached
    /// because the other half failed first) is not the same claim as <c>skipped</c> (the
    /// step was reached and deliberately not needed), and neither is <c>passed</c>. Only
    /// <c>passed</c> is evidence of compilation, and only <c>passed</c> may be badged.
    ///
    /// Superseded steps are excluded from the verdict entirely: a step whose work a later
    /// step redid decided nothing, so counting it condemned halves that compiled — the
    /// <c>npm ci</c> → <c>npm install</c> fallback is the documented common path, and it was
    /// stamping <c>failed</c> on successful frontends. The step itself is still in
    /// <c>attempts[].steps[]</c>, flagged <c>superseded</c>.
    /// </summary>
    private static BuildHalfSummary SummariseHalf(BuildAttemptRecord? attempt, BuildHalf half)
    {
        var steps = attempt?.Steps
            .Where(step => step.Half == half && !step.Superseded)
            .ToList() ?? [];

        var status = steps.Count switch
        {
            0 => "not_run",
            _ when steps.Exists(step => !step.Skipped && step.ExitCode != 0) => "failed",
            _ when steps.TrueForAll(step => step.Skipped) => "skipped",
            _ => "passed",
        };

        return new BuildHalfSummary
        {
            Half = BuildHalves.WireName(half),
            Label = BuildHalves.Label(half),
            Status = status,
            Commands = [.. steps.Where(step => !step.Skipped).Select(step => step.Command)],
        };
    }

    private static BuildAttemptReport ToAttemptReport(BuildAttemptRecord record) => new()
    {
        Attempt = record.Attempt,
        StartedAt = record.StartedAt,
        CompletedAt = record.CompletedAt,
        Status = record.Passed ? "passed" : "failed",
        Steps = [.. record.Steps.Select(ToStepReport)],
        Output = TrimToTail(record.Output),
        Errors = record.Errors,
        CorrectedFiles = record.CorrectedFiles,
    };

    private static BuildStepReport ToStepReport(BuildStepResult step) => new()
    {
        Half = BuildHalves.WireName(step.Half),
        Command = step.Command,
        ExitCode = step.ExitCode,
        DurationMs = step.DurationMs,
        ErrorCount = step.ErrorCount,
        WarningCount = step.WarningCount,
        // `superseded` outranks the exit code: the step ran and failed, but a retry redid its
        // work, so reporting it as a plain failure misdescribes an archive that compiled.
        Status = step.Skipped ? "skipped"
            : step.Superseded ? "superseded"
            : step.ExitCode == 0 ? "passed"
            : "failed",
    };

    private static string TrimToTail(string output)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= MaxOutputCharsPerAttempt)
            return output ?? string.Empty;

        var dropped = output.Length - MaxOutputCharsPerAttempt;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[… {dropped} earlier characters truncated …]\n{output[^MaxOutputCharsPerAttempt..]}");
    }
}

/// <summary>Compact projection of <see cref="BuildReport"/> embedded in <c>build_log</c>.</summary>
internal sealed record BuildReportSummary
{
    public required int SchemaVersion { get; init; }
    public required string Status { get; init; }
    public required int AttemptsUsed { get; init; }
    public required int MaxAttempts { get; init; }
    public required IReadOnlyList<BuildHalfSummaryLine> Halves { get; init; }
}

internal sealed record BuildHalfSummaryLine
{
    public required string Half { get; init; }
    public required string Label { get; init; }
    public required string Status { get; init; }
}
