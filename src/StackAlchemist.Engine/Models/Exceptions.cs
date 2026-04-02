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
