using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Owns exactly two authoritative Raid weapon slots and derives combat exclusively
/// from the replicated active slot.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-9)]
public sealed class PlayerWeaponEquipmentNetworkController : NetworkBehaviour
{
    private enum EquipmentRequestKind : byte
    {
        Equip = 1,
        Unequip = 2
    }

    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    [SerializeField] private PlayerLootReceiver _lootReceiver;
    [SerializeField] private MonoBehaviour _characterSource;
    [SerializeField] private PlayerCombatNetworkController _combatController;
    [SerializeField] private MeleeAttack _meleeAttack;
    [SerializeField] private RangedAttack _rangedAttack;
    [SerializeField] private FusionProjectileSpawner _projectileSpawner;

    [Networked] private int WeaponSlot1CatalogIndexPlusOne { get; set; }
    [Networked] private int WeaponSlot2CatalogIndexPlusOne { get; set; }
    [Networked] private int ActiveWeaponSlotValue { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] public int EquipmentRevision { get; private set; }

    private ICharacter _character;
    private NetworkMatchController _matchController;
    private bool _hasPendingAuthorityRequest;
    private EquipmentRequestKind _pendingRequestKind;
    private int _pendingCatalogIndex;
    private WeaponSlot _pendingSlot;
    private int _pendingRequestSequence;
    private int _nextRequestSequence;
    private int _appliedSlot1 = int.MinValue;
    private int _appliedSlot2 = int.MinValue;
    private int _appliedActiveSlot = int.MinValue;
    private bool _initializedBeforeSpawn;
    private readonly Queue<WeaponEquipResult> _pendingPresentationResults = new();

    /// <summary>
    /// Replicated state may only be read once Fusion has spawned the object. Presentation
    /// components run their own lifecycle callbacks while the prefab is still being instantiated,
    /// so every read-only query reports "no equipment" instead of throwing before that point.
    /// </summary>
    private bool IsEquipmentReadable => Object != null && Object.IsValid;

    public bool HasEquippedWeapon => HasAnyWeapon;
    public bool HasAnyWeapon => IsEquipmentReadable &&
        (WeaponSlot1CatalogIndexPlusOne > 0 || WeaponSlot2CatalogIndexPlusOne > 0);
    public bool HasFreeSlot => IsEquipmentReadable &&
        (WeaponSlot1CatalogIndexPlusOne == 0 || WeaponSlot2CatalogIndexPlusOne == 0);
    public WeaponSlot ActiveWeaponSlot => IsEquipmentReadable && IsValidSlotValue(ActiveWeaponSlotValue)
        ? (WeaponSlot)ActiveWeaponSlotValue
        : WeaponSlot.None;
    public int ObservedEquipmentRevision => IsEquipmentReadable ? EquipmentRevision : 0;
    public bool HasRequestInFlight { get; private set; }

    public event Action<WeaponEquipResult> EquipRequestResolved;

    private void Awake() => CacheDependencies();

