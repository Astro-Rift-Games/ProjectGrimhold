using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>
/// Adapts Town Equipment intentions to the application Loadout service. It keeps preparation
/// locking outside persistence and validates the explicit destination selected by presentation.
/// </summary>
public sealed class TownEquipmentMutationEndpoint : ITownEquipmentMutationEndpoint
{
    private readonly IPlayerLoadoutService _loadoutService;
    private readonly ProfileId _profileId;
    private readonly LootDefinitionCatalog _lootCatalog;
    private readonly Func<bool> _canMutate;
    private readonly Action _onMutated;

    public TownEquipmentMutationEndpoint(
        IPlayerLoadoutService loadoutService,
        ProfileId profileId,
        LootDefinitionCatalog lootCatalog,
        Func<bool> canMutate,
        Action onMutated = null)
    {
        _loadoutService = loadoutService ?? throw new ArgumentNullException(nameof(loadoutService));
        _lootCatalog = lootCatalog ?? throw new ArgumentNullException(nameof(lootCatalog));
        _canMutate = canMutate ?? throw new ArgumentNullException(nameof(canMutate));
        _onMutated = onMutated;
        if (!profileId.IsValid)
        {
            throw new ArgumentException("The mutated profile must be valid.", nameof(profileId));
        }

        _profileId = profileId;
    }

    public delegate Task<(bool success, TrainingWeaponResult data, BackendError error)> TrainingWeaponBackend(TrainingWeaponRequest request);
    private TrainingWeaponBackend _trainingBackend;
    private LocalProfileStore _trainingStore;
    private Func<Task<bool>> _reconcile;
    public bool IsTrainingWeaponRequestInFlight { get; private set; }

    public void BindTrainingBackend(LocalProfileStore store, TrainingWeaponBackend backend, Func<Task<bool>> reconcile = null)
    {
        _trainingStore = store;
        _trainingBackend = backend;
        _reconcile = reconcile;
    }

    public bool CanMutate => !IsTrainingWeaponRequestInFlight && _canMutate();

    public bool CanEquip(LootId lootId, EquipmentSlot slot)
    {
        if (!CanMutate || !TryResolveDefinition(lootId, out LootDefinition definition) ||
            !EquipmentSlotRules.IsCompatible(definition, slot))
        {
            return false;
        }

        IReadOnlyList<StashItem> loadout = _loadoutService.GetLoadout(_profileId);
        for (int index = 0; loadout != null && index < loadout.Count; index++)
        {
            if (loadout[index].LootId == lootId && loadout[index].Amount > 0)
            {
                return true;
            }
        }

        return false;
    }

    public StashOperationResult TryEquip(LootId lootId, EquipmentSlot slot)
    {
        if (!CanEquip(lootId, slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        StashOperationResult result = _loadoutService.TryAssignPreparedEquipment(_profileId, slot, lootId);
        if (result == StashOperationResult.Success)
        {
            _onMutated?.Invoke();
        }
        return result;
    }

    public StashOperationResult TryUnequip(EquipmentSlot slot)
    {
        if (!CanMutate || !EquipmentSlotRules.IsEquipmentSlot(slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        StashOperationResult result = _loadoutService.TryClearPreparedEquipment(_profileId, slot);
        if (result == StashOperationResult.Success)
        {
            _onMutated?.Invoke();
        }
        return result;
    }

    public async Task<StashOperationResult> RequestTrainingWeaponAsync(LootId trainingWeaponId, EquipmentSlot targetSlot = EquipmentSlot.None)
    {
        if (!CanMutate || !TryResolveDefinition(trainingWeaponId, out var definition) || !definition.IsTrainingWeapon ||
            (targetSlot != EquipmentSlot.None && targetSlot != EquipmentSlot.WeaponSetAMainHand && targetSlot != EquipmentSlot.WeaponSetBMainHand))
            return StashOperationResult.InvalidInventory;
        if (_trainingStore == null || _trainingBackend == null || !_trainingStore.IsAvailable)
            return StashOperationResult.PersistenceFailed;
        if (_trainingStore.ProfileId != _profileId || _trainingStore.RequiresEquipmentHydration)
            return StashOperationResult.InvalidInventory;
        if (_trainingStore.PendingReservation != null) return StashOperationResult.ReservationPending;

        IsTrainingWeaponRequestInFlight = true;
        try
        {
            int revision = _trainingStore.RemoteRevision;
            var response = await _trainingBackend(new TrainingWeaponRequest
            {
                weaponId = trainingWeaponId.Value, expectedRevision = revision,
                destinationSlot = targetSlot == EquipmentSlot.WeaponSetAMainHand ? "weaponSlot1" :
                    targetSlot == EquipmentSlot.WeaponSetBMainHand ? "weaponSlot2" : ""
            });
            if (!response.success)
            {
                // A stale revision or unknown outcome needs authoritative reconciliation, not a POST replay.
                // Keep the busy gate held until the refresh finishes.
                if (response.error.error == "REVISION_CONFLICT" || BackendErrorUtility.IsTransportFailure(response.error.error) ||
                    string.IsNullOrEmpty(response.error.error) || response.error.error == "INVALID_INVENTORY")
                    await ReconcileSafelyAsync();
                if (response.error.error == BackendErrorUtility.Unauthorized)
                    ApplicationAuthContext.Instance?.HandleUnauthorizedSession();
                return response.error.error switch
                {
                    "RESERVATION_PENDING" => StashOperationResult.ReservationPending,
                    "INVENTORY_FULL" => StashOperationResult.InventoryFull,
                    "REVISION_CONFLICT" or "VALIDATION_FAILED" or "INVENTORY_MIGRATION_REQUIRED" => StashOperationResult.InvalidInventory,
                    _ => StashOperationResult.PersistenceFailed
                };
            }
            var result = _trainingStore.ApplyConfirmedTrainingWeapon(response.data, revision);
            if (result == StashOperationResult.Success) _onMutated?.Invoke();
            else await ReconcileSafelyAsync();
            return result;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"[TownEquipmentMutationEndpoint] No se pudo confirmar el arma: {exception.Message}");
            await ReconcileSafelyAsync();
            return StashOperationResult.PersistenceFailed;
        }
        finally { IsTrainingWeaponRequestInFlight = false; }
    }

    private async Task ReconcileSafelyAsync()
    {
        try
        {
            if (_reconcile == null || !await _reconcile())
                UnityEngine.Debug.LogWarning("[TownEquipmentMutationEndpoint] No se pudo actualizar el inventario autoritativo.");
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"[TownEquipmentMutationEndpoint] Falló la actualización autoritativa: {exception.Message}");
        }
    }

    private bool TryResolveDefinition(LootId lootId, out LootDefinition definition)
    {
        definition = null;
        return lootId.IsValid && _lootCatalog.TryGet(lootId.Value, out definition) &&
            definition != null && EquipmentSlotRules.IsEquippableCategory(definition.Category) &&
            definition.TryValidate(out _);
    }
}
