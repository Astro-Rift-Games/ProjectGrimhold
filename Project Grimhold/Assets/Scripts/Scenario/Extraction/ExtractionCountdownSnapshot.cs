using System;
using UnityEngine;

/// <summary>
/// Immutable snapshot reporting one player's extraction-zone countdown state.
/// This contract is intentionally distinct from the individual quota snapshot
/// introduced by US-13.
/// </summary>
public readonly struct ExtractionCountdownSnapshot : IEquatable<ExtractionCountdownSnapshot>
{
    public ExtractionState State { get; }
    public EntityId ActiveZoneId { get; }
    public float RemainingSeconds { get; }
    public float TotalSeconds { get; }
    public float Progress { get; }
    public float ElapsedSeconds => Mathf.Max(0f, TotalSeconds - RemainingSeconds);

    public ExtractionCountdownSnapshot(
        ExtractionState state,
        EntityId activeZoneId,
        float remainingSeconds,
        float totalSeconds,
        float progress)
    {
        State = state;
        ActiveZoneId = activeZoneId;
        RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        TotalSeconds = Mathf.Max(0f, totalSeconds);
        Progress = Mathf.Clamp01(progress);
    }

    public static ExtractionCountdownSnapshot None() =>
        new ExtractionCountdownSnapshot(ExtractionState.None, default, 0f, 0f, 0f);

    public static ExtractionCountdownSnapshot Extracted(EntityId activeZoneId) =>
        new ExtractionCountdownSnapshot(ExtractionState.Extracted, activeZoneId, 0f, 0f, 1f);

    public bool Equals(ExtractionCountdownSnapshot other)
    {
        return State == other.State &&
               ActiveZoneId == other.ActiveZoneId &&
               Mathf.Approximately(RemainingSeconds, other.RemainingSeconds) &&
               Mathf.Approximately(TotalSeconds, other.TotalSeconds) &&
               Mathf.Approximately(Progress, other.Progress);
    }

    public override bool Equals(object obj) => obj is ExtractionCountdownSnapshot other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine((int)State, ActiveZoneId, RemainingSeconds, TotalSeconds, Progress);

    public static bool operator ==(ExtractionCountdownSnapshot left, ExtractionCountdownSnapshot right) =>
        left.Equals(right);

    public static bool operator !=(ExtractionCountdownSnapshot left, ExtractionCountdownSnapshot right) =>
        !left.Equals(right);
}
