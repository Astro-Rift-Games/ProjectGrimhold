/// <summary>
/// Owns one non-overwritable authoritative drop request and one processed-result cache.
/// </summary>
public sealed class LootDropRequestState
{
    public enum Disposition
    {
        AcceptedPending,
        PendingDuplicate,
        PendingPayloadConflict,
        BusyWithDifferentSequence,
        ProcessedDuplicate,
        ProcessedPayloadConflict,
        StaleSequence
    }

    private bool _hasPending;
    private LootDropRequestIdentity _pending;
    private bool _hasProcessed;
    private LootDropRequestIdentity _processed;
    private LootDropConfirmation _processedConfirmation;

    public Disposition TryEnqueue(
        in LootDropRequestIdentity identity,
        out LootDropConfirmation cachedConfirmation)
    {
        cachedConfirmation = default;
        if (_hasPending)
        {
            if (identity.RequestSequence == _pending.RequestSequence)
            {
                return identity == _pending
                    ? Disposition.PendingDuplicate
                    : Disposition.PendingPayloadConflict;
            }

            return Disposition.BusyWithDifferentSequence;
        }

        if (_hasProcessed)
        {
            if (identity.RequestSequence == _processed.RequestSequence)
            {
                if (identity == _processed)
                {
                    cachedConfirmation = _processedConfirmation;
                    return Disposition.ProcessedDuplicate;
                }

                return Disposition.ProcessedPayloadConflict;
            }

            if (identity.RequestSequence < _processed.RequestSequence)
            {
                return Disposition.StaleSequence;
            }
        }

        _pending = identity;
        _hasPending = true;
        return Disposition.AcceptedPending;
    }

    public bool TryConsume(out LootDropRequestIdentity identity)
    {
        identity = _pending;
        if (!_hasPending)
        {
            return false;
        }

        _hasPending = false;
        return true;
    }

    public void RecordProcessed(
        in LootDropRequestIdentity identity,
        in LootDropConfirmation confirmation)
    {
        _processed = identity;
        _processedConfirmation = confirmation;
        _hasProcessed = true;
    }

    public void Reset()
    {
        _hasPending = false;
        _pending = default;
        _hasProcessed = false;
        _processed = default;
        _processedConfirmation = default;
    }
}
