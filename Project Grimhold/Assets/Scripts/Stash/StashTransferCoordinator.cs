using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Runs every Stash screen action (transfers, Take All / Leave All, equip and unequip) through one
/// serialized queue, so an equip never interleaves with a transfer that is waiting for the backend.
/// Each transfer is checked against the local Store rules first, so a transfer the Store would
/// refuse never reaches the backend, then follows the remote-first order: the backend confirms,
/// then the local profile applies the same change. A failed or undetermined remote operation
/// reconciles the local profile with the backend instead of retrying the mutation.
/// </summary>
public sealed class StashTransferCoordinator
{
    private readonly ProfileId _profileId;
    private readonly IPlayerStashService _stash;
    private readonly IPlayerLoadoutService _loadout;
    private readonly IRemoteInventoryService _remote;
    private readonly IProfileReconciliation _reconciliation;
    private readonly ITownEquipmentMutationEndpoint _equipment;
    private readonly SemaphoreSlim _queue = new(1, 1);

    /// <param name="remote">Backend operations; null when none is available, which makes transfers unavailable.</param>
    /// <param name="reconciliation">Reconciles the local profile after a remote failure; may be null.</param>
    public StashTransferCoordinator(
        ProfileId profileId,
        IPlayerStashService stash,
        IPlayerLoadoutService loadout,
        IRemoteInventoryService remote,
        IProfileReconciliation reconciliation,
        ITownEquipmentMutationEndpoint equipment)
    {
        _profileId = profileId;
        _stash = stash ?? throw new ArgumentNullException(nameof(stash));
        _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        _remote = remote;
        _reconciliation = reconciliation;
    }

    public Task<StashTransferOutcome> TransferAsync(LootId lootId, bool fromStash, LootTransferQuantityMode mode) =>
        RunExclusiveAsync(() => TransferCoreAsync(lootId, fromStash, mode));

    public Task<StashTransferOutcome> TakeAllAsync() => RunExclusiveAsync(TakeAllCoreAsync);

    public Task<StashTransferOutcome> LeaveAllAsync() => RunExclusiveAsync(LeaveAllCoreAsync);

    /// <summary>
    /// Equips one unit. The Stash source uses the local atomic Stash-to-Equipment operation; the
    /// Loadout source goes through the Town Equipment endpoint and its Ready gate.
    /// </summary>
    public Task<StashTransferOutcome> EquipAsync(LootId lootId, EquipmentSlot slot, bool fromStash) =>
        RunExclusiveAsync(() => EquipCoreAsync(lootId, slot, fromStash));

    public Task<StashTransferOutcome> UnequipAsync(EquipmentSlot slot) =>
        RunExclusiveAsync(() => UnequipCoreAsync(slot));

