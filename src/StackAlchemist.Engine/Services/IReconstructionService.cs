using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface IReconstructionService
{
    /// <summary>
    /// Parse raw LLM output containing [[FILE:path]]...[[END_FILE]] blocks
    /// into a dictionary of file path → content.
    /// </summary>
    Dictionary<string, string> Parse(string llmOutput);

    /// <summary>
    /// Take rendered templates and parsed LLM blocks, inject LLM content into
    /// the appropriate injection zones, and return the final file set.
    /// </summary>
    Dictionary<string, string> Reconstruct(
        Dictionary<string, string> renderedTemplates,
        Dictionary<string, string> llmBlocks,
        ITemplateProvider templateProvider);

    /// <summary>
    /// Split the blocks of a build-repair response into the ones that may be written into
    /// <paramref name="outputDirectory"/> and the ones that must not — same routing rule
    /// <see cref="Reconstruct"/> applies to the first-pass output. See the implementation
    /// for why this reports instead of throwing.
    /// </summary>
    RepairWriteSet ResolveRepairWrites(Dictionary<string, string> llmBlocks, string outputDirectory);
}

/// <summary>
/// The verdict on a build-repair response: what is safe to write, and what was refused.
/// </summary>
/// <param name="Writes">Blocks whose path lands inside the generated project tree.</param>
/// <param name="Rejected">
/// Paths that would have been orphaned outside the tree, written outside the output directory,
/// or dropped into the archive as a literal <c>__zone__/…</c> file. Never written.
/// </param>
/// <param name="RejectionNotice">
/// Operator- and customer-facing explanation naming <paramref name="Rejected"/> and the valid
/// tree roots, or <c>null</c> when nothing was refused.
/// </param>
public sealed record RepairWriteSet(
    Dictionary<string, string> Writes,
    IReadOnlyList<string> Rejected,
    string? RejectionNotice);
