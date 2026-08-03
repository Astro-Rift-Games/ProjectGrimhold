using System;
using UnityEngine;

/// <summary>
/// Read-only projection of confirmed ritual state for presentation consumers.
/// Timing values are derived from configuration and the authoritative TickTimer.
/// </summary>
public readonly struct ExtractionRitualSnapshot : IEquatable<ExtractionRitualSnapshot>
{
    public ExtractionRitualState State { get; }
    public float TotalSeconds { get; }
    public float RemainingSeconds { get; }
    public float Progress { get; }

    public ExtractionRitualSnapshot(
        ExtractionRitualState state,
        float totalSeconds,
        float remainingSeconds,
        float progress)
    {
        State = state;
        TotalSeconds = totalSeconds;
        RemainingSeconds = remainingSeconds;
        Progress = Mathf.Clamp01(progress);
    }

    public bool Equals(ExtractionRitualSnapshot other)
    {
        return State == other.State &&
            TotalSeconds.Equals(other.TotalSeconds) &&
            RemainingSeconds.Equals(other.RemainingSeconds) &&
            Progress.Equals(other.Progress);
    }

    public override bool Equals(object obj)
    {
        return obj is ExtractionRitualSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(State, TotalSeconds, RemainingSeconds, Progress);
    }
}
