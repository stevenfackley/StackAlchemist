namespace StackAlchemist.Engine.Models;

/// <summary>
/// The values <c>generations.status</c> holds: the <c>generations_status_check</c> CHECK in the
/// web app's <c>src/db/schema.ts</c> lists the same set (a web test compares the two). One engine
/// state has two names on purpose (docs/DECISIONS.md, the status CHECK widening):
/// <see cref="GeneratingCode"/> is written when a generation starts, <see cref="Generating"/> by
/// the build-repair loop, and both are <see cref="GenerationState.Generating"/>.
/// </summary>
public static class GenerationStatus
{
    public const string Pending = "pending";
    public const string ExtractingSchema = "extracting_schema";
    public const string GeneratingCode = "generating_code";
    public const string Generating = "generating";
    public const string Building = "building";
    public const string Packing = "packing";
    public const string Uploading = "uploading";
    public const string Success = "success";
    public const string Failed = "failed";

    /// <summary>Every status, in the CHECK's order.</summary>
    public static readonly IReadOnlyList<string> All =
        [Pending, ExtractingSchema, GeneratingCode, Generating, Building, Packing, Uploading, Success, Failed];

    /// <summary>
    /// Every status except the two terminal ones, in the CHECK's order: the rows the reconciler
    /// sweeps. A restart can orphan a job mid-packing or mid-upload as easily as mid-build.
    /// </summary>
    public static readonly IReadOnlyList<string> NonTerminal = [.. All.Where(status => !IsTerminal(status))];

    public static bool IsTerminal(string status) => status is Success or Failed;

    /// <summary>
    /// The status a <see cref="GenerationState"/> is written as: its name in lower case, so
    /// <see cref="GenerationState.Generating"/> is <see cref="Generating"/>. A test checks every
    /// state lands in <see cref="All"/>, so renaming a state cannot write a status the CHECK refuses.
    /// </summary>
    public static string ToWire(GenerationState state) => state.ToString().ToLowerInvariant();
}
