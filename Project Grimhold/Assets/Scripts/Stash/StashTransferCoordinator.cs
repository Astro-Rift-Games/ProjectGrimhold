using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Runs Town inventory actions (transfers, split, Take All / Leave All, equip and unequip) through one
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
    private int _pendingGemInstalls;
    public bool HasGemInstallPending => _pendingGemInstalls > 0;
    public event Action GemInstallPendingChanged;

    /// <param name="remote">Backend operations; null when none is available, which makes transfers unavailable.</param>
    /// <param name="reconciliation">Reconciles the local profile after a remote failure; may be null.</param>
    /// <param name="catalog">Stash-screen ordering metadata. Null preserves legacy source order for existing callers.</param>
    public StashTransferCoordinator(
        ProfileId profileId,
        IPlayerStashService stash,
        IPlayerLoadoutService loadout,
        IRemoteInventoryService remote,
        IProfileReconciliation reconciliation,
        ITownEquipmentMutationEndpoint equipment = null,
        LootDefinitionCatalog catalog = null)
    {
        _profileId = profileId;
        _stash = stash ?? throw new ArgumentNullException(nameof(stash));
        _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        _equipment = equipment;
        _remote = remote;
        _reconciliation = reconciliation;
        _catalog = catalog;
    }

    public Task<StashTransferOutcome> TransferAsync(LootId lootId, bool fromStash, LootTransferQuantityMode mode) =>
        RunExclusiveAsync(() => TransferCoreAsync(ResolveSourceSlot(lootId, fromStash), lootId, fromStash, mode));

    public Task<StashTransferOutcome> TransferAsync(int sourceSlot, LootId lootId, bool fromStash, LootTransferQuantityMode mode) =>
        RunExclusiveAsync(() => TransferCoreAsync(sourceSlot, lootId, fromStash, mode));

    public Task<StashTransferOutcome> OrderAsync(InventoryList list, IReadOnlyList<int> slotOrder) =>
        RunExclusiveAsync(() => OrderCoreAsync(list, slotOrder));

    public Task<StashTransferOutcome> MoveAsync(InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id) =>
        RunExclusiveAsync(async () =>
        {
            if (!(_loadout is ISlotPlayerLoadoutService local) || !(_remote is ISlotRemoteInventoryService remote))
                return StashTransferOutcome.Unavailable();
            var rule = local.EvaluateMove(_profileId, from, fromSlot, to, toSlot, id);
            if (rule != StashOperationResult.Success) return StashTransferOutcome.Rejected(rule);
            var outcome = await ExecuteRemoteAsync(() => remote.MoveSlotsAsync(from, fromSlot, to, toSlot, id));
            return outcome.IsSuccess ? await ApplyLocalAsync(local.TryMove(_profileId, from, fromSlot, to, toSlot, id)) : outcome;
        });

    /// <summary>
    /// Splits one addressed stack through the shared queue: local validation, remote confirmation,
    /// then local commit. Failure or an unknown result reconciles once and never retries.
    /// </summary>
    public Task<StashTransferOutcome> SplitAsync(InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId) =>
        RunExclusiveAsync(async () =>
        {
            if (!(_loadout is ISlotPlayerLoadoutService local) || !(_remote is ISlotRemoteInventoryService remote))
                return StashTransferOutcome.Unavailable();
            var rule = local.EvaluateSplit(_profileId, list, sourceSlot, amount, destinationSlot, lootId);
            if (rule != StashOperationResult.Success) return StashTransferOutcome.Rejected(rule);
            var outcome = await ExecuteRemoteAsync(() => remote.SplitSlotAsync(list, sourceSlot, amount, destinationSlot, lootId));
            return outcome.IsSuccess
                ? await ApplyLocalAsync(local.TrySplit(_profileId, list, sourceSlot, amount, destinationSlot, lootId))
                : outcome;
        });

    /// <summary>Read-only admission check for the Town split dialog, using the same local rules as commit.</summary>
    public StashOperationResult EvaluateSplit(InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId) =>
        _loadout is ISlotPlayerLoadoutService local && SupportsSlots(local.InventorySchemaVersion) &&
        _remote is ISlotRemoteInventoryService
            ? local.EvaluateSplit(_profileId, list, sourceSlot, amount, destinationSlot, lootId)
            : StashOperationResult.InvalidInventory;

    public Task<StashTransferOutcome> SortAsync(InventoryList list, LootDefinitionCatalog catalog = null) =>
        RunExclusiveAsync(() => SortCoreAsync(list, catalog ?? _catalog));

    private async Task<StashTransferOutcome> SortCoreAsync(InventoryList list, LootDefinitionCatalog catalog)
    {
        if (list != InventoryList.Stash && list != InventoryList.Loadout)
            return StashTransferOutcome.Rejected(StashOperationResult.InvalidInventory);
        IReadOnlyList<StashItem> source = list == InventoryList.Stash
            ? _stash.GetStash(_profileId) : _loadout.GetLoadout(_profileId);
        int[] order = StashDisplayOrder.SortedCopy(source, catalog).Select(item => item.Slot).ToArray();
        return await OrderCoreAsync(list, order);
    }

    private async Task<StashTransferOutcome> OrderCoreAsync(InventoryList list, IReadOnlyList<int> order)
    {
        if (!(_loadout is ISlotPlayerLoadoutService local) || !SupportsSlots(local.InventorySchemaVersion) ||
            !(_remote is ISlotRemoteInventoryService remote)) return StashTransferOutcome.Unavailable();
        var rule = local.EvaluateOrderSlots(_profileId, list, order);
        if (rule != StashOperationResult.Success) return StashTransferOutcome.Rejected(rule);
        if (order.Select((slot, i) => slot == i).All(x => x)) return StashTransferOutcome.Success();
        var outcome = await ExecuteRemoteAsync(() => remote.OrderSlotsAsync(list, order));
        return outcome.IsSuccess ? await ApplyLocalAsync(local.TryOrderSlots(_profileId, list, order)) : outcome;
    }

    public Task<StashTransferOutcome> TakeAllAsync() => RunExclusiveAsync(TakeAllCoreAsync);

    public Task<StashTransferOutcome> LeaveAllAsync() => RunExclusiveAsync(LeaveAllCoreAsync);

    /// <summary>
    /// Equips one unit. The Stash source projects, moves remotely, syncs Equipment, then applies atomically; the
    /// Loadout source goes through the Town Equipment endpoint and its Ready gate.
    /// </summary>
    public Task<StashTransferOutcome> EquipAsync(LootId lootId, EquipmentSlot slot, bool fromStash,
        ITownEquipmentMutationEndpoint equipment = null) =>
        RunExclusiveAsync(() => EquipCoreAsync(lootId, slot, fromStash, equipment ?? _equipment));

    public Task<StashTransferOutcome> UnequipAsync(EquipmentSlot slot, ITownEquipmentMutationEndpoint equipment = null) =>
        RunExclusiveAsync(() => UnequipCoreAsync(slot, equipment ?? _equipment));

    /// <summary>Shares the Town queue; a rejected install never applies a local preview or retries.</summary>
    public async Task<StashTransferOutcome> InstallGemAsync(Grimhold.Backend.InstallGemRequest request, LocalProfileStore store)
    {
        _pendingGemInstalls++;
        try
        {
            GemInstallPendingChanged?.Invoke();
            return await InstallGemCoreAsync(request, store);
        }
        finally
        {
            _pendingGemInstalls--;
            GemInstallPendingChanged?.Invoke();
        }
    }

    private Task<StashTransferOutcome> InstallGemCoreAsync(Grimhold.Backend.InstallGemRequest request, LocalProfileStore store) =>
        RunExclusiveAsync(async () =>
        {
            if (store == null || store.ProfileId != _profileId || !store.IsAvailable ||
                store.InventorySchemaVersion != 3 || store.RequiresEquipmentHydration)
                return StashTransferOutcome.RemoteFailed(null, "INVENTORY_MIGRATION_REQUIRED");
            if (store.PendingReservation != null)
                return StashTransferOutcome.RemoteFailed(null, "RESERVATION_PENDING");
            if (store.RemoteRevision != request.expectedRevision)
                return StashTransferOutcome.RemoteFailed(null, "REVISION_CONFLICT");
            if (!(_remote is IRemoteGemInstallService remote)) return StashTransferOutcome.Unavailable();
            try
            {
                var result = await remote.InstallGemAsync(request);
                if (!result.success) return StashTransferOutcome.RemoteFailed(result.error.message, result.error.error);
                return await ApplyLocalAsync(store.ApplyConfirmedGemInstall(result.data, request.expectedRevision));
            }
            catch (Exception)
            {
                return await ReconcileAsync()
                    ? StashTransferOutcome.RemoteFailed(null, "REMOTE_RESULT_UNKNOWN")
                    : StashTransferOutcome.RecoveryFailed(StashOperationResult.Success, null, "REMOTE_RESULT_UNKNOWN");
            }
        });

    private static bool SupportsSlots(int schema) => schema == 2 || schema == 3;

    private async Task<StashTransferOutcome> RunExclusiveAsync(Func<Task<StashTransferOutcome>> operation)
    {
        await _queue.WaitAsync();
        try
        {
            // Reads remain available on legacy inventories, but no Town mutation may precede migration.
            if (!(_loadout is ISlotPlayerLoadoutService slots) || !SupportsSlots(slots.InventorySchemaVersion))
                return StashTransferOutcome.Unavailable();
            return await operation();
        }
        finally
        {
            _queue.Release();
        }
    }

    private async Task<StashTransferOutcome> TransferCoreAsync(
        int sourceSlot,
        LootId lootId,
        bool fromStash,
        LootTransferQuantityMode mode)
    {
        if (!(_remote is ISlotRemoteInventoryService remoteSlots) ||
            !(_loadout is ISlotPlayerLoadoutService local) || !SupportsSlots(local.InventorySchemaVersion))
        {
            return StashTransferOutcome.Unavailable();
        }

        int amount = ResolveAmount(sourceSlot, lootId, fromStash, mode);
        StashOperationResult rule = local.EvaluateTransfer(_profileId, sourceSlot, lootId, amount, fromStash);
        if (rule != StashOperationResult.Success)
        {
            return StashTransferOutcome.Rejected(rule);
        }

        StashTransferOutcome remote = await ExecuteRemoteAsync(() => remoteSlots.TransferAsync(sourceSlot, lootId, amount, fromStash));
        if (!remote.IsSuccess)
        {
            return remote;
        }

        return await ApplyLocalAsync(local.TryTransfer(_profileId, sourceSlot, lootId, amount, fromStash));
    }

    private Task<StashTransferOutcome> TakeAllCoreAsync() => TransferAllCoreAsync(fromStash: true);

    private Task<StashTransferOutcome> LeaveAllCoreAsync() => TransferAllCoreAsync(fromStash: false);

    /// <summary>
    /// Take All / Leave All is one backend request that applies the single-transfer rules to every source
    /// stack in slot order, skips refused stacks and persists the rest in one write. On success the local
    /// profile takes the returned lists in one commit; counts come from the per-stack results. A failed or
    /// undetermined request leaves the local profile unchanged, reconciles once and never retries.
    /// </summary>
    private async Task<StashTransferOutcome> TransferAllCoreAsync(bool fromStash)
    {
        if (!(_remote is IRemoteBulkTransferService bulk) || !(_loadout is ISlotPlayerLoadoutService local))
        {
            return StashTransferOutcome.Unavailable();
        }

        int sourceCount = (fromStash ? _stash.GetStash(_profileId) : _loadout.GetLoadout(_profileId)).Count;
        if (sourceCount == 0)
        {
            return StashTransferOutcome.Success();
        }

        bool success;
        Grimhold.Backend.TransferAllResult data;
        Grimhold.Backend.BackendError error;
        try
        {
            (success, data, error) = await bulk.TransferAllAsync(fromStash);
        }
        catch (Exception)
        {
            // An exception cannot establish whether the server committed. Reconcile, never retry.
            return await TransferAllFailedAsync(sourceCount, "Remote result unavailable", "REMOTE_RESULT_UNKNOWN");
        }

        if (!success)
        {
            return await TransferAllFailedAsync(sourceCount, error.message, error.error);
        }

        Grimhold.Backend.TransferAllStackResult[] results =
            data.results ?? Array.Empty<Grimhold.Backend.TransferAllStackResult>();
        int completed = results.Count(result => result.moved);
        int failed = results.Length - completed;
        if (completed > 0)
        {
            StashTransferOutcome applied = await ApplyLocalAsync(local.TryApplyConfirmedTransferAll(_profileId, data));
            if (!applied.IsSuccess)
            {
                return applied;
            }
        }

        if (failed == 0)
        {
            return StashTransferOutcome.Success(completed);
        }

        StashOperationResult rule = RuleFor(results.First(result => !result.moved).errorCode);
        return completed == 0
            ? StashTransferOutcome.Rejected(rule).WithCounts(0, failed)
            : StashTransferOutcome.Partial(completed, failed, rule: rule);
    }

    private async Task<StashTransferOutcome> TransferAllFailedAsync(int sourceCount, string message, string errorCode) =>
        await ReconcileAsync()
            ? StashTransferOutcome.RemoteFailed(message, errorCode).WithCounts(0, sourceCount)
            : StashTransferOutcome.RecoveryFailed(StashOperationResult.Success, message, errorCode, 0, sourceCount);

    private static StashOperationResult RuleFor(string errorCode) =>
        errorCode == "INVENTORY_FULL" ? StashOperationResult.InventoryFull : StashOperationResult.InvalidInventory;

    private async Task<StashTransferOutcome> EquipCoreAsync(LootId lootId, EquipmentSlot slot, bool fromStash,
        ITownEquipmentMutationEndpoint equipment)
    {
        if (equipment == null || !equipment.CanMutate)
        {
            return StashTransferOutcome.Blocked();
        }

        if (fromStash)
        {
            if (!(_remote is ISlotRemoteInventoryService remoteSlots)) return StashTransferOutcome.Unavailable();

            StashOperationResult rule = _loadout.EvaluateEquipFromStash(
                _profileId, lootId, slot, out PreparedEquipmentLoadout projected);
            if (rule != StashOperationResult.Success) return StashTransferOutcome.Rejected(rule);

            // Copy ownership, not the aggregate reference/revision: production requests
            // save revision metadata while another endpoint can independently change items.
            StashItem[] stashBefore = _stash.GetStash(_profileId).ToArray();
            StashItem[] loadoutBefore = _loadout.GetLoadout(_profileId).ToArray();
            PreparedEquipmentLoadout equipmentBefore = _loadout.GetPreparedEquipment(_profileId);

            int sourceSlot = ResolveSourceSlot(lootId, true);
            StashTransferOutcome move = await ExecuteRemoteAsync(() => remoteSlots.TransferAsync(sourceSlot, lootId, 1, true));
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

        StashOperationResult local = equipment.TryEquip(lootId, slot);
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
        // Equality includes location: drift must not consume another physical stack.
        return expected.Length == current.Count && expected.All(item => current.Contains(item));
    }

    private async Task<StashTransferOutcome> UnequipCoreAsync(EquipmentSlot slot, ITownEquipmentMutationEndpoint equipment)
    {
        if (equipment == null || !equipment.CanMutate)
        {
            return StashTransferOutcome.Blocked();
        }

        StashOperationResult local = equipment.TryUnequip(slot);
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

    private int ResolveSourceSlot(LootId id, bool fromStash)
    {
        var source = fromStash ? _stash.GetStash(_profileId) : _loadout.GetLoadout(_profileId);
        int index = ProfileInventoryRules.FindIndex(source, id);
        return index < 0 ? -1 : source[index].Slot;
    }

    private int ResolveAmount(int slot, LootId lootId, bool fromStash, LootTransferQuantityMode mode)
    {
        if (mode != LootTransferQuantityMode.FullStack)
        {
            return 1;
        }

        IReadOnlyList<StashItem> source = fromStash ? _stash.GetStash(_profileId) : _loadout.GetLoadout(_profileId);
        foreach (StashItem item in source)
        {
            if (item.Slot == slot && item.LootId == lootId)
            {
                return item.Amount;
            }
        }

        // Not owned: the dry run rejects it, so any positive amount reaches the same answer.
        return 1;
    }
}
