namespace StackAlchemist.Engine.Models;

public class MalformedLlmOutputException : Exception
{
    public MalformedLlmOutputException(string message) : base(message) { }
}

public class TruncatedLlmResponseException : MalformedLlmOutputException
{
    public TruncatedLlmResponseException(string message) : base(message) { }
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

/// <summary>Thrown when a tier value outside the range 1–3 is supplied.</summary>
public class InvalidTierException : Exception
{
    public InvalidTierException(string message) : base(message) { }
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
