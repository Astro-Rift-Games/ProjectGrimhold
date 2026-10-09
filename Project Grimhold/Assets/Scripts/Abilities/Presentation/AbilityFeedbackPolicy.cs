/// <summary>Decides which authoritative ability outcomes are worth a one-shot owner notification.</summary>
public static class AbilityFeedbackPolicy
{
    public static bool ShouldNotifyRejection(AbilityActivationFailure failure) =>
        failure != AbilityActivationFailure.None;

    /// <summary>Completion and participation teardown are visible from confirmed state; only stops need a cue.</summary>
    public static bool ShouldNotifyInterruption(AbilityExecutionStopReason reason) =>
        reason != AbilityExecutionStopReason.Completed && reason != AbilityExecutionStopReason.ParticipationEnded;
}
