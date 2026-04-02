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
}
