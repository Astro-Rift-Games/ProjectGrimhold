using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Owns the six authoritative Raid Equipment slots — two weapon quick slots plus Helmet, Armor,
/// Gloves and Boots — and derives combat exclusively from the replicated active weapon slot.
/// Armor slots hold Equipment state only and never reach the combat strategies.
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
    [Networked] private int HelmetCatalogIndexPlusOne { get; set; }
    [Networked] private int ArmorCatalogIndexPlusOne { get; set; }
    [Networked] private int GlovesCatalogIndexPlusOne { get; set; }
    [Networked] private int BootsCatalogIndexPlusOne { get; set; }
    [Networked] private int ActiveWeaponSlotValue { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] public int EquipmentRevision { get; private set; }

    private ICharacter _character;
    private NetworkMatchController _matchController;
    private bool _hasPendingAuthorityRequest;
    private EquipmentRequestKind _pendingRequestKind;
    private int _pendingCatalogIndex;
    private EquipmentSlot _pendingSlot;
    private int _pendingRequestSequence;
    private int _nextRequestSequence;
    private int _appliedSlot1 = int.MinValue;
    private int _appliedSlot2 = int.MinValue;
    private int _appliedActiveSlot = int.MinValue;
    private bool _initializedBeforeSpawn;
    private readonly Queue<EquipmentOperationResult> _pendingPresentationResults = new();

    /// <summary>Every Equipment slot in a stable presentation order, owned by the slot rules.</summary>
    public static EquipmentSlot[] AllSlots => EquipmentSlotRules.AllSlots;

    /// <summary>
    /// Replicated state may only be read once Fusion has spawned the object. Presentation
    /// components run their own lifecycle callbacks while the prefab is still being instantiated,
    /// so every read-only query reports "no equipment" instead of throwing before that point.
    /// </summary>
    private bool IsEquipmentReadable => Object != null && Object.IsValid;

    public bool HasEquippedWeapon => HasAnyWeapon;
    public bool HasAnyWeapon => IsEquipmentReadable &&
        (WeaponSlot1CatalogIndexPlusOne > 0 || WeaponSlot2CatalogIndexPlusOne > 0);

    /// <summary>True while any of the six Equipment slots still owns a unit.</summary>
    public bool HasAnyEquipment
    {
        get
        {
            if (!IsEquipmentReadable)
            {
                return false;
            }

            for (int index = 0; index < AllSlots.Length; index++)
            {
                if (GetCatalogIndexPlusOne(AllSlots[index]) > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public WeaponSlot ActiveWeaponSlot =>
        IsEquipmentReadable && ActiveWeaponSlotValue >= (int)WeaponSlot.None &&
        ActiveWeaponSlotValue <= (int)WeaponSlot.Slot2
            ? (WeaponSlot)ActiveWeaponSlotValue
            : WeaponSlot.None;
    public int ObservedEquipmentRevision => IsEquipmentReadable ? EquipmentRevision : 0;
    public bool HasRequestInFlight { get; private set; }

    public event Action<EquipmentOperationResult> EquipRequestResolved;

    private void Awake() => CacheDependencies();

    public override void Spawned()
    {
        CacheDependencies();
        if (!ValidateEquipmentDependencies() || !ValidateWeaponDependencies())
        {
            return;
        }

        _matchController = Runner.GetComponent<NetworkMatchController>();
        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this) && !_initializedBeforeSpawn)
        {
            for (int index = 0; index < AllSlots.Length; index++)
            {
                SetCatalogIndexPlusOne(AllSlots[index], 0);
            }

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

        if (HasReplicatedWeaponStateChanged())
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
        EquipmentSlot slot = _pendingSlot;
        int requestSequence = _pendingRequestSequence;
        _hasPendingAuthorityRequest = false;

        EquipmentOperationResult result = requestKind == EquipmentRequestKind.Equip
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

    /// <summary>
    /// Reports whether the destination slot this loot would target is currently free.
    /// Mirrors the authoritative slot resolution so the UI does not offer impossible intentions.
    /// </summary>
    public bool CanEquip(LootId lootId) =>
        TryResolveTargetSlot(lootId, out _, out _) != EquipmentSlot.None;

    public bool TryRequestEquip(LootId lootId)
    {
        if (!IsEquipmentReadable || !HasInputAuthority || HasRequestInFlight ||
            TryResolveTargetSlot(lootId, out int catalogIndex, out _) == EquipmentSlot.None)
        {
            return false;
        }

        return TrySendRequest(EquipmentRequestKind.Equip, catalogIndex, EquipmentSlot.None);
    }

    public bool TryRequestUnequip(EquipmentSlot slot)
    {
        if (!IsEquipmentReadable || !HasInputAuthority || HasRequestInFlight ||
            !EquipmentSlotRules.IsEquipmentSlot(slot) || !IsSlotOccupied(slot))
        {
            return false;
        }

        return TrySendRequest(EquipmentRequestKind.Unequip, -1, slot);
    }

    public bool TryRequestUnequip(WeaponSlot slot) =>
        TryRequestUnequip(EquipmentSlotRules.FromWeaponSlot(slot));

    public bool IsSlotOccupied(EquipmentSlot slot) => GetCatalogIndexPlusOne(slot) > 0;

    public bool IsSlotOccupied(WeaponSlot slot) =>
        IsSlotOccupied(EquipmentSlotRules.FromWeaponSlot(slot));

    public bool TryGetSlotLoot(EquipmentSlot slot, out LootEntry entry)
    {
        entry = default;
        if (!TryGetSlotDefinition(slot, out LootDefinition definition))
        {
            return false;
        }

        entry = new LootEntry(definition.LootId, 1);
        return true;
    }

    public bool TryGetSlotLoot(WeaponSlot slot, out LootEntry entry) =>
        TryGetSlotLoot(EquipmentSlotRules.FromWeaponSlot(slot), out entry);

    public bool TryGetSlotDefinition(EquipmentSlot slot, out LootDefinition definition)
    {
        definition = null;
        int catalogIndex = GetCatalogIndexPlusOne(slot) - 1;
        return catalogIndex >= 0 && _lootCatalog != null &&
            _lootCatalog.TryGetByIndex(catalogIndex, out definition) && definition != null;
    }

    public bool TryGetSlotDefinition(WeaponSlot slot, out LootDefinition definition) =>
        TryGetSlotDefinition(EquipmentSlotRules.FromWeaponSlot(slot), out definition);

    /// <summary>Compatibility query whose result is always the active weapon.</summary>
    public bool TryGetEquippedLoot(out LootEntry entry) => TryGetSlotLoot(ActiveWeaponSlot, out entry);

    /// <summary>Resolves only the active weapon for combat and presentation consumers.</summary>
    public bool TryGetEquippedDefinition(out LootDefinition definition) =>
        TryGetSlotDefinition(ActiveWeaponSlot, out definition);

    /// <summary>
    /// Initializes both slots from compact references into an already initialized admission inventory.
    /// Validation completes before either Inventory or Equipment is mutated.
    /// </summary>
    public bool TryInitializePreparedEquipment(
        IReadOnlyList<LootEntry> reservedLoadout,
        IReadOnlyList<int> entryIndicesPlusOne,
        out string error)
    {
        error = null;
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        if (!HasStateAuthority || reservedLoadout == null || _lootReceiver == null || _lootCatalog == null)
        {
            error = "Prepared equipment initialization requires State Authority and loadout dependencies.";
            return false;
        }

        if (entryIndicesPlusOne == null || entryIndicesPlusOne.Count != slots.Length)
        {
            error = "Prepared equipment references are missing.";
            return false;
        }

        var catalogIndices = new int[slots.Length];
        for (int index = 0; index < slots.Length; index++)
        {
            if (!TryResolveAdmissionSlot(
                    reservedLoadout,
                    entryIndicesPlusOne[index],
                    slots[index],
                    out catalogIndices[index],
                    out error))
            {
                return false;
            }
        }

        int slot1Catalog = catalogIndices[0];
        int slot2Catalog = catalogIndices[1];
        if (slot1Catalog == 0 && slot2Catalog == 0)
        {
            error = "Raid admission requires at least one prepared weapon.";
            return false;
        }

        if (!RaidLoadoutRules.TryValidatePreparedEquipmentReferences(
                reservedLoadout,
                entryIndicesPlusOne,
                requireWeapon: true,
                out error))
        {
            return false;
        }

        for (int index = 0; index < catalogIndices.Length; index++)
        {
            if (!TryValidateInventoryUnit(catalogIndices[index], out error))
            {
                return false;
            }
        }

        int activeCatalog = slot1Catalog > 0 ? slot1Catalog : slot2Catalog;
        if (!TryResolveValidWeapon(activeCatalog - 1, out _, out AttackConfig activeConfig) ||
            !TryConfigureStrategy(activeConfig, out _))
        {
            error = "The prepared active weapon cannot configure a combat strategy.";
            return false;
        }

        for (int index = 0; index < catalogIndices.Length; index++)
        {
            if (!CommitInventoryExtraction(catalogIndices[index], out error))
            {
                throw new InvalidOperationException(error ?? "Validated prepared equipment could not be committed.");
            }
        }

        for (int index = 0; index < slots.Length; index++)
        {
            SetCatalogIndexPlusOne(slots[index], catalogIndices[index]);
        }

        ActiveWeaponSlotValue = slot1Catalog > 0 ? (int)WeaponSlot.Slot1 : (int)WeaponSlot.Slot2;
        EquipmentRevision++;
        _initializedBeforeSpawn = true;
        return true;
    }

    /// <summary>
    /// Verifies every Equipment slot against the expected expedition ownership snapshot.
    /// Parameters are named per slot so callers cannot mismatch positional entries.
    /// </summary>
    public bool TryMatchesExactEquipment(
        LootEntry? expectedWeaponSlot1,
        LootEntry? expectedWeaponSlot2,
        LootEntry? expectedHelmet,
        LootEntry? expectedArmor,
        LootEntry? expectedGloves,
        LootEntry? expectedBoots,
        out string error)
    {
        error = null;
        if (MatchesSlot(EquipmentSlot.WeaponSlot1, expectedWeaponSlot1) &&
            MatchesSlot(EquipmentSlot.WeaponSlot2, expectedWeaponSlot2) &&
            MatchesSlot(EquipmentSlot.Helmet, expectedHelmet) &&
            MatchesSlot(EquipmentSlot.Armor, expectedArmor) &&
            MatchesSlot(EquipmentSlot.Gloves, expectedGloves) &&
            MatchesSlot(EquipmentSlot.Boots, expectedBoots))
        {
            return true;
        }

        error = "Equipment no longer matches the expected snapshot.";
        return false;
    }

    public bool TryClearExactEquipment(
        LootEntry? expectedWeaponSlot1,
        LootEntry? expectedWeaponSlot2,
        LootEntry? expectedHelmet,
        LootEntry? expectedArmor,
        LootEntry? expectedGloves,
        LootEntry? expectedBoots,
        out string error)
    {
        if (!HasStateAuthority)
        {
            error = "Equipment can only be cleared by State Authority.";
            return false;
        }

        if (!TryMatchesExactEquipment(
                expectedWeaponSlot1, expectedWeaponSlot2, expectedHelmet,
                expectedArmor, expectedGloves, expectedBoots, out error))
        {
            return false;
        }

        for (int index = 0; index < AllSlots.Length; index++)
        {
            SetCatalogIndexPlusOne(AllSlots[index], 0);
        }

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
            RPC_ConfirmRequest(requestSequence, (int)EquipmentOperationResult.InvalidRequest);
            return default;
        }

        _pendingRequestKind = (EquipmentRequestKind)requestKind;
        _pendingCatalogIndex = catalogIndex;
        _pendingSlot = EquipmentSlotRules.IsValidSlotValue(slotValue)
            ? (EquipmentSlot)slotValue
            : EquipmentSlot.None;
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
        _pendingPresentationResults.Enqueue((EquipmentOperationResult)resultValue);
    }

    private bool TrySendRequest(EquipmentRequestKind kind, int catalogIndex, EquipmentSlot slot)
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

    private EquipmentOperationResult TryEquipAuthority(int catalogIndex)
    {
        if (!ValidateEquipmentDependencies()) return EquipmentOperationResult.DependenciesUnavailable;
        if (!CanMutateEquipment()) return EquipmentOperationResult.PlayerUnavailable;

        if (!_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition) || definition == null ||
            !EquipmentSlotRules.IsEquippableCategory(definition.Category))
        {
            return EquipmentOperationResult.InvalidEquipment;
        }

        EquipmentSlot targetSlot = ResolveFreeTargetSlot(definition.Category);
        if (targetSlot == EquipmentSlot.None)
        {
            return definition.Category == LootCategory.Weapon
                ? EquipmentOperationResult.NoFreeWeaponSlot
                : EquipmentOperationResult.SlotOccupied;
        }

        // Only weapons reach the combat strategies. Armor never depends on them, so their
        // dependencies are validated exclusively on this branch.
        bool becomesActive = false;
        if (EquipmentSlotRules.IsWeaponSlot(targetSlot))
        {
            if (!ValidateWeaponDependencies()) return EquipmentOperationResult.DependenciesUnavailable;
            if (!TryResolveValidWeapon(catalogIndex, out _, out AttackConfig attackConfig))
            {
                return EquipmentOperationResult.InvalidEquipment;
            }

            becomesActive = ActiveWeaponSlot == WeaponSlot.None;
            if (becomesActive && !TryConfigureStrategy(attackConfig, out _))
            {
                return EquipmentOperationResult.InvalidEquipment;
            }
        }

        LootTransferRequest extraction = CreateInventoryTransfer(definition.LootId);
        if (_lootReceiver.ValidateExtraction(extraction) != LootTransferFailureReason.None)
        {
            return EquipmentOperationResult.ItemNotOwned;
        }

        _lootReceiver.CommitExtraction(extraction);
        SetCatalogIndexPlusOne(targetSlot, catalogIndex + 1);
        EquipmentRevision++;
        if (becomesActive)
        {
            ActiveWeaponSlotValue = (int)EquipmentSlotRules.ToWeaponSlot(targetSlot);
            ApplyReplicatedActiveWeapon();
        }
        else
        {
            CaptureAppliedState();
        }

        return EquipmentOperationResult.Succeeded;
    }

    private EquipmentOperationResult TryUnequipAuthority(EquipmentSlot slot)
    {
        if (!ValidateEquipmentDependencies()) return EquipmentOperationResult.DependenciesUnavailable;
        if (!CanMutateEquipment()) return EquipmentOperationResult.PlayerUnavailable;
        if (!EquipmentSlotRules.IsEquipmentSlot(slot)) return EquipmentOperationResult.InvalidRequest;
        if (!TryGetSlotLoot(slot, out LootEntry equipped)) return EquipmentOperationResult.EmptySlot;

        LootTransferRequest receive = CreateInventoryTransfer(equipped.LootId);
        if (_lootReceiver.ValidateReceive(receive) != LootTransferFailureReason.None)
        {
            return EquipmentOperationResult.InventoryFull;
        }

        _lootReceiver.CommitReceive(receive);
        SetCatalogIndexPlusOne(slot, 0);
        EquipmentRevision++;
        if (EquipmentSlotRules.IsWeaponSlot(slot) &&
            ActiveWeaponSlot == EquipmentSlotRules.ToWeaponSlot(slot))
        {
            WeaponSlot other = slot == EquipmentSlot.WeaponSlot1 ? WeaponSlot.Slot2 : WeaponSlot.Slot1;
            ActiveWeaponSlotValue = IsSlotOccupied(other) ? (int)other : (int)WeaponSlot.None;
            ApplyReplicatedActiveWeapon();
        }
        else
        {
            CaptureAppliedState();
        }

        return EquipmentOperationResult.Succeeded;
    }

    /// <summary>
    /// Resolves the slot this loot would occupy, or <see cref="EquipmentSlot.None"/> when the
    /// category is not equippable or its destination is already taken.
    /// </summary>
    private EquipmentSlot ResolveFreeTargetSlot(LootCategory category)
    {
        if (category == LootCategory.Weapon)
        {
            return !IsSlotOccupied(EquipmentSlot.WeaponSlot1)
                ? EquipmentSlot.WeaponSlot1
                : !IsSlotOccupied(EquipmentSlot.WeaponSlot2) ? EquipmentSlot.WeaponSlot2 : EquipmentSlot.None;
        }

        EquipmentSlot fixedSlot = EquipmentSlotRules.ResolveFixedSlot(category);
        return fixedSlot != EquipmentSlot.None && !IsSlotOccupied(fixedSlot)
            ? fixedSlot
            : EquipmentSlot.None;
    }

    private EquipmentSlot TryResolveTargetSlot(LootId lootId, out int catalogIndex, out LootDefinition definition)
    {
        definition = null;
        catalogIndex = -1;
        return IsEquipmentReadable && _lootCatalog != null &&
            _lootCatalog.TryGetIndex(lootId, out catalogIndex) &&
            _lootCatalog.TryGetByIndex(catalogIndex, out definition) && definition != null
            ? ResolveFreeTargetSlot(definition.Category)
            : EquipmentSlot.None;
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

        int catalogIndex = GetCatalogIndexPlusOne(EquipmentSlotRules.FromWeaponSlot(activeSlot)) - 1;
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

    /// <summary>
    /// Resolves one admission reference into a catalog index, rechecking that the referenced unit
    /// may occupy <paramref name="slot"/>. Weapon slots additionally require a usable weapon.
    /// </summary>
    private bool TryResolveAdmissionSlot(
        IReadOnlyList<LootEntry> reservedLoadout,
        int entryIndexPlusOne,
        EquipmentSlot slot,
        out int catalogIndexPlusOne,
        out string error)
    {
        catalogIndexPlusOne = 0;
        error = null;
        if (entryIndexPlusOne == 0) return true;
        int entryIndex = entryIndexPlusOne - 1;
        if (entryIndex < 0 || entryIndex >= reservedLoadout.Count)
        {
            error = "Prepared equipment reference is outside the reserved loadout.";
            return false;
        }

        LootEntry entry = reservedLoadout[entryIndex];
        if (!_lootCatalog.TryGetIndex(entry.LootId, out int catalogIndex) ||
            !_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition) ||
            !EquipmentSlotRules.IsCompatible(definition.Category, slot))
        {
            error = $"Prepared '{entry.LootId.Value}' cannot occupy {slot}.";
            return false;
        }

        if (EquipmentSlotRules.IsWeaponSlot(slot) && !TryResolveValidWeapon(catalogIndex, out _, out _))
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

    private bool MatchesSlot(EquipmentSlot slot, LootEntry? expected)
    {
        bool hasCurrent = TryGetSlotLoot(slot, out LootEntry current);
        return expected.HasValue ? hasCurrent && current == expected.Value : !hasCurrent;
    }

    private int GetCatalogIndexPlusOne(EquipmentSlot slot) => IsEquipmentReadable
        ? slot switch
        {
            EquipmentSlot.WeaponSlot1 => WeaponSlot1CatalogIndexPlusOne,
            EquipmentSlot.WeaponSlot2 => WeaponSlot2CatalogIndexPlusOne,
            EquipmentSlot.Helmet => HelmetCatalogIndexPlusOne,
            EquipmentSlot.Armor => ArmorCatalogIndexPlusOne,
            EquipmentSlot.Gloves => GlovesCatalogIndexPlusOne,
            EquipmentSlot.Boots => BootsCatalogIndexPlusOne,
            _ => 0
        }
        : 0;

    private void SetCatalogIndexPlusOne(EquipmentSlot slot, int value)
    {
        switch (slot)
        {
            case EquipmentSlot.WeaponSlot1: WeaponSlot1CatalogIndexPlusOne = value; break;
            case EquipmentSlot.WeaponSlot2: WeaponSlot2CatalogIndexPlusOne = value; break;
            case EquipmentSlot.Helmet: HelmetCatalogIndexPlusOne = value; break;
            case EquipmentSlot.Armor: ArmorCatalogIndexPlusOne = value; break;
            case EquipmentSlot.Gloves: GlovesCatalogIndexPlusOne = value; break;
            case EquipmentSlot.Boots: BootsCatalogIndexPlusOne = value; break;
        }
    }

    /// <summary>
    /// Observes only the weapon state that can rebuild the combat strategy. Armor slots are
    /// deliberately excluded so equipping a piece never reconfigures the active attack.
    /// </summary>
    private bool HasReplicatedWeaponStateChanged() =>
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

    /// <summary>Dependencies every Equipment operation needs, weapons and armor alike.</summary>
    private bool ValidateEquipmentDependencies()
    {
        if (_lootCatalog != null && _lootReceiver != null && _character != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(PlayerWeaponEquipmentNetworkController)} has missing Equipment dependencies.", this);
        return false;
    }

    /// <summary>
    /// Dependencies needed only to resolve, activate or rebuild a weapon. Equipping armor must
    /// never fail because a combat strategy is unassigned.
    /// </summary>
    private bool ValidateWeaponDependencies()
    {
        if (_combatController != null && _meleeAttack != null && _rangedAttack != null &&
            _projectileSpawner != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(PlayerWeaponEquipmentNetworkController)} has missing weapon dependencies.", this);
        return false;
    }

    // The RPC transports the kind as int while the enum is byte-backed, so Enum.IsDefined would
    // reject the boxed value outright. The range check mirrors EquipmentSlotRules.IsValidSlotValue.
    private static bool IsValidRequestKind(int value) =>
        value == (int)EquipmentRequestKind.Equip || value == (int)EquipmentRequestKind.Unequip;

    private static bool WasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority) =>
        invokeInfo.SendMessageResult == RpcSendMessageResult.Sent ||
        hasStateAuthority && invokeInfo.LocalInvokeResult == RpcLocalInvokeResult.Invoked;
}
