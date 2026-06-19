using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public sealed class InFlightGenerationRegistryTests
{
    private readonly InFlightGenerationRegistry _sut = new();

    [Fact]
    public void Add_MakesContainsTrue()
    {
        _sut.Add("gen-1");

        _sut.Contains("gen-1").Should().BeTrue();
    }

    [Fact]
    public void Remove_AfterAdd_MakesContainsFalse()
    {
        _sut.Add("gen-2");
        _sut.Remove("gen-2");

        _sut.Contains("gen-2").Should().BeFalse();
    }

    [Fact]
    public void Remove_NonExistentId_IsNoOp()
    {
        var act = () => _sut.Remove("does-not-exist");

        act.Should().NotThrow();
        _sut.Contains("does-not-exist").Should().BeFalse();
    }

    [Fact]
    public void Add_SameIdTwice_IsIdempotent()
    {
        _sut.Add("gen-3");
        _sut.Add("gen-3");

        _sut.Contains("gen-3").Should().BeTrue();
    }

    [Fact]
    public void Contains_UnknownId_ReturnsFalse()
    {
        _sut.Contains("never-added").Should().BeFalse();
    }

    [Fact]
    public void ConcurrentAdds_AllSucceed()
    {
        var ids = Enumerable.Range(0, 100).Select(i => $"gen-concurrent-{i}").ToList();

        Parallel.ForEach(ids, id => _sut.Add(id));

        foreach (var id in ids)
            _sut.Contains(id).Should().BeTrue(because: $"{id} should be tracked after concurrent add");
    }
}