    public override void Spawned()
    {
        CacheDependencies();
        if (!ValidateDependencies())
        {
            return;
        }

        _matchController = Runner.GetComponent<NetworkMatchController>();
        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this) && !_initializedBeforeSpawn)
        {
            WeaponSlot1CatalogIndexPlusOne = 0;
            WeaponSlot2CatalogIndexPlusOne = 0;
            ActiveWeaponSlotValue = (int)WeaponSlot.None;
            _combatController.TryClearActiveAttack();
        }

        if (HasStateAuthority)
        {
            ApplyReplicatedActiveWeapon();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (HasReplicatedEquipmentChanged())
        {
            ApplyReplicatedActiveWeapon();
        }

        ProcessWeaponSelectionInput();
        if (!_hasPendingAuthorityRequest)
        {
            return;
        }

        EquipmentRequestKind requestKind = _pendingRequestKind;
        int catalogIndex = _pendingCatalogIndex;
        WeaponSlot slot = _pendingSlot;
        int requestSequence = _pendingRequestSequence;
        _hasPendingAuthorityRequest = false;

        WeaponEquipResult result = requestKind == EquipmentRequestKind.Equip
            ? TryEquipAuthority(catalogIndex)
            : TryUnequipAuthority(slot);
        RPC_ConfirmRequest(requestSequence, (int)result);
    }

    public override void Render()
    {
        while (_pendingPresentationResults.Count > 0)
        {
            EquipRequestResolved?.Invoke(_pendingPresentationResults.Dequeue());
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _pendingPresentationResults.Clear();
        HasRequestInFlight = false;
        _hasPendingAuthorityRequest = false;
    }

    public bool TryRequestEquip(LootId lootId)
    {
        if (!HasInputAuthority || HasRequestInFlight || !HasFreeSlot ||
            _lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int catalogIndex))
        {
            return false;
        }

        return TrySendRequest(EquipmentRequestKind.Equip, catalogIndex, WeaponSlot.None);
    }

    public bool TryRequestUnequip(WeaponSlot slot)
    {
        if (!HasInputAuthority || HasRequestInFlight || !IsWeaponSlot(slot) || !IsSlotOccupied(slot))
        {
            return false;
        }

        return TrySendRequest(EquipmentRequestKind.Unequip, -1, slot);
    }

    public bool IsSlotOccupied(WeaponSlot slot) => GetCatalogIndexPlusOne(slot) > 0;

    public bool TryGetSlotLoot(WeaponSlot slot, out LootEntry entry)
    {
        entry = default;
        int catalogIndex = GetCatalogIndexPlusOne(slot) - 1;
        if (catalogIndex < 0 || _lootCatalog == null ||
            !_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition))
        {
            return false;
        }

        entry = new LootEntry(definition.LootId, 1);
        return true;
    }

    public bool TryGetSlotDefinition(WeaponSlot slot, out LootDefinition definition)
    {
        definition = null;
        int catalogIndex = GetCatalogIndexPlusOne(slot) - 1;
        return catalogIndex >= 0 && _lootCatalog != null &&
            _lootCatalog.TryGetByIndex(catalogIndex, out definition) && definition != null;
    }

    /// <summary>Compatibility query whose result is always the active weapon.</summary>
    public bool TryGetEquippedLoot(out LootEntry entry) => TryGetSlotLoot(ActiveWeaponSlot, out entry);

    /// <summary>Resolves only the active weapon for combat and presentation consumers.</summary>
    public bool TryGetEquippedDefinition(out LootDefinition definition) =>
        TryGetSlotDefinition(ActiveWeaponSlot, out definition);

    /// <summary>
    /// Initializes both slots from compact references into an already initialized admission inventory.
    /// Validation completes before either Inventory or Equipment is mutated.
    /// </summary>
    public bool TryInitializePreparedWeapons(
        IReadOnlyList<LootEntry> reservedLoadout,
        int weaponSlot1EntryIndexPlusOne,
        int weaponSlot2EntryIndexPlusOne,
        out string error)
    {
        error = null;
        if (!HasStateAuthority || reservedLoadout == null || _lootReceiver == null)
        {
            error = "Prepared weapon initialization requires State Authority and loadout dependencies.";
            return false;
        }

        if (!TryResolveAdmissionSlot(reservedLoadout, weaponSlot1EntryIndexPlusOne, out int slot1Catalog, out error) ||
            !TryResolveAdmissionSlot(reservedLoadout, weaponSlot2EntryIndexPlusOne, out int slot2Catalog, out error))
        {
            return false;
        }

        if (slot1Catalog == 0 && slot2Catalog == 0)
        {
            error = "Raid admission requires at least one prepared weapon.";
            return false;
        }

        if (weaponSlot1EntryIndexPlusOne > 0 && weaponSlot1EntryIndexPlusOne == weaponSlot2EntryIndexPlusOne &&
            reservedLoadout[weaponSlot1EntryIndexPlusOne - 1].Amount < 2)
        {
            error = "Both weapon slots reference one unit from the reserved loadout.";
            return false;
        }

        if (!TryValidateInventoryUnit(slot1Catalog, out error) || !TryValidateInventoryUnit(slot2Catalog, out error))
        {
            return false;
        }

        int activeCatalog = slot1Catalog > 0 ? slot1Catalog : slot2Catalog;
        if (!TryResolveValidWeapon(activeCatalog - 1, out _, out AttackConfig activeConfig) ||
            !TryConfigureStrategy(activeConfig, out _))
        {
            error = "The prepared active weapon cannot configure a combat strategy.";
            return false;
        }

        if (!CommitInventoryExtraction(slot1Catalog, out error) ||
            !CommitInventoryExtraction(slot2Catalog, out error))
        {
            throw new InvalidOperationException(error ?? "Validated prepared equipment could not be committed.");
        }

        WeaponSlot1CatalogIndexPlusOne = slot1Catalog;
        WeaponSlot2CatalogIndexPlusOne = slot2Catalog;
        ActiveWeaponSlotValue = slot1Catalog > 0 ? (int)WeaponSlot.Slot1 : (int)WeaponSlot.Slot2;
        EquipmentRevision++;
        _initializedBeforeSpawn = true;
        return true;
    }

    public bool TryMatchesExactEquipment(
        LootEntry? expectedSlot1,
        LootEntry? expectedSlot2,
        out string error)
    {
        error = null;
        if (MatchesSlot(WeaponSlot.Slot1, expectedSlot1) && MatchesSlot(WeaponSlot.Slot2, expectedSlot2))
        {
            return true;
        }

        error = "Weapon Equipment no longer matches the expected snapshot.";
        return false;
    }

    public bool TryClearExactEquipment(
        LootEntry? expectedSlot1,
        LootEntry? expectedSlot2,
        out string error)
    {
        if (!HasStateAuthority)
        {
            error = "Equipment can only be cleared by State Authority.";
            return false;
        }

        if (!TryMatchesExactEquipment(expectedSlot1, expectedSlot2, out error))
        {
            return false;
        }

        WeaponSlot1CatalogIndexPlusOne = 0;
        WeaponSlot2CatalogIndexPlusOne = 0;
        ActiveWeaponSlotValue = (int)WeaponSlot.None;
        EquipmentRevision++;
        ApplyReplicatedActiveWeapon();
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, InvokeLocal = true,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private RpcInvokeInfo RPC_RequestEquipment(
        int requestKind,
        int catalogIndex,
        int slotValue,
        int requestSequence,
        RpcInfo info = default)
    {
        if (!HasStateAuthority || info.Source != Object.InputAuthority)
        {
            return default;
        }

        if (_hasPendingAuthorityRequest || !IsValidRequestKind(requestKind))
        {
            RPC_ConfirmRequest(requestSequence, (int)WeaponEquipResult.InvalidRequest);
            return default;
        }

        _pendingRequestKind = (EquipmentRequestKind)requestKind;
        _pendingCatalogIndex = catalogIndex;
        _pendingSlot = IsValidSlotValue(slotValue) ? (WeaponSlot)slotValue : WeaponSlot.None;
        _pendingRequestSequence = requestSequence;
        _hasPendingAuthorityRequest = true;
        return default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ConfirmRequest(int requestSequence, int resultValue)
    {
        if (requestSequence != _nextRequestSequence)
        {
            return;
        }

        HasRequestInFlight = false;
        _pendingPresentationResults.Enqueue((WeaponEquipResult)resultValue);
    }

    private bool TrySendRequest(EquipmentRequestKind kind, int catalogIndex, WeaponSlot slot)
    {
        int requestSequence = ++_nextRequestSequence;
        RpcInvokeInfo invokeInfo = RPC_RequestEquipment(
            (int)kind,
            catalogIndex,
            (int)slot,
            requestSequence);
        if (!WasAccepted(invokeInfo, HasStateAuthority))
        {
            return false;
        }

        HasRequestInFlight = true;
        return true;
    }

    private WeaponEquipResult TryEquipAuthority(int catalogIndex)
    {
        if (!ValidateDependencies()) return WeaponEquipResult.DependenciesUnavailable;
        if (!CanMutateEquipment()) return WeaponEquipResult.PlayerUnavailable;
        WeaponSlot freeSlot = WeaponSlot1CatalogIndexPlusOne == 0
            ? WeaponSlot.Slot1
            : WeaponSlot2CatalogIndexPlusOne == 0 ? WeaponSlot.Slot2 : WeaponSlot.None;
        if (freeSlot == WeaponSlot.None) return WeaponEquipResult.NoFreeWeaponSlot;

        if (!TryResolveValidWeapon(catalogIndex, out LootDefinition definition, out AttackConfig attackConfig))
        {
            return WeaponEquipResult.InvalidWeapon;
        }

        bool becomesActive = ActiveWeaponSlot == WeaponSlot.None;
        if (becomesActive && !TryConfigureStrategy(attackConfig, out _))
        {
            return WeaponEquipResult.InvalidWeapon;
        }

        LootTransferRequest extraction = CreateInventoryTransfer(definition.LootId);
        if (_lootReceiver.ValidateExtraction(extraction) != LootTransferFailureReason.None)
        {
            return WeaponEquipResult.WeaponNotOwned;
        }

        _lootReceiver.CommitExtraction(extraction);
        SetCatalogIndexPlusOne(freeSlot, catalogIndex + 1);
        if (becomesActive)
        {
            ActiveWeaponSlotValue = (int)freeSlot;
            ApplyReplicatedActiveWeapon();
        }
        else
        {
            CaptureAppliedState();
        }
        EquipmentRevision++;

        return WeaponEquipResult.Succeeded;
    }

    private WeaponEquipResult TryUnequipAuthority(WeaponSlot slot)
    {
        if (!ValidateDependencies()) return WeaponEquipResult.DependenciesUnavailable;
        if (!CanMutateEquipment()) return WeaponEquipResult.PlayerUnavailable;
        if (!TryGetSlotLoot(slot, out LootEntry equipped)) return WeaponEquipResult.EmptyWeaponSlot;

        LootTransferRequest receive = CreateInventoryTransfer(equipped.LootId);
        if (_lootReceiver.ValidateReceive(receive) != LootTransferFailureReason.None)
        {
            return WeaponEquipResult.InventoryFull;
        }

        _lootReceiver.CommitReceive(receive);
        SetCatalogIndexPlusOne(slot, 0);
        if (ActiveWeaponSlot == slot)
        {
            WeaponSlot other = slot == WeaponSlot.Slot1 ? WeaponSlot.Slot2 : WeaponSlot.Slot1;
            ActiveWeaponSlotValue = IsSlotOccupied(other) ? (int)other : (int)WeaponSlot.None;
            ApplyReplicatedActiveWeapon();
        }
        else
        {
            CaptureAppliedState();
        }
        EquipmentRevision++;

        return WeaponEquipResult.Succeeded;
    }

    private void ProcessWeaponSelectionInput()
    {
        if (!GetInput(out PlayerNetworkInput input))
        {
            return;
        }

        NetworkButtons current = input.Buttons;
        bool slot1Pressed = current.WasPressed(PreviousButtons, PlayerInputButton.WeaponSlot1);
        bool slot2Pressed = current.WasPressed(PreviousButtons, PlayerInputButton.WeaponSlot2);
        PreviousButtons = current;

        if (slot1Pressed == slot2Pressed || !CanMutateEquipment())
        {
            return;
        }

        WeaponSlot requested = slot1Pressed ? WeaponSlot.Slot1 : WeaponSlot.Slot2;
        if (requested == ActiveWeaponSlot || !IsSlotOccupied(requested))
        {
            return;
        }

        ActiveWeaponSlotValue = (int)requested;
        EquipmentRevision++;
        ApplyReplicatedActiveWeapon();
    }

    private void ApplyReplicatedActiveWeapon()
    {
        CaptureAppliedState();
        WeaponSlot activeSlot = ActiveWeaponSlot;
        if (!IsSlotOccupied(activeSlot))
        {
            if (HasStateAuthority)
            {
                ActiveWeaponSlotValue = (int)WeaponSlot.None;
                _appliedActiveSlot = ActiveWeaponSlotValue;
                _combatController.TryClearActiveAttack();
            }
            return;
        }

        int catalogIndex = GetCatalogIndexPlusOne(activeSlot) - 1;
        if (!TryResolveValidWeapon(catalogIndex, out _, out AttackConfig attackConfig) ||
            !TryConfigureStrategy(attackConfig, out MonoBehaviour attackSource))
        {
            Debug.LogError($"{nameof(PlayerWeaponEquipmentNetworkController)} could not rebuild active weapon index {catalogIndex}.", this);
            if (HasStateAuthority)
            {
                _combatController.TryClearActiveAttack();
            }
            return;
        }

        if (HasStateAuthority && !_combatController.TrySetActiveAttack(attackSource))
        {
            Debug.LogError($"{nameof(PlayerWeaponEquipmentNetworkController)} could not bind the active weapon strategy.", this);
        }
    }

    private bool TryResolveAdmissionSlot(
        IReadOnlyList<LootEntry> reservedLoadout,
        int entryIndexPlusOne,
        out int catalogIndexPlusOne,
        out string error)
    {
        catalogIndexPlusOne = 0;
        error = null;
        if (entryIndexPlusOne == 0) return true;
        int entryIndex = entryIndexPlusOne - 1;
        if (entryIndex < 0 || entryIndex >= reservedLoadout.Count)
        {
            error = "Prepared weapon reference is outside the reserved loadout.";
            return false;
        }

        LootEntry entry = reservedLoadout[entryIndex];
        if (!_lootCatalog.TryGetIndex(entry.LootId, out int catalogIndex) ||
            !TryResolveValidWeapon(catalogIndex, out _, out _))
        {
            error = $"Prepared weapon '{entry.LootId.Value}' is invalid.";
            return false;
        }

        catalogIndexPlusOne = catalogIndex + 1;
        return true;
    }

    private bool TryValidateInventoryUnit(int catalogIndexPlusOne, out string error)
    {
        error = null;
        if (catalogIndexPlusOne == 0) return true;
        if (!_lootCatalog.TryGetByIndex(catalogIndexPlusOne - 1, out LootDefinition definition))
        {
            error = "Prepared weapon catalog index is invalid.";
            return false;
        }

        LootTransferRequest request = CreateInventoryTransfer(definition.LootId);
        if (_lootReceiver.ValidateExtraction(request) == LootTransferFailureReason.None)
        {
            return true;
        }

        error = $"Reserved loadout does not own prepared weapon '{definition.LootId.Value}'.";
        return false;
    }

    private bool CommitInventoryExtraction(int catalogIndexPlusOne, out string error)
    {
        error = null;
        if (catalogIndexPlusOne == 0) return true;
        _lootCatalog.TryGetByIndex(catalogIndexPlusOne - 1, out LootDefinition definition);
        LootTransferRequest request = CreateInventoryTransfer(definition.LootId);
        if (_lootReceiver.ValidateExtraction(request) != LootTransferFailureReason.None)
        {
            error = "Prepared weapon ownership changed during initialization.";
            return false;
        }

        _lootReceiver.CommitExtraction(request);
        return true;
    }

    private LootTransferRequest CreateInventoryTransfer(LootId lootId) => new(
        _lootReceiver.Id,
        _lootReceiver.Id,
        lootId,
        1,
        Runner != null ? Runner.Tick : 0);

    private bool TryResolveValidWeapon(int catalogIndex, out LootDefinition definition, out AttackConfig attackConfig)
    {
        definition = null;
        attackConfig = null;
        if (_lootCatalog == null || !_lootCatalog.TryGetByIndex(catalogIndex, out definition) ||
            definition.Category != LootCategory.Weapon || definition.WeaponDefinition == null ||
            !definition.WeaponDefinition.TryValidate(out _))
        {
            return false;
        }

        attackConfig = definition.WeaponDefinition.PrimaryAttack;
        return true;
    }

    private bool TryConfigureStrategy(AttackConfig attackConfig, out MonoBehaviour attackSource)
    {
        attackSource = null;
        if (attackConfig is MeleeAttackConfig meleeConfig && _meleeAttack != null &&
            _meleeAttack.TryConfigure(meleeConfig))
        {
            attackSource = _meleeAttack;
            return true;
        }

        if (attackConfig is RangedAttackConfig rangedConfig && _rangedAttack != null &&
            _projectileSpawner != null && _projectileSpawner.TryConfigure(rangedConfig) &&
            _rangedAttack.TryConfigure(rangedConfig))
        {
            attackSource = _rangedAttack;
            return true;
        }

        return false;
    }

    private bool CanMutateEquipment() => _character != null && _character.IsAlive &&
        (_matchController == null || _matchController.Phase == NetworkMatchController.MatchPhase.InProgress);

    private bool MatchesSlot(WeaponSlot slot, LootEntry? expected)
    {
        bool hasCurrent = TryGetSlotLoot(slot, out LootEntry current);
        return expected.HasValue ? hasCurrent && current == expected.Value : !hasCurrent;
    }

    private int GetCatalogIndexPlusOne(WeaponSlot slot) => IsEquipmentReadable
        ? slot switch
        {
            WeaponSlot.Slot1 => WeaponSlot1CatalogIndexPlusOne,
            WeaponSlot.Slot2 => WeaponSlot2CatalogIndexPlusOne,
            _ => 0
        }
        : 0;

    private void SetCatalogIndexPlusOne(WeaponSlot slot, int value)
    {
        if (slot == WeaponSlot.Slot1) WeaponSlot1CatalogIndexPlusOne = value;
        else if (slot == WeaponSlot.Slot2) WeaponSlot2CatalogIndexPlusOne = value;
    }

    private bool HasReplicatedEquipmentChanged() =>
        _appliedSlot1 != WeaponSlot1CatalogIndexPlusOne ||
        _appliedSlot2 != WeaponSlot2CatalogIndexPlusOne ||
        _appliedActiveSlot != ActiveWeaponSlotValue;

    private void CaptureAppliedState()
    {
        _appliedSlot1 = WeaponSlot1CatalogIndexPlusOne;
        _appliedSlot2 = WeaponSlot2CatalogIndexPlusOne;
        _appliedActiveSlot = ActiveWeaponSlotValue;
    }

    private void CacheDependencies()
    {
        _character = _characterSource as ICharacter;
        _character ??= GetComponent<ICharacter>();
    }

    private bool ValidateDependencies()
    {
        if (_lootCatalog != null && _lootReceiver != null && _character != null &&
            _combatController != null && _meleeAttack != null && _rangedAttack != null &&
            _projectileSpawner != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(PlayerWeaponEquipmentNetworkController)} has missing dependencies.", this);
        return false;
    }

    private static bool IsWeaponSlot(WeaponSlot slot) =>
        slot == WeaponSlot.Slot1 || slot == WeaponSlot.Slot2;

    private static bool IsValidSlotValue(int value) =>
        value >= (int)WeaponSlot.None && value <= (int)WeaponSlot.Slot2;

    // The RPC transports the kind as int while the enum is byte-backed, so Enum.IsDefined would
    // reject the boxed value outright. The range check mirrors IsValidSlotValue.
    private static bool IsValidRequestKind(int value) =>
        value == (int)EquipmentRequestKind.Equip || value == (int)EquipmentRequestKind.Unequip;

    private static bool WasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority) =>
        invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
        hasStateAuthority && invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked;
}
