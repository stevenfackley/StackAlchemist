using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class PendingWriteBufferTests
{
    private static PendingGenerationWrite Write(string id) =>
        new(id, new Dictionary<string, object?> { ["status"] = "failed" }, DateTimeOffset.UtcNow);

    [Fact]
    public void Buffer_IsFifo()
    {
        var buffer = new PendingWriteBuffer();
        buffer.Enqueue(Write("a"));
        buffer.Enqueue(Write("b"));

        buffer.TryDequeue(out var first).Should().BeTrue();
        first!.GenerationId.Should().Be("a");
        buffer.TryDequeue(out var second).Should().BeTrue();
        second!.GenerationId.Should().Be("b");
        buffer.TryDequeue(out _).Should().BeFalse();
    }

    [Fact]
    public void Buffer_OverflowDropsOldest()
    {
        var buffer = new PendingWriteBuffer();
        for (var i = 0; i < 300; i++)
            buffer.Enqueue(Write($"gen-{i}"));

        buffer.Count.Should().Be(256);
        buffer.TryDequeue(out var oldest).Should().BeTrue();
        oldest!.GenerationId.Should().Be("gen-44", "the first 44 writes were dropped on overflow");
    }

    [Fact]
    public void Buffer_IsThreadSafeUnderConcurrentEnqueue()
    {
        var buffer = new PendingWriteBuffer();
        Parallel.For(0, 200, i => buffer.Enqueue(Write($"gen-{i}")));

        buffer.Count.Should().Be(200);
    }
}
