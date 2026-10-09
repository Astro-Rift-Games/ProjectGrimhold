using System;

/// <summary>Presentation edges derived from consecutive observations of one slot's confirmed execution.</summary>
[Flags]
public enum AbilityFeedbackEdge : byte
{
    None = 0,
    /// <summary>A new accepted execution (sequence advanced).</summary>
    Started = 1,
    /// <summary>The same execution moved from Preparing to Executing.</summary>
    PhaseAdvanced = 2,
    /// <summary>The execution finished or was stopped; the reason is not part of confirmed state.</summary>
    Ended = 4
}

/// <summary>
/// Detects start / phase / end edges of one slot from confirmed (sequence, phase) samples. Presentation-only: it
/// never feeds simulation. The first sample after construction, <see cref="Reset"/> or a backwards sequence is
/// adopted silently, so restore, Host Migration, rebind and presenter reactivation never replay old activations.
/// </summary>
public sealed class AbilityFeedbackTracker
{
    private bool _hasBaseline;
    private uint _sequence;
    private AbilityExecutionPhase _phase;

    /// <summary>Adopts the sample as the current state without reporting any edge.</summary>
    public void Baseline(uint sequence, AbilityExecutionPhase phase)
    {
        _hasBaseline = true;
        _sequence = sequence;
        _phase = phase;
    }

    /// <summary>Forgets the adopted state; the next sample is adopted silently.</summary>
    public void Reset() => _hasBaseline = false;

    public AbilityFeedbackEdge Observe(uint sequence, AbilityExecutionPhase phase)
    {
        if (!_hasBaseline || sequence < _sequence)
        {
            Baseline(sequence, phase);
            return AbilityFeedbackEdge.None;
        }

        var edges = AbilityFeedbackEdge.None;
        bool wasActive = _phase != AbilityExecutionPhase.Idle;
        bool isActive = phase != AbilityExecutionPhase.Idle;
        if (sequence > _sequence)
        {
            edges |= AbilityFeedbackEdge.Started;
            // Started and finished between two samples (e.g. an instant dash).
            if (!isActive) edges |= AbilityFeedbackEdge.Ended;
        }
        else if (wasActive && !isActive)
        {
            edges |= AbilityFeedbackEdge.Ended;
        }
        else if (_phase == AbilityExecutionPhase.Preparing && phase == AbilityExecutionPhase.Executing)
        {
            edges |= AbilityFeedbackEdge.PhaseAdvanced;
        }

        _sequence = sequence;
        _phase = phase;
        return edges;
    }
}
