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
    private readonly LootDefinitionCatalog _catalog;

    /// <param name="remote">Backend operations; null when none is available, which makes transfers unavailable.</param>
    /// <param name="reconciliation">Reconciles the local profile after a remote failure; may be null.</param>
    /// <param name="catalog">Stash-screen ordering metadata. Null preserves legacy source order for existing callers.</param>
    public StashTransferCoordinator(
        ProfileId profileId,
        IPlayerStashService stash,
        IPlayerLoadoutService loadout,
        IRemoteInventoryService remote,
        IProfileReconciliation reconciliation,
        ITownEquipmentMutationEndpoint equipment,
        LootDefinitionCatalog catalog = null)
    {
        _profileId = profileId;
        _stash = stash ?? throw new ArgumentNullException(nameof(stash));
        _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        _remote = remote;
        _reconciliation = reconciliation;
        _catalog = catalog;
    }

    public Task<StashTransferOutcome> TransferAsync(LootId lootId, bool fromStash, LootTransferQuantityMode mode) =>
        RunExclusiveAsync(() => TransferCoreAsync(lootId, fromStash, mode));

    public Task<StashTransferOutcome> TakeAllAsync() => RunExclusiveAsync(TakeAllCoreAsync);

    public Task<StashTransferOutcome> LeaveAllAsync() => RunExclusiveAsync(LeaveAllCoreAsync);

    /// <summary>
    /// Equips one unit. The Stash source projects, moves remotely, syncs Equipment, then applies atomically; the
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

        StashTransferOutcome remote = await ExecuteRemoteAsync(() => fromStash
            ? _remote.MoveToLoadoutAsync(lootId, amount)
            : _remote.MoveToStashAsync(lootId, amount));
        if (!remote.IsSuccess)
        {
            return remote;
        }

        StashOperationResult local = fromStash
            ? _loadout.TryTransferToLoadout(_profileId, lootId, amount)
            : _loadout.TryTransferToStash(_profileId, lootId, amount);
        return await ApplyLocalAsync(local);
    }

    private Task<StashTransferOutcome> TakeAllCoreAsync() => TransferEachStackAsync(fromStash: true);

    private Task<StashTransferOutcome> LeaveAllCoreAsync() => TransferEachStackAsync(fromStash: false);

    /// <summary>
    /// Take All / Leave All snapshots all source IDs in display order (legacy Store order without a catalog), including items outside the
    /// visible pool. Each stack is checked, moved remotely and applied locally before the next.
    /// Ordinary failures are skipped, but failed reconciliation stops further mutations.
    /// Counts describe confirmed attempts versus unconfirmed/not-attempted IDs, not actual moves.
    /// </summary>
    private async Task<StashTransferOutcome> TransferEachStackAsync(bool fromStash)
    {
        if (_remote == null)
        {
            return StashTransferOutcome.Unavailable();
        }

        IReadOnlyList<StashItem> source = fromStash ? _stash.GetStash(_profileId) : _loadout.GetLoadout(_profileId);
        // Snapshot once inside the queue, not from a visible page or a cached projection.
        LootId[] order = (_catalog != null ? StashDisplayOrder.SortedCopy(source, _catalog) : source)
            .Select(item => item.LootId)
            .ToArray();
        int completed = 0;
        int failed = 0;
        StashTransferOutcome firstFailure = default;
        for (int index = 0; index < order.Length; index++)
        {
            LootId lootId = order[index];
            // A reconciliation after an earlier failure may have changed the source, so every
            // stack is re-read and checked against the current state.
            if (!IsOwned(lootId, fromStash))
            {
                if (failed++ == 0)
                {
                    firstFailure = StashTransferOutcome.Rejected(StashOperationResult.InvalidInventory);
                }
                continue;
            }

            StashTransferOutcome outcome = await TransferCoreAsync(lootId, fromStash, LootTransferQuantityMode.FullStack);
            if (outcome.IsSuccess)
            {
                completed++;
                continue;
            }

            if (failed++ == 0)
            {
                firstFailure = outcome;
            }

            if (outcome.Status == StashTransferStatus.RecoveryFailed)
            {
                return StashTransferOutcome.RecoveryFailed(
                    outcome.Rule, outcome.RemoteError, outcome.RemoteErrorCode,
                    completed, failed + order.Length - index - 1);
            }
        }

        if (failed == 0)
        {
            return StashTransferOutcome.Success(completed);
        }

        return completed == 0
            ? firstFailure.WithCounts(completed, failed)
            : StashTransferOutcome.Partial(
                completed, failed, firstFailure.RemoteError, firstFailure.RemoteErrorCode, firstFailure.Rule);
    }

    private bool IsOwned(LootId lootId, bool fromStash)
    {
        IReadOnlyList<StashItem> source = fromStash ? _stash.GetStash(_profileId) : _loadout.GetLoadout(_profileId);
        foreach (StashItem item in source)
        {
            if (item.LootId == lootId)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<StashTransferOutcome> EquipCoreAsync(LootId lootId, EquipmentSlot slot, bool fromStash)
    {
        if (!_equipment.CanMutate)
        {
            return StashTransferOutcome.Blocked();
        }

        if (fromStash)
        {
            if (_remote == null) return StashTransferOutcome.Unavailable();

            StashOperationResult rule = _loadout.EvaluateEquipFromStash(
                _profileId, lootId, slot, out PreparedEquipmentLoadout projected);
            if (rule != StashOperationResult.Success) return StashTransferOutcome.Rejected(rule);

            // Copy ownership, not the aggregate reference/revision: production requests
            // save revision metadata while another endpoint can independently change items.
            StashItem[] stashBefore = _stash.GetStash(_profileId).ToArray();
            StashItem[] loadoutBefore = _loadout.GetLoadout(_profileId).ToArray();
            PreparedEquipmentLoadout equipmentBefore = _loadout.GetPreparedEquipment(_profileId);

            StashTransferOutcome move = await ExecuteRemoteAsync(() => _remote.MoveToLoadoutAsync(lootId, 1));
            if (!move.IsSuccess) return move;
            if (!OwnershipMatches(stashBefore, loadoutBefore, equipmentBefore))
                return await ApplyLocalAsync(StashOperationResult.InvalidInventory);

            StashTransferOutcome equip = await ExecuteRemoteAsync(() => _remote.UpdatePreparedEquipmentAsync(projected));
            if (!equip.IsSuccess) return equip;
            if (!OwnershipMatches(stashBefore, loadoutBefore, equipmentBefore))
                return await ApplyLocalAsync(StashOperationResult.InvalidInventory);

            // No await between the final guard and current-state Store revalidation/application.
            return await ApplyLocalAsync(_loadout.TryEquipFromStash(_profileId, lootId, slot));
        }

        StashOperationResult local = _equipment.TryEquip(lootId, slot);
        return local == StashOperationResult.Success
            ? await SyncPreparedEquipmentAsync()
            : StashTransferOutcome.Rejected(local);
    }

    private bool OwnershipMatches(
        StashItem[] stash, StashItem[] loadout, PreparedEquipmentLoadout equipment)
    {
        return equipment.Equals(_loadout.GetPreparedEquipment(_profileId)) &&
            SameItems(stash, _stash.GetStash(_profileId)) &&
            SameItems(loadout, _loadout.GetLoadout(_profileId));
    }

    private static bool SameItems(StashItem[] expected, IReadOnlyList<StashItem> current)
    {
        // Store inventories have one entry per ID; ordering is not ownership.
        return expected.Length == current.Count && expected.All(item => current.Contains(item));
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
        return await ExecuteRemoteAsync(() => _remote.UpdatePreparedEquipmentAsync(prepared));
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

        return await ReconcileAsync()
            ? StashTransferOutcome.Diverged(local)
            : StashTransferOutcome.RecoveryFailed(local);
    }

    private async Task<StashTransferOutcome> ExecuteRemoteAsync(
        Func<Task<(bool success, Grimhold.Backend.BackendError error)>> operation)
    {
        bool success;
        Grimhold.Backend.BackendError error;
        try
        {
            (success, error) = await operation();
        }
        catch (Exception)
        {
            // An exception cannot establish whether the server committed. Reconcile, never retry.
            return await ReconcileAsync()
                ? StashTransferOutcome.RemoteFailed("Remote result unavailable", "REMOTE_RESULT_UNKNOWN")
                : StashTransferOutcome.RecoveryFailed(
                    StashOperationResult.Success, "Remote result unavailable", "REMOTE_RESULT_UNKNOWN");
        }
        if (success)
        {
            return StashTransferOutcome.Success();
        }

        // Never retry: even an error response may follow a committed mutation.
        return await ReconcileAsync()
            ? StashTransferOutcome.RemoteFailed(error.message, error.error)
            : StashTransferOutcome.RecoveryFailed(StashOperationResult.Success, error.message, error.error);
    }

    private async Task<bool> ReconcileAsync()
    {
        if (_reconciliation == null) return false;
        try
        {
            return await _reconciliation.ReconcileAsync();
        }
        catch (Exception)
        {
            return false;
        }
    }

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
