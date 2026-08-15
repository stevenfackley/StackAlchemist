namespace StackAlchemist.Engine.Models;

public class MalformedLlmOutputException : Exception
{
    public MalformedLlmOutputException(string message) : base(message) { }
}

public class TruncatedLlmResponseException : MalformedLlmOutputException
{
    public TruncatedLlmResponseException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the LLM emits <c>[[FILE:…]]</c> blocks whose paths exist nowhere in the
/// rendered project tree, so writing them would drop source files at the archive root where
/// no compiler, bundler or dev server ever looks at them.
///
/// This is deliberately fatal rather than a warning. The failure it replaces was silent: the
/// V1 prompt asked for <c>src/types/index.ts</c> while the tree keeps its frontend under
/// <c>nextjs/src/</c>, so every paid generation quietly shipped an orphan <c>src/</c>
/// directory and a frontend with no types, no API client and an empty page — a broken
/// deliverable that still passed the build gate. A failed generation is visible and
/// refundable; a plausible-looking archive that is missing half the app is neither.
///
/// Derives from <see cref="MalformedLlmOutputException"/> so <c>ErrorCategorizer</c> reports
/// it as a schema-class failure and the retry prompt can name the offending paths.
/// </summary>
public class UnmappedLlmFileException : MalformedLlmOutputException
{
    public UnmappedLlmFileException(IReadOnlyCollection<string> unmappedPaths, IReadOnlyCollection<string> treeRoots)
        : base(BuildMessage(unmappedPaths, treeRoots))
    {
        UnmappedPaths = [.. unmappedPaths];
    }

    /// <summary>The emitted paths that matched nothing, in the order they were parsed.</summary>
    public IReadOnlyList<string> UnmappedPaths { get; }

    private static string BuildMessage(IReadOnlyCollection<string> unmappedPaths, IReadOnlyCollection<string> treeRoots)
    {
        var paths = string.Join(", ", unmappedPaths);
        var roots = treeRoots.Count > 0
            ? string.Join(", ", treeRoots.Order(StringComparer.Ordinal))
            : "(none — the rendered template produced no directories)";

        // "__zone__/" is ReconstructionService.ZonePathPrefix, spelled out here so the
        // Models layer stays free of a Services reference.
        return $"The model emitted {unmappedPaths.Count} file block(s) that do not belong to the generated " +
               $"project tree and would have been written to the archive root, where nothing reads them: {paths}. " +
               $"Emit paths under one of the project's top-level directories ({roots}), " +
               "or address an injection zone as '__zone__/<ZoneName>'.";
    }
}

public class MissingTemplateVariableException : Exception
{
    public MissingTemplateVariableException(string message) : base(message) { }
}

public class TemplateNotFoundException : Exception
{
    public TemplateNotFoundException(string message) : base(message) { }
}

public class InvalidStateTransitionException : InvalidOperationException
{
    public InvalidStateTransitionException(string message) : base(message) { }
}

/// <summary>Thrown when the LLM returns a response that cannot be parsed as a valid JSON schema.</summary>
public class SchemaExtractionException : Exception
{
    public SchemaExtractionException(string message) : base(message) { }
    public SchemaExtractionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Thrown when the extracted schema references entities or fields that do not exist.</summary>
public class SchemaValidationException : Exception
{
    public SchemaValidationException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the Anthropic API stays rate-limited/overloaded (429/529) after the
/// retry budget is exhausted. Distinct from other HTTP failures so it can surface
/// to the user as "high demand, try again shortly" instead of a generic error.
/// </summary>
public class LlmRateLimitException : Exception
{
    public LlmRateLimitException(string message) : base(message) { }
}

/// <summary>Thrown when a tier value outside the range 1–3 is supplied.</summary>
public class InvalidTierException : Exception
{
    public InvalidTierException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a generation is routed to a BYOK-only provider (OpenAI / OpenRouter) but no usable
/// API key could be resolved — either none was stored, or the stored ciphertext failed to decrypt.
/// The message is user-safe and MUST NEVER contain key material. Categorized as "internal" (the
/// error_category CHECK enum has no dedicated "config" value).
/// </summary>
public class ByokConfigException : Exception
{
    public ByokConfigException(string message) : base(message) { }
}

/// <summary>
/// Base type for Cloudflare R2 bucket-configuration failures.
/// Throw when the bucket cannot be reached due to a mis-set
/// R2_BUCKET_NAME / R2_ACCOUNT_ID / credentials, distinct from
/// transient network or upload errors.
/// </summary>
public abstract class R2BucketException : InvalidOperationException
{
    protected R2BucketException(string message) : base(message) { }
}

/// <summary>Thrown when the configured R2 bucket does not exist in the configured Cloudflare account.</summary>
public sealed class R2BucketNotFoundException : R2BucketException
{
    public R2BucketNotFoundException(string bucket, string accountId)
        : base($"R2 bucket '{bucket}' was not found in account '{accountId}'. " +
               $"Check R2_BUCKET_NAME and R2_ACCOUNT_ID, and verify the bucket exists in the Cloudflare R2 dashboard.") { }
}

/// <summary>Thrown when the configured R2 credentials cannot access the bucket (403/AccessDenied).</summary>
public sealed class R2BucketAccessDeniedException : R2BucketException
{
    public R2BucketAccessDeniedException(string bucket, string accountId)
        : base($"R2 bucket '{bucket}' in account '{accountId}' exists but the configured credentials cannot access it. " +
               $"Check R2_ACCESS_KEY_ID / R2_SECRET_ACCESS_KEY scopes (must include Object Read & Write for this bucket).") { }
}
