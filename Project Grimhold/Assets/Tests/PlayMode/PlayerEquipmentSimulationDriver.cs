#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Fusion;

/// <summary>
/// Performs State Authority inventory setup inside Fusion simulation so equipment tests can
/// arrange ownership the same way raid admission does.
/// </summary>
public sealed class PlayerEquipmentSimulationDriver : SimulationBehaviour
{
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

        LastResult = operation == RequestedOperation.InitializeLoadout
            ? _receiver.TryInitializeLoadout(_items, out string error)
            : _receiver.TryForceSyncLoadout(_items, out error);
        LastError = error;
        CompletionSequence++;
    }
}
#endif
