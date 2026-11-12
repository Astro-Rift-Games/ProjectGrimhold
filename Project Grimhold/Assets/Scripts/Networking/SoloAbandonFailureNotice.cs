/// <summary>
/// Local observer of the replicated Abandon failure counter: reports each new failure once.
/// </summary>
public sealed class SoloAbandonFailureNotice
{
    private bool _hasBaseline;
    private int _lastSequence;

    /// <summary>
    /// Returns true when the counter moved since the previous observation. The first observation
    /// only records the baseline, so a rebind (reconnect, Host Migration) never replays a failure.
    /// </summary>
    public bool Observe(int failureSequence)
    {
        if (!_hasBaseline)
        {
            _hasBaseline = true;
            _lastSequence = failureSequence;
            return false;
        }

        if (failureSequence == _lastSequence)
        {
            return false;
        }

        _lastSequence = failureSequence;
        return true;
    }
}
