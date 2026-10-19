/// <summary>
/// Detects a new recovery outcome from the replicated outcome sequence. The first sample after
/// construction, <see cref="Reset"/> or a backwards sequence is adopted silently, so late join,
/// rebind, restore and Host Migration never replay an old outcome.
/// </summary>
public sealed class DownedRecoveryOutcomeTracker
{
    private bool _hasBaseline;
    private int _sequence;

    public void Baseline(int sequence)
    {
        _hasBaseline = true;
        _sequence = sequence;
    }

    public void Reset() => _hasBaseline = false;

    /// <summary>True exactly once for each sequence advance after the baseline.</summary>
    public bool Observe(int sequence)
    {
        if (!_hasBaseline || sequence < _sequence)
        {
            Baseline(sequence);
            return false;
        }

        bool isNew = sequence > _sequence;
        _sequence = sequence;
        return isNew;
    }
}
