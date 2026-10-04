namespace StackAlchemist.Engine.Services;

/// <summary>
/// The one indentation rule for text spliced into an <c>[[LLM_INJECTION_START/END]]</c> zone
/// (StackAlchemist#450): a fill keeps its own <em>relative</em> indentation, and its
/// <em>absolute</em> indentation is the START marker line's.
///
/// Whitespace-significant languages need both halves. A Python fill inside a <c>def</c> must land
/// at the body's indentation whatever column the model wrote it at, and a nested
/// <c>if</c>/<c>for</c> inside the fill must stay nested. The marker's whitespace is used
/// verbatim, so a tab-indented template gets tabs and a space-indented one gets spaces; the fill's
/// relative indentation is never re-tabbed (there is no reliable tab width to convert with).
/// </summary>
internal static class ZoneIndentation
{
    /// <summary>
    /// Drops leading and trailing whitespace-only lines and trailing whitespace, but never the
    /// first content line's indentation — <c>string.Trim()</c> did, which put line 1 of every
    /// zone at column 0. Line endings are normalised to <c>\n</c>.
    /// </summary>
    public static string TrimBlankLines(string text)
    {
        var lines = SplitLines(text);
        var first = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        if (first < 0)
            return string.Empty;

        var last = Array.FindLastIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        return string.Join('\n', lines[first..(last + 1)]).TrimEnd();
    }

    /// <summary>
    /// Removes the fill's common leading whitespace, then prefixes every non-blank line with
    /// <paramref name="indent"/>. Whitespace-only lines become empty (flake8 W293), and the lines
    /// are joined with <paramref name="newline"/>.
    /// </summary>
    public static string Reindent(string content, string indent, string newline)
    {
        var lines = SplitLines(content);
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                lines[i] = string.Empty;
        }

        var common = CommonLeadingWhitespace(lines);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
                lines[i] = indent + lines[i][common.Length..];
        }

        return string.Join(newline, lines);
    }

    /// <summary>
    /// The leading whitespace of the line containing <paramref name="index"/> — the marker's
    /// indentation when the marker opens its line (every shipped template), else the indentation
    /// of whatever code precedes it.
    /// </summary>
    public static string LineIndentation(string text, int index)
    {
        var lineStart = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        var end = lineStart;
        while (end < index && text[end] is ' ' or '\t')
            end++;
        return text[lineStart..end];
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    /// <summary>
    /// Longest whitespace prefix shared by every non-empty line, compared character by character
    /// (like Python's <c>textwrap.dedent</c>), so a tab and four spaces are never treated as equal.
    /// </summary>
    private static string CommonLeadingWhitespace(string[] lines)
    {
        string? common = null;
        foreach (var line in lines)
        {
            if (line.Length == 0)
                continue;

            var width = 0;
            while (width < line.Length && line[width] is ' ' or '\t')
                width++;
            var leading = line[..width];

            if (common is null)
            {
                common = leading;
                continue;
            }

            var shared = 0;
            while (shared < common.Length && shared < leading.Length && common[shared] == leading[shared])
                shared++;
            common = common[..shared];
        }

        return common ?? string.Empty;
    }
}
