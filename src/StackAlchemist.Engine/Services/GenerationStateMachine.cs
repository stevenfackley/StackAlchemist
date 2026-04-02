using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public static class GenerationStateMachine
{
    private const int MaxRetries = 3;

    private static readonly HashSet<GenerationState> TerminalStates =
        [GenerationState.Success, GenerationState.Failed];

    private static readonly Dictionary<(GenerationState, GenerationEvent), GenerationState> Transitions = new()
    {
        [(GenerationState.Pending, GenerationEvent.EnginePickedUp)] = GenerationState.Generating,
        [(GenerationState.Generating, GenerationEvent.CodeReconstructed)] = GenerationState.Building,
        [(GenerationState.Building, GenerationEvent.BuildPassed)] = GenerationState.Packing,
        // BuildFailed is handled specially — routes to Generating or Failed depending on retry count
        [(GenerationState.Packing, GenerationEvent.ZipCreated)] = GenerationState.Uploading,
        [(GenerationState.Uploading, GenerationEvent.UploadedToR2)] = GenerationState.Success,
    };

    public static GenerationState Transition(
        GenerationState current,
        GenerationEvent trigger,
        GenerationContext? context = null)
    {
        if (TerminalStates.Contains(current))
        {
            throw new InvalidStateTransitionException(
                $"Cannot transition from terminal state '{current}'.");
        }

        // Special handling for BuildFailed — retry or fail
        if (current == GenerationState.Building && trigger == GenerationEvent.BuildFailed)
        {
            if (context is not null && context.RetryCount < MaxRetries)
            {
                context.RetryCount++;
                return GenerationState.Generating;
            }

            return GenerationState.Failed;
        }

        if (Transitions.TryGetValue((current, trigger), out var next))
        {
            return next;
        }

        throw new InvalidStateTransitionException(
            $"Invalid transition: '{current}' + '{trigger}' has no defined target state.");
    }
}
