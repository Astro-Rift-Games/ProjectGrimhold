/// <summary>
/// Tracks one legitimate Input Authority request in flight and advances sequence only after accepted transport.
/// </summary>
public sealed class LootTransferClientRequestState
{
    private uint _lastSentSequence;
    private bool _hasInFlight;
    private LootTransferRequestIdentity _expected;

    public bool HasInFlight => _hasInFlight;

    public bool TryCreateCandidate(
        EntityId sourceId,
        EntityId destinationId,
        int catalogIndex,
        out LootTransferRequestIdentity identity)
    {
        identity = default;
        if (_hasInFlight || sourceId.Value == 0 || destinationId.Value == 0 ||
            sourceId == destinationId || catalogIndex < 0)
        {
            return false;
        }

        uint sequence = unchecked(_lastSentSequence + 1);
        if (sequence == 0)
        {
            sequence = 1;
        }

        identity = new LootTransferRequestIdentity(sequence, sourceId, destinationId, catalogIndex);
        return true;
    }

    public void MarkSent(in LootTransferRequestIdentity identity)
    {
        _lastSentSequence = identity.RequestSequence;
        _expected = identity;
        _hasInFlight = true;
    }

    public bool TryRelease(uint requestSequence, out LootTransferRequestIdentity expected)
    {
        expected = _expected;
        if (!_hasInFlight || requestSequence != _expected.RequestSequence)
        {
            return false;
        }

        _hasInFlight = false;
        _expected = default;
        return true;
    }

    public bool TryGetExpected(out LootTransferRequestIdentity expected)
    {
        expected = _expected;
        return _hasInFlight;
    }

    public void Reset()
    {
        _lastSentSequence = 0;
        _hasInFlight = false;
        _expected = default;
    }
}
