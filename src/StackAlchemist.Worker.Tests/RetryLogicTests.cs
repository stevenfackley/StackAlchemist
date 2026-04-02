using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Worker.Tests;

public class RetryLogicTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ShouldRetry_WhenRetryCountBelowMax_TransitionsToGenerating(int retryCount)
    {
        var ctx = new GenerationContext
        {
            GenerationId = "test", Mode = "simple", Tier = 2,
            RetryCount = retryCount,
        };

        var result = GenerationStateMachine.Transition(
            GenerationState.Building, GenerationEvent.BuildFailed, ctx);

        result.Should().Be(GenerationState.Generating);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void ShouldRetry_WhenRetryCountAtOrAboveMax_TransitionsToFailed(int retryCount)
    {
        var ctx = new GenerationContext
        {
            GenerationId = "test", Mode = "simple", Tier = 2,
            RetryCount = retryCount,
        };

        var result = GenerationStateMachine.Transition(
            GenerationState.Building, GenerationEvent.BuildFailed, ctx);

        result.Should().Be(GenerationState.Failed);
    }

    [Fact]
    public void RetryLoop_IncreasesRetryCount()
    {
        var ctx = new GenerationContext
        {
            GenerationId = "test", Mode = "simple", Tier = 2,
            RetryCount = 0,
        };

        // Simulate 3 build failures
        for (var i = 0; i < 3; i++)
        {
            var state = GenerationStateMachine.Transition(
                GenerationState.Building, GenerationEvent.BuildFailed, ctx);

            if (i < 2)
            {
                state.Should().Be(GenerationState.Generating);
                // Transition back to building for next iteration
                state = GenerationStateMachine.Transition(state, GenerationEvent.CodeReconstructed, ctx);
                state.Should().Be(GenerationState.Building);
            }
            else
            {
                // Third failure at retryCount=3 → failed
                state.Should().Be(GenerationState.Failed);
            }
        }

        ctx.RetryCount.Should().Be(3);
    }
}
