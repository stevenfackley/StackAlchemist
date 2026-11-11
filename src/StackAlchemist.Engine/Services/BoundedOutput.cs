using System.Globalization;
using System.Text;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Reads a build command's output to the end while holding only its start and its end in memory.
///
/// Build commands run generated code inside the Engine's own process tree, and their output used
/// to be read whole: a loop that prints forever could take gigabytes off the Engine in the ten
/// minutes a step is allowed, and with it every other customer's queued build. The reader still
/// drains the pipe to the end, so the child never blocks on a full pipe; it just stops keeping
/// the middle. The start is kept for the command's banner and first errors, the end for the
/// failure summary every toolchain prints last.
/// </summary>
internal static class BoundedOutput
{
    /// <summary>Characters kept from the start of the output.</summary>
    public const int HeadChars = 32 * 1024;

    /// <summary>Characters kept from the end of the output.</summary>
    public const int TailChars = 256 * 1024;

    public static async Task<string> ReadToEndAsync(
        TextReader reader, int headChars = HeadChars, int tailChars = TailChars)
    {
        var buffer = new char[8 * 1024];
        var head = new StringBuilder();
        StringBuilder? tail = null;
        long dropped = 0;

        int read;
        // Not cancellable, as before: the read ends when the child, or the kill that follows a
        // cancellation, closes the pipe.
        while ((read = await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None)) > 0)
        {
            var toHead = Math.Min(read, headChars - head.Length);
            if (toHead > 0)
                head.Append(buffer, 0, toHead);
            if (toHead == read)
                continue;

            tail ??= new StringBuilder();
            tail.Append(buffer, toHead, read - toHead);

            // Trimmed only once it holds twice its limit, so the copy that a trim costs is paid
            // once per `tailChars` read rather than on every chunk.
            if (tail.Length > tailChars * 2L)
                dropped += TrimToLast(tail, tailChars);
        }

        if (tail is null)
            return head.ToString();

        dropped += TrimToLast(tail, tailChars);
        if (dropped > 0)
            head.Append(CultureInfo.InvariantCulture, $"\n[… {dropped} characters of output dropped …]\n");

        return head.Append(tail).ToString();
    }

    private static int TrimToLast(StringBuilder text, int keep)
    {
        var excess = text.Length - keep;
        if (excess <= 0)
            return 0;

        text.Remove(0, excess);
        return excess;
    }
}
