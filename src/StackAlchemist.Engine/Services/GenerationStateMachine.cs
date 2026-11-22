using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public static class GenerationStateMachine
{
    /// <summary>
    /// How many automatic corrections a failing build gets (the Compile Guarantee's budget): on
    /// the failure after the last one, <see cref="GenerationEvent.BuildFailed"/> goes to
    /// <see cref="GenerationState.Failed"/>. The compile worker's loop, the repair prompts, the
    /// build report and the refund email all read this one number. Not the customer's own
    /// retries (<see cref="FailedBuildRefunder.RetryLimit"/>).
    /// </summary>
    public const int MaxBuildRetries = 3;

    private static readonly HashSet<GenerationState> TerminalStates =
        [GenerationState.Success, GenerationState.Failed];

    private static readonly Dictionary<(GenerationState, GenerationEvent), GenerationState> Transitions = new()
    {
        [(GenerationState.Pending, GenerationEvent.EnginePickedUp)] = GenerationState.Generating,
        [(GenerationState.Generating, GenerationEvent.CodeReconstructed)] = GenerationState.Building,
        // Tier 1 (Blueprint) — schema/api-docs emitted, skip the build loop entirely.
        [(GenerationState.Generating, GenerationEvent.BlueprintCompleted)] = GenerationState.Packing,
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
            if (context is not null && context.RetryCount < MaxBuildRetries)
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
