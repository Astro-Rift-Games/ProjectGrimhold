/// <summary>An authored phase transition. Zero duration means no phase deadline, not automatic completion.</summary>
public readonly struct AbilityExecutionPlan
{
    public AbilityExecutionPhase Phase { get; }
    public float DurationSeconds { get; }

    public AbilityExecutionPlan(AbilityExecutionPhase phase, float durationSeconds = 0f)
    {
        Phase = phase;
        DurationSeconds = durationSeconds;
    }

    public bool IsValidStart =>
        (Phase == AbilityExecutionPhase.Executing || Phase == AbilityExecutionPhase.Preparing) &&
        !float.IsNaN(DurationSeconds) && !float.IsInfinity(DurationSeconds) &&
        DurationSeconds >= 0f && (Phase != AbilityExecutionPhase.Preparing || DurationSeconds > 0f);
}
