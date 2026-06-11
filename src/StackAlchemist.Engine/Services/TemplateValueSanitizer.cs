namespace StackAlchemist.Engine.Services;

/// <summary>
/// Sanitizes user-derived values before they enter the Handlebars template context.
/// The template engine runs with NoEscape=true (HTML-escaping would corrupt generated
/// source code), so identifier whitelisting is the injection defense: stripping
/// everything outside [A-Za-z0-9_] removes '{', '}', '/', '.' and '-' — killing
/// {{...}} re-evaluation AND path traversal in one move. These values also become C#
/// class names, file paths, and SQL identifiers, so the whitelist is required anyway.
/// </summary>
internal static class TemplateValueSanitizer
{
    internal static string SanitizeIdentifier(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var c in value)
        {
            if (char.IsAsciiLetterOrDigit(c) || c == '_')
                buffer[length++] = c;
        }

        // Must start with a letter to be a valid identifier everywhere it lands.
        var start = 0;
        while (start < length && !char.IsAsciiLetter(buffer[start]))
            start++;

        return start >= length ? fallback : new string(buffer[start..length]);
    }
}
