#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Fusion;

/// <summary>
/// Performs State Authority inventory setup inside Fusion simulation so equipment tests can
/// arrange ownership the same way raid admission does.
/// </summary>
public sealed class PlayerEquipmentSimulationDriver : SimulationBehaviour
{
    private static readonly RaidParticipantId TestParticipantId = CreateTestParticipantId();

    private enum RequestedOperation
    {
        None,
        InitializeLoadout,
        ForceSyncLoadout
    }

    private RequestedOperation _operation;
    private PlayerLootReceiver _receiver;
    private IReadOnlyList<LootEntry> _items;

    public int CompletionSequence { get; private set; }
    public bool LastResult { get; private set; }
    public string LastError { get; private set; }

    public void RequestInitializeLoadout(PlayerLootReceiver receiver, IReadOnlyList<LootEntry> items)
    {
        _receiver = receiver;
        _items = items;
        _operation = RequestedOperation.InitializeLoadout;
    }

    public void RequestForceSyncLoadout(PlayerLootReceiver receiver, IReadOnlyList<LootEntry> items)
    {
        _receiver = receiver;
        _items = items;
        _operation = RequestedOperation.ForceSyncLoadout;
    }

    public override void FixedUpdateNetwork()
    {
        if (_operation == RequestedOperation.None || _receiver == null)
        {
            return;
        }

        RequestedOperation operation = _operation;
        _operation = RequestedOperation.None;

        LastResult = _receiver.IsRaidLootOriginAware
            ? TrySyncRaidLoadout(_receiver, _items, out string error)
            : operation == RequestedOperation.InitializeLoadout
                ? _receiver.TryInitializeLoadout(_items, out error)
                : _receiver.TryForceSyncLoadout(_items, out error);
        LastError = error;
        CompletionSequence++;
    }

    private static bool TrySyncRaidLoadout(
        PlayerLootReceiver receiver,
        IReadOnlyList<LootEntry> items,
        out string error)
    {
        error = null;
        if (receiver.TryGetLootContent(out IReadOnlyList<LootEntry> current) && current.Count > 0)
        {
            if (!receiver.TryGetRaidLootOriginEntries(out IReadOnlyList<RaidLootOriginEntry> origins) ||
                !receiver.TryClearExactRaidContent(current, origins, out error))
            {
                error ??= "The Raid inventory test setup could not clear its exact provenance.";
                return false;
            }
        }

        return receiver.TryInitializeRaidLoadout(items, TestParticipantId, out error);
    }

    private static RaidParticipantId CreateTestParticipantId()
    {
        RaidParticipantId.TryCreate(1, out RaidParticipantId participantId);
        return participantId;
    }
}
#endif
