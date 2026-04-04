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

        // Simulate 4 build failures: MaxRetries=3, so the 4th failure (retryCount reaches 3) → Failed
        for (var i = 0; i < 4; i++)
        {
            var state = GenerationStateMachine.Transition(
                GenerationState.Building, GenerationEvent.BuildFailed, ctx);

            if (i < 3)
            {
                state.Should().Be(GenerationState.Generating);
                // Transition back to building for next iteration
                state = GenerationStateMachine.Transition(state, GenerationEvent.CodeReconstructed, ctx);
                state.Should().Be(GenerationState.Building);
            }
            else
            {
                // Fourth failure: retryCount already at 3 (MaxRetries) → Failed
                state.Should().Be(GenerationState.Failed);
            }
        }

        ctx.RetryCount.Should().Be(3);
    }

    [Fact]
    public void BuildFailed_WithNullContext_TransitionsToFailed()
    {
        var result = GenerationStateMachine.Transition(
            GenerationState.Building,
            GenerationEvent.BuildFailed,
            context: null);

        result.Should().Be(GenerationState.Failed);
    }
}
