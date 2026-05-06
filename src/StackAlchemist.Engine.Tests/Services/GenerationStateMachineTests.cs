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
}
