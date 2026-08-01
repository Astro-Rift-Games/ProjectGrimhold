using System;
using UnityEngine;

/// <summary>
/// Immutable snapshot reporting the state and progress of an extraction operation.
/// Exposes ActiveZoneId, RemainingSeconds, TotalSeconds and normalized Progress.
/// </summary>
public readonly struct ExtractionProgressSnapshot : IEquatable<ExtractionProgressSnapshot>
{
    /// <summary>
    /// Gets the process state of the extraction.
    /// </summary>
    public ExtractionState State { get; }

    /// <summary>
    /// Gets the canonical EntityId of the active extraction zone.
    /// </summary>
    public EntityId ActiveZoneId { get; }

    /// <summary>
    /// Gets the remaining countdown duration in seconds.
    /// </summary>
    public float RemainingSeconds { get; }

    /// <summary>
    /// Gets the total required countdown duration in seconds.
    /// </summary>
    public float TotalSeconds { get; }

    /// <summary>
    /// Gets the normalized progress value ranging from 0.0 to 1.0.
    /// </summary>
    public float Progress { get; }

    /// <summary>
    /// Gets the elapsed duration in seconds for the current extraction countdown.
    /// </summary>
    public float ElapsedSeconds => Mathf.Max(0f, TotalSeconds - RemainingSeconds);

    public ExtractionProgressSnapshot(ExtractionState state, EntityId activeZoneId, float remainingSeconds, float totalSeconds, float progress)
    {
        State = state;
        ActiveZoneId = activeZoneId;
        RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        TotalSeconds = Mathf.Max(0f, totalSeconds);
        Progress = Mathf.Clamp01(progress);
    }

    public static ExtractionProgressSnapshot None() => new ExtractionProgressSnapshot(ExtractionState.None, default, 0f, 0f, 0f);

    public static ExtractionProgressSnapshot Extracted(EntityId activeZoneId) =>
        new ExtractionProgressSnapshot(ExtractionState.Extracted, activeZoneId, 0f, 0f, 1f);

    public bool Equals(ExtractionProgressSnapshot other)
    {
        return State == other.State &&
               ActiveZoneId == other.ActiveZoneId &&
               Mathf.Approximately(RemainingSeconds, other.RemainingSeconds) &&
               Mathf.Approximately(TotalSeconds, other.TotalSeconds) &&
               Mathf.Approximately(Progress, other.Progress);
    }

    public override bool Equals(object obj)
    {
        return obj is ExtractionProgressSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)State, ActiveZoneId, RemainingSeconds, TotalSeconds, Progress);
    }

    public static bool operator ==(ExtractionProgressSnapshot left, ExtractionProgressSnapshot right) => left.Equals(right);
    public static bool operator !=(ExtractionProgressSnapshot left, ExtractionProgressSnapshot right) => !left.Equals(right);
}
