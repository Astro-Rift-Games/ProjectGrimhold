using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grimhold.Backend;

/// <summary>Screen-local irreversible intention; the shared queue and Store own confirmed persistence.</summary>
public sealed class TownGemInstallSession : IDisposable
{
    private readonly LocalProfileStore _store;
    private readonly StashTransferCoordinator _coordinator;
    private readonly LootDefinitionCatalog _catalog;
    private InventoryList _gemList;
    private int _gemSlot;
    private int _revision;
    private bool _disposed;
    public event Action Changed;
    public bool IsSelecting { get; private set; }
    public bool IsConfirming { get; private set; }
    private bool _isPending;
    public bool IsPending
    {
        get => _isPending || _coordinator?.HasGemInstallPending == true;
        private set => _isPending = value;
    }
    public bool LocksMutations => IsSelecting || IsConfirming || IsPending;
    private string _message;
    public string Message
    {
        get => IsPending ? "Incrustando gema…" : _message;
        private set => _message = value;
    }
    public string Confirmation { get; private set; }
    public InstallGemRequest Request { get; private set; }

    public TownGemInstallSession(LocalProfileStore store, StashTransferCoordinator coordinator, LootDefinitionCatalog catalog)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _coordinator = coordinator;
        _catalog = catalog;
        _store.ProfileCommitted += OnCommitted;
        if (_coordinator != null) _coordinator.GemInstallPendingChanged += OnPendingChanged;
    }

    public bool Begin(InventoryList list, int slot)
    {
        if (_disposed || IsPending) return false;
        if (!Available()) return false;
        StashItem gem = Read(list, slot);
        if (!gem.IsValid || !Resolve(gem.LootId, out var definition) || definition.GemDefinition == null)
            return Reject("GEM_NOT_AVAILABLE");
        _gemList = list;
        _gemSlot = slot;
        _revision = _store.RemoteRevision;
        IsSelecting = true;
        IsConfirming = false;
        Message = "Selecciona un arma para incrustar la gema";
        Changed?.Invoke();
        return true;
    }

    public bool CanTarget(InventoryList list, int slot)
    {
        if (!IsSelecting || _store.RemoteRevision != _revision || !IsWeapon(list, slot, out var weapon)) return false;
        StashItem gem = Read(_gemList, _gemSlot);
        return Resolve(gem.LootId, out var definition) && weapon.Instance.Modifiers.GetValueOrDefault()
            .TryInstallGem(definition, out _, out _);
    }

    public bool Choose(InventoryList list, int slot)
    {
        if (!IsSelecting || IsPending) return false;
        if (!Available()) return false;
        if (_store.RemoteRevision != _revision) return Reject("REVISION_CONFLICT");
        if (!IsWeapon(list, slot, out var weapon)) return false;
        var gem = Read(_gemList, _gemSlot);
        Resolve(gem.LootId, out var gemDefinition);
        if (!weapon.Instance.Modifiers.GetValueOrDefault().TryInstallGem(gemDefinition, out _, out var failure))
            return Reject(failure == WeaponGemInstallFailure.NotAGem ? "GEM_NOT_AVAILABLE" : "GEM_NO_EMPTY_SOCKET");
        Resolve(weapon.LootId, out var weaponDefinition);
        Request = new InstallGemRequest
        {
            transactionId = Guid.NewGuid().ToString("N"), weaponList = list, weaponSlot = slot,
            instanceId = weapon.Instance.InstanceId, gemList = _gemList, gemSlot = _gemSlot,
            gemLootId = gem.LootId.Value, expectedRevision = _revision
        };
        Confirmation = $"¿Incrustar {gemDefinition.DisplayName} en {weaponDefinition.DisplayName}? Es permanente.";
        IsSelecting = false;
        IsConfirming = true;
        Message = null;
        Changed?.Invoke();
        return true;
    }

    /// <summary>True consumes gem-on-weapon gestures even when rejected; all other drops fall through.</summary>
    public bool Drop(InventoryList gemList, int gemSlot, InventoryList weaponList, int weaponSlot)
    {
        var gem = Read(gemList, gemSlot);
        var weapon = Read(weaponList, weaponSlot);
        if (!Resolve(gem.LootId, out var definition) || definition.GemDefinition == null ||
            !Resolve(weapon.LootId, out var target) || target.Category != LootCategory.Weapon) return false;
        if (LocksMutations) return true;
        if (Begin(gemList, gemSlot)) Choose(weaponList, weaponSlot);
        return true;
    }

    public void Cancel()
    {
        if (IsPending) return;
        IsSelecting = IsConfirming = false;
        Message = Confirmation = null;
        Request = default;
        Changed?.Invoke();
    }

    public async Task ConfirmAsync()
    {
        if (_disposed || !IsConfirming || IsPending) return;
        IsPending = true;
        IsConfirming = false;
        Message = "Incrustando gema…";
        Changed?.Invoke();
        try
        {
            var result = _coordinator != null
                ? await _coordinator.InstallGemAsync(Request, _store) : StashTransferOutcome.Unavailable();
            Message = result.IsSuccess ? "Gema incrustada" : FailureMessage(result.RemoteErrorCode);
        }
        finally
        {
            IsPending = IsConfirming = IsSelecting = false;
            if (!_disposed) Changed?.Invoke();
        }
    }

    private bool Available()
    {
        if (!_store.IsAvailable || _store.InventorySchemaVersion != 3 || _store.RequiresEquipmentHydration)
            return Reject("INVENTORY_MIGRATION_REQUIRED");
        return _store.PendingReservation == null || Reject("RESERVATION_PENDING");
    }
    private bool IsWeapon(InventoryList list, int slot, out StashItem item)
    {
        item = Read(list, slot);
        return item.IsValid && item.Amount == 1 && item.Instance?.HasIdentity == true &&
            Resolve(item.LootId, out var definition) && definition.Category == LootCategory.Weapon && definition.WeaponDefinition != null;
    }
    private bool Resolve(LootId id, out LootDefinition definition)
    {
        definition = null;
        return id.IsValid && _catalog != null && _catalog.TryGet(id.Value, out definition);
    }
    private StashItem Read(InventoryList list, int slot)
    {
        IReadOnlyList<StashItem> items = list == InventoryList.Stash ? _store.GetStash() :
            list == InventoryList.Loadout ? _store.GetLoadout() : null;
        if (items != null) foreach (var item in items) if (item.Slot == slot) return item;
        return default;
    }
    private bool Reject(string code)
    {
        IsSelecting = IsConfirming = false;
        Message = FailureMessage(code);
        Changed?.Invoke();
        return false;
    }
    private void OnPendingChanged()
    {
        if (!_isPending && _coordinator.HasGemInstallPending)
        {
            IsSelecting = IsConfirming = false;
            Request = default;
            Confirmation = null;
        }
        Changed?.Invoke();
    }
    private void OnCommitted(ProfileId profile)
    {
        if (profile == _store.ProfileId && !IsPending && (IsSelecting || IsConfirming)) Reject("REVISION_CONFLICT");
    }
    public static string FailureMessage(string code) => code switch
    {
        "INSTANCE_MISMATCH" => "El arma ya no está disponible",
        "GEM_NOT_AVAILABLE" => "La gema ya no está disponible",
        "GEM_NO_SOCKETS" or "GEM_NO_EMPTY_SOCKET" => "Esta arma no tiene ranuras libres",
        "REVISION_CONFLICT" => "El inventario cambió. Vuelve a intentarlo",
        "RESERVATION_PENDING" => "No se puede modificar el inventario mientras hay una reserva de Raid pendiente",
        "INVENTORY_MIGRATION_REQUIRED" => "El inventario debe sincronizarse antes de incrustar gemas",
        _ => "No se pudo incrustar la gema"
    };
    public void Dispose()
    {
        _disposed = true;
        _store.ProfileCommitted -= OnCommitted;
        if (_coordinator != null) _coordinator.GemInstallPendingChanged -= OnPendingChanged;
        Changed = null;
    }
}
