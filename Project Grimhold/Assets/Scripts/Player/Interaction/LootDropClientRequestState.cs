/// <summary>
/// Tracks one legitimate Input Authority drop request in flight.
/// </summary>
public sealed class LootDropClientRequestState
{
    private uint _lastSentSequence;
    private bool _hasInFlight;
    private LootDropRequestIdentity _expected;

    public bool HasInFlight => _hasInFlight;

    public bool TryCreateCandidate(
        int catalogIndex,
        LootTransferQuantityMode quantityMode,
        out LootDropRequestIdentity identity)
    {
        identity = default;
        if (_hasInFlight || catalogIndex < 0 || !IsSupported(quantityMode))
        {
            return false;
        }

        uint sequence = unchecked(_lastSentSequence + 1);
        if (sequence == 0)
        {
            sequence = 1;
        }

        identity = new LootDropRequestIdentity(sequence, catalogIndex, quantityMode);
        return true;
    }

    public void MarkSent(in LootDropRequestIdentity identity)
    {
        _lastSentSequence = identity.RequestSequence;
        _expected = identity;
        _hasInFlight = true;
    }

    public bool TryRelease(uint requestSequence, out LootDropRequestIdentity expected)
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

    public void Reset()
    {
        _lastSentSequence = 0;
        _hasInFlight = false;
        _expected = default;
    }

    private static bool IsSupported(LootTransferQuantityMode mode) =>
        mode == LootTransferQuantityMode.SingleUnit ||
        mode == LootTransferQuantityMode.FullStack;
}
