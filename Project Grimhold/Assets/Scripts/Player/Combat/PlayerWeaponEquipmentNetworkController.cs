using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Owns the eight authoritative Raid Equipment slots — two Weapon Sets plus Helmet, Armor,
/// Gloves and Boots — and derives combat exclusively from the active Set's replicated Main Hand.
/// Armor slots hold Equipment state only and never reach the combat strategies.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-9)]
public sealed class PlayerWeaponEquipmentNetworkController : NetworkBehaviour, IEquipmentVisualSource
{
    private enum EquipmentRequestKind : byte
    {
        Equip = 1,
        Unequip = 2
    }

    private enum WeaponEligibilityFailure : byte
    {
        None = 0,
        InvalidDefinition = 1,
        AttributesUnavailable = 2,
        RequirementsNotMet = 3
    }

    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    [SerializeField] private PlayerLootReceiver _lootReceiver;
    [SerializeField] private MonoBehaviour _characterSource;
    [SerializeField] private PlayerCombatNetworkController _combatController;
    [SerializeField] private MeleeAttack _meleeAttack;
    [SerializeField] private RangedAttack _rangedAttack;
    [SerializeField] private FusionProjectileSpawner _projectileSpawner;
    [SerializeField] private RaidAvatarParticipantLink _participantLink;

    [Networked] private int WeaponSetAMainHandCatalogIndexPlusOne { get; set; }
    [Networked] private int WeaponSetBMainHandCatalogIndexPlusOne { get; set; }
    [Networked] private int WeaponSetAOffHandCatalogIndexPlusOne { get; set; }
    [Networked] private int WeaponSetBOffHandCatalogIndexPlusOne { get; set; }
    [Networked] private int HelmetCatalogIndexPlusOne { get; set; }
    [Networked] private int ArmorCatalogIndexPlusOne { get; set; }
    [Networked] private int GlovesCatalogIndexPlusOne { get; set; }
    [Networked] private int BootsCatalogIndexPlusOne { get; set; }
    [Networked] private int ActiveWeaponSetSlotValue { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] public int EquipmentRevision { get; private set; }

    private ICharacter _character;
    private PlayerRaidLootOriginState _raidOriginState;
    private NetworkMatchController _matchController;
    private bool _hasPendingAuthorityRequest;
    private EquipmentRequestKind _pendingRequestKind;
    private int _pendingCatalogIndex;
    private EquipmentSlot _pendingSlot;
    private int _pendingRequestSequence;
    private int _nextRequestSequence;
    private int _appliedSlot1 = int.MinValue;
    private int _appliedSlot2 = int.MinValue;
    private int _appliedOffHandA = int.MinValue;
    private int _appliedOffHandB = int.MinValue;
    private int _appliedActiveSlot = int.MinValue;
    private int _appliedAttributeRevision = int.MinValue;
    private bool _initializedBeforeSpawn;
    private bool _reportedUnavailableWeaponAttributes;
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
        (WeaponSetAMainHandCatalogIndexPlusOne > 0 || WeaponSetBMainHandCatalogIndexPlusOne > 0);

    /// <summary>True while any of the eight Equipment slots still owns a unit.</summary>
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

    public WeaponSetSlot ActiveWeaponSetSlot =>
        IsEquipmentReadable && ActiveWeaponSetSlotValue >= (int)WeaponSetSlot.None &&
        ActiveWeaponSetSlotValue <= (int)WeaponSetSlot.SetB
            ? (WeaponSetSlot)ActiveWeaponSetSlotValue
            : WeaponSetSlot.None;
    public int ObservedEquipmentRevision => IsEquipmentReadable ? EquipmentRevision : 0;
    public bool IsOffHandBlocked(WeaponSetSlot set)
    {
        EquipmentSlot mainHand = EquipmentSlotRules.GetMainHandSlot(set);
        return mainHand != EquipmentSlot.None &&
            TryGetSlotDefinition(mainHand, out LootDefinition definition) &&
            definition?.WeaponDefinition?.Handedness == WeaponHandedness.TwoHanded;
    }
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

