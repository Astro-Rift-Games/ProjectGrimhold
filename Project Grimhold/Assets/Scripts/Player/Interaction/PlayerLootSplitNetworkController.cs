using System;
using Fusion;
using UnityEngine;

/// <summary>Queues authenticated split intentions; only State Authority commits on a simulation tick.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerLootSplitNetworkController : NetworkBehaviour
{
    private PlayerLootReceiver _receiver;
    private ICharacter _character;
    private PlayerExtractionController _extraction;
    private NetworkMatchController _match;
    private bool _dependenciesValid;
    private uint _sequence;
    private uint _inFlight;
    private uint _pendingSequence;
    private int _source;
    private int _amount;
    private int _destination;
    private int _revision;
    private bool _notify;
    private LootTransferFailureReason _confirmation;

    public bool HasRequestInFlight => _inFlight != 0;
    public event Action<LootTransferFailureReason> SplitConfirmed;

    public override void Spawned()
    {
        _receiver = GetComponent<PlayerLootReceiver>();
        _character = GetComponent<ICharacter>();
        _extraction = GetComponent<PlayerExtractionController>();
        _match = Runner.GetComponent<NetworkMatchController>();
        _dependenciesValid = _receiver != null && _character != null && _extraction != null;
        if (!_dependenciesValid)
            Debug.LogError($"{nameof(PlayerLootSplitNetworkController)} has missing player dependencies.", this);
        ResetRequests();
    }

    public bool TryRequestSplit(int sourceSlotIndex, int amount, int destinationSlotIndex, int expectedInventoryRevision)
    {
        if (!HasInputAuthority || HasRequestInFlight || !CanAct() || amount < 1 ||
            !LootTransferRequest.IsValidInventorySlotAddress(sourceSlotIndex, expectedInventoryRevision) ||
            destinationSlotIndex < 0 || destinationSlotIndex >= PlayerLootReceiver.PhysicalSlotCount ||
            destinationSlotIndex == sourceSlotIndex) return false;

        uint sequence = unchecked(++_sequence);
        if (sequence == 0) sequence = ++_sequence;
        RpcInvokeInfo result = RPC_RequestSplit(sequence, sourceSlotIndex, amount, destinationSlotIndex, expectedInventoryRevision);
        if (!WasAccepted(result, HasStateAuthority)) return false;
        _inFlight = sequence;
        return true;
    }

    private static bool WasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority) =>
        invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
        hasStateAuthority && invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked;

    private bool CanAct() => _dependenciesValid && _character.IsAlive &&
        !PlayerDownedGate.IsDowned(_character) && !PlayerConsumptionGate.IsConsuming(_character) &&
        !PlayerStunGate.IsStunned(_character) &&
        _extraction.State != ExtractionState.Extracted &&
        (_match == null || _match.Phase == NetworkMatchController.MatchPhase.InProgress);

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, InvokeLocal = true,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private RpcInvokeInfo RPC_RequestSplit(uint sequence, int source, int amount, int destination, int revision,
        RpcInfo info = default)
    {
        if (!HasStateAuthority || info.Source != Object.InputAuthority || sequence == 0) return default;
        // Never overwrite an accepted payload. Its observed revision also prevents a committed replay.
        if (_pendingSequence != 0)
        {
            if (_pendingSequence != sequence)
                RPC_ReceiveSplitConfirmation(sequence, (int)LootTransferFailureReason.ContainerUnavailable);
            return default;
        }
        _pendingSequence = sequence;
        _source = source;
        _amount = amount;
        _destination = destination;
        _revision = revision;
        return default;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _pendingSequence == 0) return;
        uint sequence = _pendingSequence;
        _pendingSequence = 0;
        LootTransferFailureReason reason = LootTransferFailureReason.PlayerUnavailable;
        if (CanAct())
        {
            PlayerReviveGate.InterruptIfReviving(_character);
            reason = _receiver.TrySplitStack(_source, _amount, _destination, _revision);
        }
        RPC_ReceiveSplitConfirmation(sequence, (int)reason);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveSplitConfirmation(uint sequence, int reason)
    {
        if (_inFlight != sequence) return;
        _inFlight = 0;
        _confirmation = Enum.IsDefined(typeof(LootTransferFailureReason), reason)
            ? (LootTransferFailureReason)reason : LootTransferFailureReason.ContainerUnavailable;
        _notify = true;
    }

    public override void Render()
    {
        if (!_notify) return;
        _notify = false;
        SplitConfirmed?.Invoke(_confirmation);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => ResetRequests();

    private void ResetRequests()
    {
        _sequence = 0;
        _inFlight = 0;
        _pendingSequence = 0;
        _notify = false;
    }
}