    private async Task<StashTransferOutcome> RunExclusiveAsync(Func<Task<StashTransferOutcome>> operation)
    {
        await _queue.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _queue.Release();
        }
    }

    private async Task<StashTransferOutcome> TransferCoreAsync(
        LootId lootId,
        bool fromStash,
        LootTransferQuantityMode mode)
    {
        if (_remote == null)
        {
            return StashTransferOutcome.Unavailable();
        }

        int amount = ResolveAmount(lootId, fromStash, mode);
        StashOperationResult rule = fromStash
            ? _loadout.EvaluateTransferToLoadout(_profileId, lootId, amount)
            : _loadout.EvaluateTransferToStash(_profileId, lootId, amount);
        if (rule != StashOperationResult.Success)
        {
            return StashTransferOutcome.Rejected(rule);
        }

        (bool success, Grimhold.Backend.BackendError error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            () => fromStash
                ? _remote.MoveToLoadoutAsync(lootId, amount)
                : _remote.MoveToStashAsync(lootId, amount),
            ReconcileAsync);
        if (!success)
        {
            return StashTransferOutcome.RemoteFailed(error.message);
        }

        StashOperationResult local = fromStash
            ? _loadout.TryTransferToLoadout(_profileId, lootId, amount)
            : _loadout.TryTransferToStash(_profileId, lootId, amount);
        return await ApplyLocalAsync(local);
    }

    private async Task<StashTransferOutcome> TakeAllCoreAsync()
    {
        if (_remote == null)
        {
            return StashTransferOutcome.Unavailable();
        }

        StashOperationResult rule = _loadout.EvaluateTransferAllToLoadout(_profileId);
        if (rule != StashOperationResult.Success)
        {
            return StashTransferOutcome.Rejected(rule);
        }

        StashItem[] items = _stash.GetStash(_profileId).ToArray();
        foreach (StashItem item in items)
        {
            (bool success, Grimhold.Backend.BackendError error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
                () => _remote.MoveToLoadoutAsync(item.LootId, item.Amount),
                ReconcileAsync);
            if (!success)
            {
                return StashTransferOutcome.RemoteFailed(error.message);
            }
        }

        return await ApplyLocalAsync(_loadout.TryTransferAllToLoadout(_profileId));
    }

    private async Task<StashTransferOutcome> LeaveAllCoreAsync()
    {
        if (_remote == null)
        {
            return StashTransferOutcome.Unavailable();
        }

        StashOperationResult rule = _loadout.EvaluateTransferAllToStash(_profileId);
        if (rule != StashOperationResult.Success)
        {
            return StashTransferOutcome.Rejected(rule);
        }

        StashItem[] items = _loadout.GetLoadout(_profileId).ToArray();
        foreach (StashItem item in items)
        {
            (bool success, Grimhold.Backend.BackendError error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
                () => _remote.MoveToStashAsync(item.LootId, item.Amount),
                ReconcileAsync);
            if (!success)
            {
                return StashTransferOutcome.RemoteFailed(error.message);
            }
        }

        return await ApplyLocalAsync(_loadout.TryTransferAllToStash(_profileId));
    }

    private async Task<StashTransferOutcome> EquipCoreAsync(LootId lootId, EquipmentSlot slot, bool fromStash)
    {
        if (!_equipment.CanMutate)
        {
            return StashTransferOutcome.Blocked();
        }

        StashOperationResult local = fromStash
            ? _loadout.TryEquipFromStash(_profileId, lootId, slot)
            : _equipment.TryEquip(lootId, slot);
        return local == StashOperationResult.Success
            ? await SyncPreparedEquipmentAsync()
            : StashTransferOutcome.Rejected(local);
    }

    private async Task<StashTransferOutcome> UnequipCoreAsync(EquipmentSlot slot)
    {
        if (!_equipment.CanMutate)
        {
            return StashTransferOutcome.Blocked();
        }

        StashOperationResult local = _equipment.TryUnequip(slot);
        return local == StashOperationResult.Success
            ? await SyncPreparedEquipmentAsync()
            : StashTransferOutcome.Rejected(local);
    }

    /// <summary>
    /// Pushes the prepared Equipment the local profile already committed. A failure reconciles the
    /// profile with the backend, which also reverts that local change.
    /// </summary>
    private async Task<StashTransferOutcome> SyncPreparedEquipmentAsync()
    {
        if (_remote == null)
        {
            return StashTransferOutcome.Unavailable();
        }

        PreparedEquipmentLoadout prepared = _loadout.GetPreparedEquipment(_profileId);
        (bool success, Grimhold.Backend.BackendError error) = await RemoteOperationPolicy.ExecuteWithReconciliationAsync(
            () => _remote.UpdatePreparedEquipmentAsync(prepared),
            ReconcileAsync);
        return success ? StashTransferOutcome.Success() : StashTransferOutcome.RemoteFailed(error.message);
    }

    /// <summary>
    /// The backend already confirmed the transfer, so a local refusal means the two sides diverged
    /// and the local profile is rebuilt from the backend.
    /// </summary>
    private async Task<StashTransferOutcome> ApplyLocalAsync(StashOperationResult local)
    {
        if (local == StashOperationResult.Success)
        {
            return StashTransferOutcome.Success();
        }

        await ReconcileAsync();
        return StashTransferOutcome.Diverged(local);
    }

    private Task<bool> ReconcileAsync() =>
        _reconciliation != null ? _reconciliation.ReconcileAsync() : Task.FromResult(false);

    private int ResolveAmount(LootId lootId, bool fromStash, LootTransferQuantityMode mode)
    {
        if (mode != LootTransferQuantityMode.FullStack)
        {
            return 1;
        }

        IReadOnlyList<StashItem> source = fromStash ? _stash.GetStash(_profileId) : _loadout.GetLoadout(_profileId);
        foreach (StashItem item in source)
        {
            if (item.LootId == lootId)
            {
                return item.Amount;
            }
        }

        // Not owned: the dry run rejects it, so any positive amount reaches the same answer.
        return 1;
    }
}