            ActiveWeaponSetSlotValue = (int)WeaponSetSlot.None;
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
            ? TryEquipAuthority(catalogIndex, slot)
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
        _reportedUnavailableWeaponAttributes = false;
    }

    /// <summary>
    /// Reports whether the destination slot this loot would target is currently free.
    /// Mirrors the authoritative slot resolution so the UI does not offer impossible intentions.
    /// </summary>
    public bool CanEquip(LootId lootId, EquipmentSlot slot)
    {
        return TryResolveTargetSlot(lootId, slot, out int catalogIndex, out _) &&
            (!EquipmentSlotRules.IsHandSlot(slot) ||
                TryResolveEligibleWeapon(catalogIndex, out _, out _, out _) ==
                WeaponEligibilityFailure.None);
    }

    public bool TryRequestEquip(LootId lootId, EquipmentSlot slot)
    {
        if (!IsEquipmentReadable || !HasInputAuthority || HasRequestInFlight)
        {
            return false;
        }

        if (!TryResolveTargetSlot(lootId, slot, out int catalogIndex, out _) ||
            EquipmentSlotRules.IsHandSlot(slot) &&
            TryResolveEligibleWeapon(catalogIndex, out _, out _, out _) !=
            WeaponEligibilityFailure.None)
        {
            return false;
        }

        return TrySendRequest(EquipmentRequestKind.Equip, catalogIndex, slot);
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

    public bool TryRequestUnequip(WeaponSetSlot slot) =>
        TryRequestUnequip(EquipmentSlotRules.GetMainHandSlot(slot));

    public bool IsSlotOccupied(EquipmentSlot slot) => GetCatalogIndexPlusOne(slot) > 0;

    public bool IsSlotOccupied(WeaponSetSlot slot) =>
        IsSlotOccupied(EquipmentSlotRules.GetMainHandSlot(slot));

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

    public bool TryGetSlotLoot(WeaponSetSlot slot, out LootEntry entry) =>
        TryGetSlotLoot(EquipmentSlotRules.GetMainHandSlot(slot), out entry);

    public bool TryGetSlotRaidOrigin(EquipmentSlot slot, out RaidLootOrigin origin)
    {
        origin = default;
        return IsSlotOccupied(slot) && _raidOriginState != null &&
            _raidOriginState.TryGetEquipmentOrigin(slot, out origin);
    }

    public bool TryGetSlotDefinition(EquipmentSlot slot, out LootDefinition definition)
    {
        definition = null;
        int catalogIndex = GetCatalogIndexPlusOne(slot) - 1;
        return catalogIndex >= 0 && _lootCatalog != null &&
            _lootCatalog.TryGetByIndex(catalogIndex, out definition) && definition != null;
    }

    public bool TryGetSlotDefinition(WeaponSetSlot slot, out LootDefinition definition) =>
        TryGetSlotDefinition(EquipmentSlotRules.GetMainHandSlot(slot), out definition);

    /// <summary>Compatibility query whose result is always the active weapon.</summary>
    public bool TryGetEquippedLoot(out LootEntry entry) => TryGetSlotLoot(ActiveWeaponSetSlot, out entry);

    /// <summary>Resolves only the active weapon for combat and presentation consumers.</summary>
    public bool TryGetEquippedDefinition(out LootDefinition definition) =>
        TryGetSlotDefinition(ActiveWeaponSetSlot, out definition);

    /// <summary>
    /// Initializes all Equipment slots from compact references into an already initialized admission inventory.
    /// Validation completes before either Inventory or Equipment is mutated.
    /// </summary>
    public bool TryInitializePreparedEquipment(
        IReadOnlyList<LootEntry> reservedLoadout,
        IReadOnlyList<int> entryIndicesPlusOne,
        WeaponSetSlot activeWeaponSet,
        out string error)
    {
        error = null;
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        CacheDependencies();
        if (!HasStateAuthority || reservedLoadout == null || _lootReceiver == null || _lootCatalog == null)
        {
            error = "Prepared equipment initialization requires State Authority and loadout dependencies.";
            return false;
        }

        if (_participantLink == null ||
            !_participantLink.TryGetCharacterAttributeState(out CharacterAttributeState attributes))
        {
            error = "Prepared equipment initialization requires admitted character attributes.";
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
                    attributes,
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

        int setAOffIndex = Array.IndexOf(slots, EquipmentSlot.WeaponSetAOffHand);
        int setBOffIndex = Array.IndexOf(slots, EquipmentSlot.WeaponSetBOffHand);
        if (IsTwoHandedCatalogIndex(slot1Catalog) && catalogIndices[setAOffIndex] > 0 ||
            IsTwoHandedCatalogIndex(slot2Catalog) && catalogIndices[setBOffIndex] > 0)
        {
            error = "A two-handed prepared weapon cannot coexist with an Off Hand item in its Set.";
            return false;
        }

        if (activeWeaponSet != WeaponSetSlot.SetA && activeWeaponSet != WeaponSetSlot.SetB ||
            activeWeaponSet == WeaponSetSlot.SetA && slot1Catalog == 0 ||
            activeWeaponSet == WeaponSetSlot.SetB && slot2Catalog == 0)
        {
            error = "The active Weapon Set must reference a valid Main Hand weapon.";
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
            if (!TryValidateInventoryUnit(catalogIndices[index], out _, out error))
            {
                return false;
            }
        }

        int activeCatalog = activeWeaponSet == WeaponSetSlot.SetA ? slot1Catalog : slot2Catalog;
        if (TryResolveEligibleWeapon(
                activeCatalog - 1,
                attributes,
                out LootDefinition activeDefinition,
                out AttackConfig activeConfig) !=
                WeaponEligibilityFailure.None ||
            !TryConfigureStrategy(
                activeDefinition.WeaponDefinition,
                activeConfig,
                attributes,
                out _))
        {
            error = "The prepared active weapon cannot configure a combat strategy.";
            return false;
        }

        for (int index = 0; index < catalogIndices.Length; index++)
        {
            if (!TryCommitInventoryExtraction(
                    catalogIndices[index], out RaidLootOriginTransfer originTransfer, out error))
            {
                throw new InvalidOperationException(error ?? "Validated prepared equipment could not be committed.");
            }

            if (catalogIndices[index] != 0 &&
                !_raidOriginState.TrySetEquipmentOrigin(slots[index], originTransfer.Buckets[0].Origin))
            {
                throw new InvalidOperationException("Validated prepared Equipment provenance could not be committed.");
            }
        }

        for (int index = 0; index < slots.Length; index++)
        {
            SetCatalogIndexPlusOne(slots[index], catalogIndices[index]);
        }

        ActiveWeaponSetSlotValue = (int)activeWeaponSet;
        EquipmentRevision++;
        _initializedBeforeSpawn = true;
        return true;
    }

    private bool IsTwoHandedCatalogIndex(int catalogIndexPlusOne)
    {
        return catalogIndexPlusOne > 0 &&
            _lootCatalog.TryGetByIndex(catalogIndexPlusOne - 1, out LootDefinition definition) &&
            definition?.WeaponDefinition?.Handedness == WeaponHandedness.TwoHanded;
    }

    /// <summary>
    /// Verifies every Equipment slot against the expected expedition ownership snapshot.
    /// Parameters are named per slot so callers cannot mismatch positional entries.
    /// </summary>
    public bool TryMatchesExactEquipment(
        LootEntry? expectedWeaponSetAMainHand,
        LootEntry? expectedWeaponSetBMainHand,
        LootEntry? expectedHelmet,
        LootEntry? expectedArmor,
        LootEntry? expectedGloves,
        LootEntry? expectedBoots,
        LootEntry? expectedWeaponSetAOffHand,
        LootEntry? expectedWeaponSetBOffHand,
        out string error)
    {
        error = null;
        if (MatchesSlot(EquipmentSlot.WeaponSetAMainHand, expectedWeaponSetAMainHand) &&
            MatchesSlot(EquipmentSlot.WeaponSetBMainHand, expectedWeaponSetBMainHand) &&
            MatchesSlot(EquipmentSlot.Helmet, expectedHelmet) &&
            MatchesSlot(EquipmentSlot.Armor, expectedArmor) &&
            MatchesSlot(EquipmentSlot.Gloves, expectedGloves) &&
            MatchesSlot(EquipmentSlot.Boots, expectedBoots) &&
            MatchesSlot(EquipmentSlot.WeaponSetAOffHand, expectedWeaponSetAOffHand) &&
            MatchesSlot(EquipmentSlot.WeaponSetBOffHand, expectedWeaponSetBOffHand))
        {
            return true;
        }

        error = "Equipment no longer matches the expected snapshot.";
        return false;
    }

    public bool TryClearExactEquipment(
        LootEntry? expectedWeaponSetAMainHand,
        LootEntry? expectedWeaponSetBMainHand,
        LootEntry? expectedHelmet,
        LootEntry? expectedArmor,
        LootEntry? expectedGloves,
        LootEntry? expectedBoots,
        LootEntry? expectedWeaponSetAOffHand,
        LootEntry? expectedWeaponSetBOffHand,
        out string error)
    {
        if (!HasStateAuthority)
        {
            error = "Equipment can only be cleared by State Authority.";
            return false;
        }

        if (!TryMatchesExactEquipment(
                expectedWeaponSetAMainHand, expectedWeaponSetBMainHand, expectedHelmet,
                expectedArmor, expectedGloves, expectedBoots,
                expectedWeaponSetAOffHand, expectedWeaponSetBOffHand, out error))
        {
            return false;
        }

        for (int index = 0; index < AllSlots.Length; index++)
        {
            SetCatalogIndexPlusOne(AllSlots[index], 0);
        }

        ActiveWeaponSetSlotValue = (int)WeaponSetSlot.None;
        EquipmentRevision++;
        ApplyReplicatedActiveWeapon();
        return true;
    }

    public bool TryMatchesExactEquipmentOrigins(
        RaidLootOrigin? expectedWeaponSetAMainHand,
        RaidLootOrigin? expectedWeaponSetBMainHand,
        RaidLootOrigin? expectedHelmet,
        RaidLootOrigin? expectedArmor,
        RaidLootOrigin? expectedGloves,
        RaidLootOrigin? expectedBoots,
        RaidLootOrigin? expectedWeaponSetAOffHand,
        RaidLootOrigin? expectedWeaponSetBOffHand,
        out string error)
    {
        error = null;
        if (MatchesSlotOrigin(EquipmentSlot.WeaponSetAMainHand, expectedWeaponSetAMainHand) &&
            MatchesSlotOrigin(EquipmentSlot.WeaponSetBMainHand, expectedWeaponSetBMainHand) &&
            MatchesSlotOrigin(EquipmentSlot.Helmet, expectedHelmet) &&
            MatchesSlotOrigin(EquipmentSlot.Armor, expectedArmor) &&
            MatchesSlotOrigin(EquipmentSlot.Gloves, expectedGloves) &&
            MatchesSlotOrigin(EquipmentSlot.Boots, expectedBoots) &&
            MatchesSlotOrigin(EquipmentSlot.WeaponSetAOffHand, expectedWeaponSetAOffHand) &&
            MatchesSlotOrigin(EquipmentSlot.WeaponSetBOffHand, expectedWeaponSetBOffHand))
        {
            return true;
        }

        error = "Equipment provenance no longer matches the expected snapshot.";
        return false;
    }

    public bool TryClearExactEquipmentOrigins(
        RaidLootOrigin? expectedWeaponSetAMainHand,
        RaidLootOrigin? expectedWeaponSetBMainHand,
        RaidLootOrigin? expectedHelmet,
        RaidLootOrigin? expectedArmor,
        RaidLootOrigin? expectedGloves,
        RaidLootOrigin? expectedBoots,
        RaidLootOrigin? expectedWeaponSetAOffHand,
        RaidLootOrigin? expectedWeaponSetBOffHand,
        out string error)
    {
        error = null;
        if (!HasStateAuthority)
        {
            error = "Equipment provenance can only be cleared by State Authority.";
            return false;
        }

        if (!TryMatchesExactEquipmentOrigins(
                expectedWeaponSetAMainHand, expectedWeaponSetBMainHand, expectedHelmet,
                expectedArmor, expectedGloves, expectedBoots,
                expectedWeaponSetAOffHand, expectedWeaponSetBOffHand, out error))
        {
            return false;
        }

        RaidLootOrigin?[] expected =
        {
            expectedWeaponSetAMainHand, expectedWeaponSetBMainHand, expectedHelmet,
            expectedArmor, expectedGloves, expectedBoots,
            expectedWeaponSetAOffHand, expectedWeaponSetBOffHand
        };
        for (int index = 0; index < AllSlots.Length; index++)
        {
            if (expected[index].HasValue &&
                !_raidOriginState.TryClearEquipmentOrigin(AllSlots[index], expected[index].Value))
            {
                throw new InvalidOperationException("Validated Equipment provenance could not be cleared.");
            }
        }

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

    private EquipmentOperationResult TryEquipAuthority(int catalogIndex, EquipmentSlot targetSlot)
    {
        if (!ValidateEquipmentDependencies()) return EquipmentOperationResult.DependenciesUnavailable;
        if (!CanMutateEquipment()) return EquipmentOperationResult.PlayerUnavailable;

        if (!_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition) || definition == null ||
            !EquipmentSlotRules.IsCompatible(definition.Category, targetSlot))
        {
            return EquipmentOperationResult.InvalidEquipment;
        }

        if (!EquipmentSlotRules.IsHandSlot(targetSlot) && IsSlotOccupied(targetSlot))
        {
            return EquipmentOperationResult.SlotOccupied;
        }

        if (!TryResolveTargetSlot(definition.LootId, targetSlot, out int resolvedIndex, out _) ||
            resolvedIndex != catalogIndex)
        {
            return EquipmentOperationResult.IncompatibleHandConfiguration;
        }

        // Only weapons reach the combat strategies. Armor never depends on them, so their
        // dependencies are validated exclusively on this branch.
        bool becomesActive = false;
        if (EquipmentSlotRules.IsHandSlot(targetSlot))
        {
            if (!ValidateWeaponDependencies()) return EquipmentOperationResult.DependenciesUnavailable;
            WeaponEligibilityFailure eligibility = TryResolveEligibleWeapon(
                catalogIndex,
                out _,
                out AttackConfig attackConfig,
                out CharacterAttributeState attributes);
            if (eligibility == WeaponEligibilityFailure.AttributesUnavailable)
            {
                return EquipmentOperationResult.DependenciesUnavailable;
            }

            if (eligibility == WeaponEligibilityFailure.RequirementsNotMet)
            {
                return EquipmentOperationResult.AttributeRequirementsNotMet;
            }

            if (eligibility != WeaponEligibilityFailure.None)
            {
                return EquipmentOperationResult.InvalidEquipment;
            }

            becomesActive = EquipmentSlotRules.IsMainHandSlot(targetSlot) &&
                (ActiveWeaponSetSlot == WeaponSetSlot.None ||
                 ActiveWeaponSetSlot == EquipmentSlotRules.GetWeaponSet(targetSlot));
            if (becomesActive && !TryConfigureStrategy(
                    definition.WeaponDefinition,
                    attackConfig,
                    attributes,
                    out _))
            {
                return EquipmentOperationResult.InvalidEquipment;
            }
        }

        LootTransferRequest extraction = CreateInventoryTransfer(definition.LootId);
        if (_lootReceiver.ValidateExtraction(extraction) != LootTransferFailureReason.None ||
            !_lootReceiver.TryResolveRaidLootOriginTransfer(extraction, out RaidLootOriginTransfer originTransfer))
        {
            return EquipmentOperationResult.ItemNotOwned;
        }

        EquipmentSlot secondDisplacedSlot = EquipmentSlot.None;
        if (definition.Category == LootCategory.Weapon &&
            definition.WeaponDefinition.Handedness == WeaponHandedness.TwoHanded)
        {
            secondDisplacedSlot = EquipmentSlotRules.GetOffHandSlot(targetSlot);
        }

        var displacedSlots = new[] { targetSlot, secondDisplacedSlot };
        var displacedEntries = new LootEntry?[2];
        var displacedOrigins = new RaidLootOrigin[2];
        var displacedTransfers = new RaidLootOriginTransfer[2];
        for (int index = 0; index < displacedSlots.Length; index++)
        {
            EquipmentSlot displacedSlot = displacedSlots[index];
            if (displacedSlot == EquipmentSlot.None || !TryGetSlotLoot(displacedSlot, out LootEntry displaced))
            {
                continue;
            }

            if (!TryGetSlotRaidOrigin(displacedSlot, out displacedOrigins[index]) ||
                !RaidLootOriginTransfer.TryCreate(displacedOrigins[index], 1, out displacedTransfers[index]))
            {
                return EquipmentOperationResult.DependenciesUnavailable;
            }

            displacedEntries[index] = displaced;
        }

        if (!CanApplyInventoryExchange(definition.LootId, displacedEntries))
        {
            return EquipmentOperationResult.InventoryFull;
        }

        for (int index = 0; index < displacedEntries.Length; index++)
        {
            if (!displacedEntries[index].HasValue) continue;
            LootTransferRequest receive = CreateInventoryTransfer(displacedEntries[index].Value.LootId);
            if (_lootReceiver.ValidateRaidLootOriginReceive(receive, displacedTransfers[index]) !=
                LootTransferFailureReason.None)
            {
                return EquipmentOperationResult.DependenciesUnavailable;
            }
        }

        _lootReceiver.CommitRaidLootExtraction(extraction, originTransfer);
        for (int index = 0; index < displacedEntries.Length; index++)
        {
            if (!displacedEntries[index].HasValue) continue;
            LootTransferRequest receive = CreateInventoryTransfer(displacedEntries[index].Value.LootId);
            _lootReceiver.CommitRaidLootReceive(receive, displacedTransfers[index]);
            if (!_raidOriginState.TryClearEquipmentOrigin(displacedSlots[index], displacedOrigins[index]))
            {
                throw new InvalidOperationException("Validated displaced Equipment provenance could not be cleared.");
            }
            SetCatalogIndexPlusOne(displacedSlots[index], 0);
        }
        if (!_raidOriginState.TrySetEquipmentOrigin(targetSlot, originTransfer.Buckets[0].Origin))
        {
            throw new InvalidOperationException("Validated Equipment provenance could not be committed.");
        }
        SetCatalogIndexPlusOne(targetSlot, catalogIndex + 1);
        EquipmentRevision++;
        if (becomesActive)
        {
            ActiveWeaponSetSlotValue = (int)EquipmentSlotRules.GetWeaponSet(targetSlot);
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
        if (!TryGetSlotRaidOrigin(slot, out RaidLootOrigin origin) ||
            !RaidLootOriginTransfer.TryCreate(origin, 1, out RaidLootOriginTransfer originTransfer) ||
            _lootReceiver.ValidateReceive(receive) != LootTransferFailureReason.None ||
            _lootReceiver.ValidateRaidLootOriginReceive(receive, originTransfer) != LootTransferFailureReason.None)
        {
            return EquipmentOperationResult.InventoryFull;
        }

        _lootReceiver.CommitRaidLootReceive(receive, originTransfer);
        if (!_raidOriginState.TryClearEquipmentOrigin(slot, origin))
        {
            throw new InvalidOperationException("Validated Equipment provenance could not be cleared.");
        }
        SetCatalogIndexPlusOne(slot, 0);
        EquipmentRevision++;
        if (EquipmentSlotRules.IsMainHandSlot(slot) &&
            ActiveWeaponSetSlot == EquipmentSlotRules.GetWeaponSet(slot))
        {
            WeaponSetSlot other = EquipmentSlotRules.GetWeaponSet(slot) == WeaponSetSlot.SetA
                ? WeaponSetSlot.SetB
                : WeaponSetSlot.SetA;
            ActiveWeaponSetSlotValue = IsSlotOccupied(other) ? (int)other : (int)WeaponSetSlot.None;
            ApplyReplicatedActiveWeapon();
        }
        else
        {
            CaptureAppliedState();
        }

        return EquipmentOperationResult.Succeeded;
    }

    private bool TryResolveTargetSlot(
        LootId lootId,
        EquipmentSlot targetSlot,
        out int catalogIndex,
        out LootDefinition definition)
    {
        definition = null;
        catalogIndex = -1;
        if (!IsEquipmentReadable || _lootCatalog == null ||
            !_lootCatalog.TryGetIndex(lootId, out catalogIndex) ||
            !_lootCatalog.TryGetByIndex(catalogIndex, out definition) || definition == null ||
            !EquipmentSlotRules.IsCompatible(definition.Category, targetSlot))
        {
            return false;
        }

        if (!EquipmentSlotRules.IsHandSlot(targetSlot))
        {
            return !IsSlotOccupied(targetSlot);
        }

        WeaponDefinition weapon = definition.WeaponDefinition;
        if (!EquipmentSlotRules.IsCompatible(weapon, targetSlot)) return false;
        if (!EquipmentSlotRules.IsOffHandSlot(targetSlot)) return true;
        EquipmentSlot mainHand = EquipmentSlotRules.GetMainHandSlot(targetSlot);
        return !TryGetSlotDefinition(mainHand, out LootDefinition mainDefinition) ||
            mainDefinition.WeaponDefinition == null ||
            mainDefinition.WeaponDefinition.Handedness != WeaponHandedness.TwoHanded;
    }

    private void ProcessWeaponSelectionInput()
    {
        if (!GetInput(out PlayerNetworkInput input))
        {
            return;
        }

        NetworkButtons current = input.Buttons;
        bool slot1Pressed = current.WasPressed(PreviousButtons, PlayerInputButton.WeaponSetA);
        bool slot2Pressed = current.WasPressed(PreviousButtons, PlayerInputButton.WeaponSetB);
        PreviousButtons = current;

        if (slot1Pressed == slot2Pressed || !CanMutateEquipment())
        {
            return;
        }

        WeaponSetSlot requested = slot1Pressed ? WeaponSetSlot.SetA : WeaponSetSlot.SetB;
        if (requested == ActiveWeaponSetSlot || !IsSlotOccupied(requested))
        {
            return;
        }

        int requestedCatalogIndex =
            GetCatalogIndexPlusOne(EquipmentSlotRules.GetMainHandSlot(requested)) - 1;
        if (TryResolveEligibleWeapon(requestedCatalogIndex, out _, out _, out _) !=
            WeaponEligibilityFailure.None)
        {
            return;
        }

        ActiveWeaponSetSlotValue = (int)requested;
        EquipmentRevision++;
        ApplyReplicatedActiveWeapon();
    }

    private void ApplyReplicatedActiveWeapon()
    {
        CaptureAppliedState();
        WeaponSetSlot activeSlot = ActiveWeaponSetSlot;
        if (!IsSlotOccupied(activeSlot))
        {
            if (HasStateAuthority)
            {
                ActiveWeaponSetSlotValue = (int)WeaponSetSlot.None;
                _appliedActiveSlot = ActiveWeaponSetSlotValue;
                _combatController.TryClearActiveAttack();
            }
            return;
        }

        int catalogIndex = GetCatalogIndexPlusOne(EquipmentSlotRules.GetMainHandSlot(activeSlot)) - 1;
        WeaponEligibilityFailure eligibility = TryResolveEligibleWeapon(
            catalogIndex,
            out LootDefinition definition,
            out AttackConfig attackConfig,
            out CharacterAttributeState attributes);
        if (eligibility == WeaponEligibilityFailure.AttributesUnavailable)
        {
            if (!_reportedUnavailableWeaponAttributes)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponEquipmentNetworkController)} cannot rebuild the active weapon until the admitted participant attributes are available.",
                    this);
                _reportedUnavailableWeaponAttributes = true;
            }

            // Participant remapping can resolve after the avatar during Host Migration. Preserve
            // the replicated selection and retry instead of turning a temporary dependency gap
            // into an authoritative Equipment mutation.
            _appliedActiveSlot = int.MinValue;
            if (HasStateAuthority)
            {
                _combatController.TryClearActiveAttack();
            }
            return;
        }

        _reportedUnavailableWeaponAttributes = false;
        if (eligibility != WeaponEligibilityFailure.None ||
            !TryConfigureStrategy(
                definition.WeaponDefinition,
                attackConfig,
                attributes,
                out MonoBehaviour attackSource))
        {
            Debug.LogError($"{nameof(PlayerWeaponEquipmentNetworkController)} could not rebuild active weapon index {catalogIndex}.", this);
            if (HasStateAuthority)
            {
                ActiveWeaponSetSlotValue = (int)WeaponSetSlot.None;
                _appliedActiveSlot = ActiveWeaponSetSlotValue;
                EquipmentRevision++;
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
        in CharacterAttributeState attributes,
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

        if (EquipmentSlotRules.IsHandSlot(slot))
        {
            WeaponEligibilityFailure failure = TryResolveEligibleWeapon(
                catalogIndex, attributes, out _, out _);
            if (failure != WeaponEligibilityFailure.None)
            {
                error = failure == WeaponEligibilityFailure.RequirementsNotMet
                    ? $"Prepared weapon '{entry.LootId.Value}' does not meet attribute requirements."
                    : $"Prepared weapon '{entry.LootId.Value}' is invalid.";
                return false;
            }
        }

        catalogIndexPlusOne = catalogIndex + 1;
        return true;
    }

    private bool TryValidateInventoryUnit(
        int catalogIndexPlusOne,
        out RaidLootOriginTransfer originTransfer,
        out string error)
    {
        originTransfer = RaidLootOriginTransfer.Empty;
        error = null;
        if (catalogIndexPlusOne == 0) return true;
        if (!_lootCatalog.TryGetByIndex(catalogIndexPlusOne - 1, out LootDefinition definition))
        {
            error = "Prepared weapon catalog index is invalid.";
            return false;
        }

        LootTransferRequest request = CreateInventoryTransfer(definition.LootId);
        if (_lootReceiver.ValidateExtraction(request) == LootTransferFailureReason.None &&
            _lootReceiver.TryResolveRaidLootOriginTransfer(request, out originTransfer))
        {
            return true;
        }

        error = $"Reserved loadout does not own prepared weapon '{definition.LootId.Value}'.";
        return false;
    }

    private bool TryCommitInventoryExtraction(
        int catalogIndexPlusOne,
        out RaidLootOriginTransfer originTransfer,
        out string error)
    {
        originTransfer = RaidLootOriginTransfer.Empty;
        error = null;
        if (catalogIndexPlusOne == 0) return true;
        _lootCatalog.TryGetByIndex(catalogIndexPlusOne - 1, out LootDefinition definition);
        LootTransferRequest request = CreateInventoryTransfer(definition.LootId);
        if (_lootReceiver.ValidateExtraction(request) != LootTransferFailureReason.None ||
            !_lootReceiver.TryResolveRaidLootOriginTransfer(request, out originTransfer))
        {
            error = "Prepared Equipment ownership changed during initialization.";
            return false;
        }

        _lootReceiver.CommitRaidLootExtraction(request, originTransfer);
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

    private WeaponEligibilityFailure TryResolveEligibleWeapon(
        int catalogIndex,
        out LootDefinition definition,
        out AttackConfig attackConfig,
        out CharacterAttributeState attributes)
    {
        attributes = default;
        if (!TryResolveValidWeapon(catalogIndex, out definition, out attackConfig))
        {
            return WeaponEligibilityFailure.InvalidDefinition;
        }

        if (_participantLink == null || !_participantLink.TryGetCharacterAttributeState(out attributes))
        {
            return WeaponEligibilityFailure.AttributesUnavailable;
        }

        return definition.WeaponDefinition.AreAttributeRequirementsSatisfiedBy(attributes)
            ? WeaponEligibilityFailure.None
            : WeaponEligibilityFailure.RequirementsNotMet;
    }

    private WeaponEligibilityFailure TryResolveEligibleWeapon(
        int catalogIndex,
        in CharacterAttributeState attributes,
        out LootDefinition definition,
        out AttackConfig attackConfig)
    {
        if (!TryResolveValidWeapon(catalogIndex, out definition, out attackConfig))
        {
            return WeaponEligibilityFailure.InvalidDefinition;
        }

        return definition.WeaponDefinition.AreAttributeRequirementsSatisfiedBy(attributes)
            ? WeaponEligibilityFailure.None
            : WeaponEligibilityFailure.RequirementsNotMet;
    }

    private bool TryConfigureStrategy(
        WeaponDefinition weaponDefinition,
        AttackConfig attackConfig,
        in CharacterAttributeState attributes,
        out MonoBehaviour attackSource)
    {
        attackSource = null;
        if (!TryResolveEffectiveDamage(weaponDefinition, attributes, out float effectiveDamage))
        {
            return false;
        }

        var parameters = new AttackExecutionParameters(
            effectiveDamage,
            weaponDefinition.DamageType,
            weaponDefinition.AttackIntervalSeconds,
            weaponDefinition.Range,
            weaponDefinition.KnockbackForce);
        if (!parameters.TryValidate(out _))
        {
            return false;
        }

        if (attackConfig is MeleeAttackConfig meleeConfig && _meleeAttack != null &&
            _meleeAttack.TryConfigure(meleeConfig, parameters))
        {
            attackSource = _meleeAttack;
            return true;
        }

        if (attackConfig is RangedAttackConfig rangedConfig && _rangedAttack != null &&
            _projectileSpawner != null && _projectileSpawner.TryConfigure(rangedConfig) &&
            _rangedAttack.TryConfigure(rangedConfig, parameters))
        {
            attackSource = _rangedAttack;
            return true;
        }

        return false;
    }

    private static bool TryResolveEffectiveDamage(
        WeaponDefinition weaponDefinition,
        in CharacterAttributeState attributes,
        out float effectiveDamage)
    {
        effectiveDamage = 0f;
        if (weaponDefinition == null ||
            !WeaponScalingContributionsResolver.TryResolve(
                weaponDefinition.OffensiveScaling,
                out WeaponScalingContributions contributions))
        {
            return false;
        }

        return WeaponDamageCalculator.TryCalculate(
            weaponDefinition.BaseDamage,
            attributes,
            contributions,
            out effectiveDamage) && effectiveDamage > 0f;
    }

    private bool CanApplyInventoryExchange(LootId equippedLootId, LootEntry?[] displaced)
    {
        if (!_lootReceiver.TryGetLootContent(out IReadOnlyList<LootEntry> current)) return false;
        var amounts = new Dictionary<LootId, int>(current.Count);
        for (int index = 0; index < current.Count; index++)
        {
            amounts[current[index].LootId] = current[index].Amount;
        }

        if (!amounts.TryGetValue(equippedLootId, out int equippedAmount) || equippedAmount < 1)
        {
            return false;
        }

        if (equippedAmount == 1) amounts.Remove(equippedLootId);
        else amounts[equippedLootId] = equippedAmount - 1;

        for (int index = 0; index < displaced.Length; index++)
        {
            if (!displaced[index].HasValue) continue;
            LootId lootId = displaced[index].Value.LootId;
            amounts.TryGetValue(lootId, out int amount);
            amounts[lootId] = amount + 1;
        }

        return amounts.Count <= _lootReceiver.SlotCapacity;
    }

    private bool CanMutateEquipment() => _character != null && _character.IsAlive &&
        (_matchController == null || _matchController.Phase == NetworkMatchController.MatchPhase.InProgress);

    private bool MatchesSlot(EquipmentSlot slot, LootEntry? expected)
    {
        bool hasCurrent = TryGetSlotLoot(slot, out LootEntry current);
        return expected.HasValue ? hasCurrent && current == expected.Value : !hasCurrent;
    }

    private bool MatchesSlotOrigin(EquipmentSlot slot, RaidLootOrigin? expected)
    {
        bool hasCurrent = TryGetSlotRaidOrigin(slot, out RaidLootOrigin current);
        return expected.HasValue ? hasCurrent && current == expected.Value : !hasCurrent;
    }

    private int GetCatalogIndexPlusOne(EquipmentSlot slot) => IsEquipmentReadable
        ? slot switch
        {
            EquipmentSlot.WeaponSetAMainHand => WeaponSetAMainHandCatalogIndexPlusOne,
            EquipmentSlot.WeaponSetBMainHand => WeaponSetBMainHandCatalogIndexPlusOne,
            EquipmentSlot.WeaponSetAOffHand => WeaponSetAOffHandCatalogIndexPlusOne,
            EquipmentSlot.WeaponSetBOffHand => WeaponSetBOffHandCatalogIndexPlusOne,
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
            case EquipmentSlot.WeaponSetAMainHand: WeaponSetAMainHandCatalogIndexPlusOne = value; break;
            case EquipmentSlot.WeaponSetBMainHand: WeaponSetBMainHandCatalogIndexPlusOne = value; break;
            case EquipmentSlot.WeaponSetAOffHand: WeaponSetAOffHandCatalogIndexPlusOne = value; break;
            case EquipmentSlot.WeaponSetBOffHand: WeaponSetBOffHandCatalogIndexPlusOne = value; break;
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
        _appliedSlot1 != WeaponSetAMainHandCatalogIndexPlusOne ||
        _appliedSlot2 != WeaponSetBMainHandCatalogIndexPlusOne ||
        _appliedOffHandA != WeaponSetAOffHandCatalogIndexPlusOne ||
        _appliedOffHandB != WeaponSetBOffHandCatalogIndexPlusOne ||
        _appliedActiveSlot != ActiveWeaponSetSlotValue ||
        _participantLink == null ||
        !_participantLink.TryGetCharacterAttributeRevision(out int attributeRevision) ||
        _appliedAttributeRevision != attributeRevision;

    private void CaptureAppliedState()
    {
        _appliedSlot1 = WeaponSetAMainHandCatalogIndexPlusOne;
        _appliedSlot2 = WeaponSetBMainHandCatalogIndexPlusOne;
        _appliedOffHandA = WeaponSetAOffHandCatalogIndexPlusOne;
        _appliedOffHandB = WeaponSetBOffHandCatalogIndexPlusOne;
        _appliedActiveSlot = ActiveWeaponSetSlotValue;
        _appliedAttributeRevision = _participantLink != null &&
            _participantLink.TryGetCharacterAttributeRevision(out int revision)
                ? revision
                : int.MinValue;
    }

    private void CacheDependencies()
    {
        _character = _characterSource as ICharacter;
        _character ??= GetComponent<ICharacter>();
        _raidOriginState ??= GetComponent<PlayerRaidLootOriginState>();
        _participantLink ??= GetComponent<RaidAvatarParticipantLink>();
    }

    /// <summary>Dependencies every Equipment operation needs, weapons and armor alike.</summary>
    private bool ValidateEquipmentDependencies()
    {
        if (_lootCatalog != null && _lootReceiver != null && _character != null && _raidOriginState != null)
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
