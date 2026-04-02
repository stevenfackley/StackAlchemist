using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Worker.Tests;

public class StateMachineTests
{
    private static GenerationContext MakeContext(int retryCount = 0) => new()
    {
        GenerationId = Guid.NewGuid().ToString(),
        Mode = "simple",
        Tier = 2,
        RetryCount = retryCount,
    };

    [Fact]
    public void Transition_FromPending_ToGenerating_Succeeds()
    {
        var ctx = MakeContext();
        var result = GenerationStateMachine.Transition(GenerationState.Pending, GenerationEvent.EnginePickedUp, ctx);
        result.Should().Be(GenerationState.Generating);
    }

    [Fact]
    public void Transition_FromGenerating_ToBuilding_Succeeds()
    {
        var result = GenerationStateMachine.Transition(GenerationState.Generating, GenerationEvent.CodeReconstructed);
        result.Should().Be(GenerationState.Building);
    }

    [Fact]
    public void Transition_FromBuilding_ToPacking_OnBuildSuccess()
    {
        var result = GenerationStateMachine.Transition(GenerationState.Building, GenerationEvent.BuildPassed);
        result.Should().Be(GenerationState.Packing);
    }

    [Fact]
    public void Transition_FromBuilding_ToGenerating_OnBuildFailure_WhenRetriesRemain()
    {
        var ctx = MakeContext(retryCount: 0);
        var result = GenerationStateMachine.Transition(GenerationState.Building, GenerationEvent.BuildFailed, ctx);
        result.Should().Be(GenerationState.Generating);
        ctx.RetryCount.Should().Be(1);
    }

    [Fact]
    public void Transition_FromBuilding_ToFailed_OnBuildFailure_WhenMaxRetriesExceeded()
    {
        var ctx = MakeContext(retryCount: 3);
        var result = GenerationStateMachine.Transition(GenerationState.Building, GenerationEvent.BuildFailed, ctx);
        result.Should().Be(GenerationState.Failed);
    }

    [Fact]
    public void Transition_FromPacking_ToUploading_Succeeds()
    {
        var result = GenerationStateMachine.Transition(GenerationState.Packing, GenerationEvent.ZipCreated);
        result.Should().Be(GenerationState.Uploading);
    }

    [Fact]
    public void Transition_FromUploading_ToSuccess_Succeeds()
    {
        var result = GenerationStateMachine.Transition(GenerationState.Uploading, GenerationEvent.UploadedToR2);
        result.Should().Be(GenerationState.Success);
    }

    [Fact]
    public void Transition_InvalidTransition_ThrowsInvalidStateTransitionException()
    {
        var act = () => GenerationStateMachine.Transition(GenerationState.Pending, GenerationEvent.BuildPassed);
        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Transition_FromSuccess_ToAnything_ThrowsTerminalStateException()
    {
        var act = () => GenerationStateMachine.Transition(GenerationState.Success, GenerationEvent.EnginePickedUp);
        act.Should().Throw<InvalidStateTransitionException>()
           .WithMessage("*terminal state*");
    }

    [Fact]
    public void Transition_FromFailed_ToAnything_ThrowsTerminalStateException()
    {
        var act = () => GenerationStateMachine.Transition(GenerationState.Failed, GenerationEvent.EnginePickedUp);
        act.Should().Throw<InvalidStateTransitionException>()
           .WithMessage("*terminal state*");
    }

    [Fact]
    public void FullHappyPath_TransitionsThroughAllStates()
    {
        var ctx = MakeContext();
        var state = GenerationState.Pending;

        state = GenerationStateMachine.Transition(state, GenerationEvent.EnginePickedUp, ctx);
        state.Should().Be(GenerationState.Generating);

        state = GenerationStateMachine.Transition(state, GenerationEvent.CodeReconstructed, ctx);
        state.Should().Be(GenerationState.Building);

        state = GenerationStateMachine.Transition(state, GenerationEvent.BuildPassed, ctx);
        state.Should().Be(GenerationState.Packing);

        state = GenerationStateMachine.Transition(state, GenerationEvent.ZipCreated, ctx);
        state.Should().Be(GenerationState.Uploading);

        state = GenerationStateMachine.Transition(state, GenerationEvent.UploadedToR2, ctx);
        state.Should().Be(GenerationState.Success);

        ctx.RetryCount.Should().Be(0);
    }
}
