using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Guards against silently consuming an LLM response that was cut off at the
/// output-token ceiling. A truncated response drops file blocks mid-stream; the
/// build then fails and the retry reproduces the same truncation — so fail fast
/// with a schema-category error ("app too large") instead of burning retries.
/// </summary>
public static class LlmResponseGuard
{
    public static void ThrowIfTruncated(LlmResponse response, string contextDescription)
    {
        if (response.StopReason == "max_tokens")
        {
            throw new TruncatedLlmResponseException(
                $"The model hit its output token limit while {contextDescription}. " +
                "The requested application is too large for a single generation — reduce the number of entities or fields and try again.");
        }
    }
}
