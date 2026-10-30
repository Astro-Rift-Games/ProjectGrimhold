using System;

/// <summary>Local, per-attack presentation clock. Never changes a simulation deadline.</summary>
public struct AttackHitstop
{
    public int Sequence { get; private set; }
    public float StartSeconds { get; private set; }
    public float HoldSeconds { get; private set; }

    public bool TrySchedule(int sequence, float contactSeconds, float currentRawElapsed, float durationSeconds)
    {
        if (sequence <= 0 || sequence == Sequence || !IsFinite(contactSeconds) ||
            !IsFinite(currentRawElapsed) || !IsFinite(durationSeconds) || durationSeconds <= 0f)
            return false;

        float start = Math.Max(contactSeconds, currentRawElapsed);
        float hold = durationSeconds - (start - contactSeconds);
        // Confirmation latency spends the original budget; it never replays the contact pose.
        if (hold < .01f) return false;
        Sequence = sequence;
        StartSeconds = start;
        HoldSeconds = hold;
        return true;
    }

    public float Map(int sequence, float rawSeconds)
    {
        if (Sequence <= 0 || sequence != Sequence || rawSeconds < StartSeconds) return rawSeconds;
        // Guard the boundary against float subtraction rounding just below the held pose.
        return Math.Max(StartSeconds, rawSeconds - HoldSeconds);
    }

    /// <summary>Release the visual delay, but keep this attack consumed until a new sequence.</summary>
    public void Cancel()
    {
        StartSeconds = 0f;
        HoldSeconds = 0f;
    }

    public void GetRemaining(float rawSeconds, out float delaySeconds, out float holdSeconds)
    {
        delaySeconds = Math.Max(0f, StartSeconds - rawSeconds);
        holdSeconds = Sequence > 0
            ? Math.Max(0f, StartSeconds + HoldSeconds - Math.Max(StartSeconds, rawSeconds)) : 0f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
