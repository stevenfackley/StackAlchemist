using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class GenerationStateMachineTests
{
    [Fact]
    public void Transition_BlueprintCompleted_RoutesGeneratingToPacking()
    {
        // Tier 1 (Blueprint) path — orchestrator emits schema/api-docs and skips
        // the build loop entirely. This transition is the gate.
        var next = GenerationStateMachine.Transition(
            GenerationState.Generating, GenerationEvent.BlueprintCompleted);

        next.Should().Be(GenerationState.Packing);
    }

    [Fact]
    public void Transition_BlueprintCompleted_FromBuilding_IsRejected()
    {
        // Blueprint path is only valid out of Generating; Tier 2/3 must NOT
        // accidentally short-circuit a real build via this event.
        var act = () => GenerationStateMachine.Transition(
            GenerationState.Building, GenerationEvent.BlueprintCompleted);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Transition_HappyPathTier2_Pending_To_Success()
    {
        var ctx = new GenerationContext { GenerationId = "t", Mode = "advanced", Tier = 2 };
        var s = GenerationState.Pending;
        s = GenerationStateMachine.Transition(s, GenerationEvent.EnginePickedUp, ctx);
        s.Should().Be(GenerationState.Generating);
        s = GenerationStateMachine.Transition(s, GenerationEvent.CodeReconstructed, ctx);
        s.Should().Be(GenerationState.Building);
        s = GenerationStateMachine.Transition(s, GenerationEvent.BuildPassed, ctx);
        s.Should().Be(GenerationState.Packing);
        s = GenerationStateMachine.Transition(s, GenerationEvent.ZipCreated, ctx);
        s.Should().Be(GenerationState.Uploading);
        s = GenerationStateMachine.Transition(s, GenerationEvent.UploadedToR2, ctx);
        s.Should().Be(GenerationState.Success);
    }

    [Fact]
    public void Transition_HappyPathTier1_Pending_To_Success_SkipsBuilding()
    {
        var ctx = new GenerationContext { GenerationId = "t", Mode = "advanced", Tier = 1 };
        var s = GenerationState.Pending;
        s = GenerationStateMachine.Transition(s, GenerationEvent.EnginePickedUp, ctx);
        s.Should().Be(GenerationState.Generating);
        s = GenerationStateMachine.Transition(s, GenerationEvent.BlueprintCompleted, ctx);
        s.Should().Be(GenerationState.Packing); // skips Building entirely
        s = GenerationStateMachine.Transition(s, GenerationEvent.ZipCreated, ctx);
        s.Should().Be(GenerationState.Uploading);
        s = GenerationStateMachine.Transition(s, GenerationEvent.UploadedToR2, ctx);
        s.Should().Be(GenerationState.Success);
    }

    [Fact]
    public void Transition_FromTerminalState_Throws()
    {
        var act = () => GenerationStateMachine.Transition(
            GenerationState.Success, GenerationEvent.UploadedToR2);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    // ── BuildFailed edge cases ────────────────────────────────────────────────

    [Fact]
    public void Transition_BuildFailed_RetryCountBelowMax_ReturnsGenerating()
    {
        // RetryCount = MaxRetries-1 (2) → still has one retry left
        var ctx = new GenerationContext { GenerationId = "t", Mode = "advanced", Tier = 2, RetryCount = 2 };

        var next = GenerationStateMachine.Transition(GenerationState.Building, GenerationEvent.BuildFailed, ctx);

        next.Should().Be(GenerationState.Generating);
        ctx.RetryCount.Should().Be(3, because: "the state machine increments RetryCount on retry");
    }

    [Fact]
    public void Transition_BuildFailed_RetryCountAtMax_ReturnsFailed()
    {
        // RetryCount = MaxRetries (3) → no more retries
        var ctx = new GenerationContext { GenerationId = "t", Mode = "advanced", Tier = 2, RetryCount = 3 };

        var next = GenerationStateMachine.Transition(GenerationState.Building, GenerationEvent.BuildFailed, ctx);

        next.Should().Be(GenerationState.Failed);
    }

    [Fact]
    public void Transition_BuildFailed_NullContext_ReturnsFailed()
    {
        // Null context → can't retry regardless
        var next = GenerationStateMachine.Transition(GenerationState.Building, GenerationEvent.BuildFailed, context: null);

        next.Should().Be(GenerationState.Failed);
    }

    [Fact]
    public void Transition_FromFailedTerminalState_Throws()
    {
        var act = () => GenerationStateMachine.Transition(GenerationState.Failed, GenerationEvent.BuildFailed);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Theory]
    [InlineData(GenerationState.Pending,    GenerationEvent.EnginePickedUp,   GenerationState.Generating)]
    [InlineData(GenerationState.Generating, GenerationEvent.CodeReconstructed, GenerationState.Building)]
    [InlineData(GenerationState.Building,   GenerationEvent.BuildPassed,       GenerationState.Packing)]
    [InlineData(GenerationState.Packing,    GenerationEvent.ZipCreated,        GenerationState.Uploading)]
    [InlineData(GenerationState.Uploading,  GenerationEvent.UploadedToR2,      GenerationState.Success)]
    public void Transition_AllNormalTransitions_ReturnExpectedState(
        GenerationState from, GenerationEvent trigger, GenerationState expected)
    {
        var next = GenerationStateMachine.Transition(from, trigger);

        next.Should().Be(expected);
    }
}
