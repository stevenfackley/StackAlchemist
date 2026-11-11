using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// <see cref="BoundedOutput"/>: a build command's output is read to the end, but only its start
/// and its end are kept, so generated code cannot print the Engine out of memory.
/// </summary>
public sealed class BoundedOutputTests
{
    [Theory]
    [InlineData("")]
    [InlineData("error CS1002: ; expected")]
    public async Task Output_within_the_limits_comes_back_unchanged(string output)
    {
        (await BoundedOutput.ReadToEndAsync(new StringReader(output), headChars: 10, tailChars: 20))
            .Should().Be(output);
    }

    [Fact]
    public async Task Output_that_exactly_fills_the_head_and_the_tail_is_not_marked_as_cut()
    {
        var output = new string('h', 10) + new string('t', 20);

        (await BoundedOutput.ReadToEndAsync(new StringReader(output), headChars: 10, tailChars: 20))
            .Should().Be(output);
    }

    [Fact]
    public async Task Longer_output_keeps_its_start_and_its_end_and_says_how_much_was_dropped()
    {
        var output = "HEAD-START" + new string('x', 100_000) + "the build failed: 3 errors";

        var kept = await BoundedOutput.ReadToEndAsync(new StringReader(output), headChars: 10, tailChars: 26);

        kept.Should().StartWith("HEAD-START\n[… 100000 characters of output dropped …]\n");
        kept.Should().EndWith("the build failed: 3 errors");
        kept.Length.Should().BeLessThan(200);
    }

    [Fact]
    public async Task Endless_output_is_read_to_the_end_without_being_held()
    {
        // 200 MB of characters through the default limits: read whole, this would be a 400 MB string.
        const long total = 200L * 1024 * 1024;

        var kept = await BoundedOutput.ReadToEndAsync(new RepeatingReader('y', total));

        kept.Length.Should().BeLessThan(BoundedOutput.HeadChars + BoundedOutput.TailChars + 100);
        kept.Should().Contain($"[… {total - BoundedOutput.HeadChars - BoundedOutput.TailChars} characters of output dropped …]");
    }

    /// <summary>Yields one character <paramref name="total"/> times, without ever holding them.</summary>
    private sealed class RepeatingReader(char value, long total) : TextReader
    {
        private long _left = total;

        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var count = (int)Math.Min(buffer.Length, _left);
            buffer.Span[..count].Fill(value);
            _left -= count;
            return ValueTask.FromResult(count);
        }
    }
}
