namespace StackAlchemist.Engine.Models;

/// <summary>
/// The <c>build-report.json</c> the customer archive ships at its root.
///
/// <c>docs/advanced-docs/compile-guarantee.md</c> has promised this file since the Compile
/// Guarantee was first written up, and nothing in the Engine produced it — the build output
/// existed only in the <c>generations.build_log</c> column, which the customer cannot see
/// after delivery. Every property here is part of that published contract: renaming one is a
/// breaking change to a document customers are sold on, so change the doc in the same commit.
/// </summary>
public sealed record BuildReport
{
    /// <summary>
    /// Bumped only on a breaking shape change, so a customer (or a support script) reading an
    /// archive from any vintage can tell what it is looking at.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    public required string GenerationId { get; init; }

    public required string ProjectType { get; init; }

    public required int Tier { get; init; }

    /// <summary>UTC, ISO-8601. When the report was written — i.e. delivery time.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// <c>verified</c> when the final attempt compiled, <c>failed</c> otherwise.
    ///
    /// In practice every DELIVERED report reads <c>verified</c>: the report is written on the
    /// pack-and-upload path, and CompileWorkerService's retries-exhausted branch refunds and
    /// returns without packing anything — so a failed generation produces no archive and
    /// therefore no report. <c>failed</c> is kept as the honest value for a report built off
    /// that path (tests, and any future failure-path delivery) rather than hardcoding a
    /// verdict, but nothing may describe it to a customer as a file they will receive.
    /// compile-guarantee.md says so explicitly; the record of a failed build is the streamed
    /// build log, which the refund path does write.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Number of build attempts that ran, including the successful one.</summary>
    public required int AttemptsUsed { get; init; }

    /// <summary>The retry ceiling the Compile Guarantee promises (3 corrections).</summary>
    public required int MaxAttempts { get; init; }

    /// <summary>
    /// Per-half verdict from the FINAL attempt — the state of the code actually in the
    /// archive. This is what the delivery UI badges; the per-attempt history below is the
    /// audit trail behind it.
    /// </summary>
    public required IReadOnlyList<BuildHalfSummary> Halves { get; init; }

    /// <summary>Every attempt, oldest first.</summary>
    public required IReadOnlyList<BuildAttemptReport> Attempts { get; init; }
}

/// <summary>Final-attempt verdict for one half of the deliverable.</summary>
public sealed record BuildHalfSummary
{
    /// <summary>
    /// The half's wire name, per the customer's stack: <c>dotnet</c>/<c>nextjs</c>, or
    /// <c>python</c>/<c>react</c>. See <see cref="BuildHalves"/>.
    /// </summary>
    public required string Half { get; init; }

    /// <summary>Human label for the UI: ".NET" / "Next.js" / "FastAPI" / "React".</summary>
    public required string Label { get; init; }

    /// <summary><c>passed</c>, <c>failed</c>, or <c>skipped</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The commands that produced <see cref="Status"/>, in execution order.</summary>
    public required IReadOnlyList<string> Commands { get; init; }
}

/// <summary>One pass through the compiler, plus whatever the repair loop changed afterwards.</summary>
public sealed record BuildAttemptReport
{
    /// <summary>1-based, matching the "[Attempt N]" lines in the streamed build log.</summary>
    public required int Attempt { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary><c>passed</c> or <c>failed</c>.</summary>
    public required string Status { get; init; }

    public required IReadOnlyList<BuildStepReport> Steps { get; init; }

    /// <summary>
    /// Full compiler output for this attempt — the whole transcript, both halves, verbatim.
    /// Truncated from the FRONT (oldest lines dropped) if it would blow up the archive; the
    /// tail is where the errors are.
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// The parsed error lines the repair prompt was built from. Empty on a passing attempt.
    /// </summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Files the LLM rewrote after this attempt failed — "the corrections applied", per the
    /// doc. Empty on the final attempt (nothing follows it) and on a passing one.
    /// </summary>
    public required IReadOnlyList<string> CorrectedFiles { get; init; }
}

/// <summary>
/// In-flight accumulator for one attempt, held on <see cref="GenerationContext"/> while the
/// compile worker runs. Mutable because <see cref="CorrectedFiles"/> is only known after the
/// attempt fails and the repair loop has written the LLM's fixes to disk. Projected into the
/// immutable <see cref="BuildAttemptReport"/> at delivery time.
/// </summary>
public sealed class BuildAttemptRecord
{
    public required int Attempt { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; set; }

    public bool Passed { get; set; }

    public IReadOnlyList<BuildStepResult> Steps { get; set; } = [];

    public string Output { get; set; } = string.Empty;

    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>Relative paths the repair loop overwrote after this attempt failed.</summary>
    public List<string> CorrectedFiles { get; } = [];
}

/// <summary>Serialized form of <see cref="BuildStepResult"/>.</summary>
public sealed record BuildStepReport
{
    /// <summary>The half's wire name — see <see cref="BuildHalfSummary.Half"/>.</summary>
    public required string Half { get; init; }

    public required string Command { get; init; }

    public required int ExitCode { get; init; }

    public required long DurationMs { get; init; }

    public required int ErrorCount { get; init; }

    public required int WarningCount { get; init; }

    /// <summary>
    /// <c>passed</c>, <c>failed</c>, <c>skipped</c>, or <c>superseded</c> — the last meaning
    /// the command failed but a retry redid its work, so it decided nothing.
    /// </summary>
    public required string Status { get; init; }
}
