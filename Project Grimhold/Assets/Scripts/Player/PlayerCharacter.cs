using UnityEngine;
using Fusion;

/// <summary>
/// Concrete network character implementation used by player-controlled entities.
/// Character identity, health and damage behavior are inherited from CharacterBase.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCharacter : CharacterBase, IManaReceiver
{
    [SerializeField]
    private PlayerCorpseGenerationController _corpseGenerationController;

    [SerializeField]
    private PlayerExtractionController _extractionController;

    [SerializeField]
    private RaidAvatarParticipantLink _participantLink;

    [SerializeField]
    private PlayerWeaponEquipmentNetworkController _equipmentController;

    [SerializeField]
    private PlayerShieldDefenseNetworkController _shieldDefenseController;

    [SerializeField]
    private PlayerDownedStateNetworkController _downedStateController;

    [SerializeField, Min(0.0001f)]
    private float _defenseMitigationConstant = 100f;

    private PlayerStaminaNetworkController _staminaController;
    private PlayerConsumableNetworkController _consumableController;

    [SerializeField]
    private PlayerManaNetworkController _manaController;

    [SerializeField]
    private PlayerInventoryPassiveEffectsController _passiveEffectsController;

    [SerializeField]
    private PlayerLootReceiver _lootReceiver;

    private int _clampedLootRevision = int.MinValue;

    public PlayerInventoryPassiveEffectsController PassiveEffectsController => _passiveEffectsController;
    public float VisionRadiusMultiplier => _passiveEffectsController != null ? _passiveEffectsController.VisionRadiusMultiplier : 1f;

    public bool IsConsuming => _consumableController != null && _consumableController.IsConsuming;
    public float ConsumableMovementSpeedMultiplier =>
        _consumableController != null && _consumableController.IsConsuming
            ? _consumableController.VoluntaryMovementSpeedMultiplier
            : 1f;

    public float CurrentMana => _manaController != null ? _manaController.CurrentMana : 0f;
    public float MaximumMana => _manaController != null && _manaController.TryGetMaximumMana(out float max) ? max : 0f;

    public bool TryRestoreMana(float amount, out string failureReason)
    {
        if (_manaController == null)
        {
            failureReason = "El objetivo no puede gestionar maná.";
            return false;
        }

        if (!IsAlive)
        {
            failureReason = "El objetivo está muerto.";
            return false;
        }

        if (IsDowned)
        {
            failureReason = "El objetivo está derribado.";
            return false;
        }

        if (!_manaController.TryGetMaximumMana(out float maximum))
        {
            failureReason = "El objetivo no puede gestionar maná.";
            return false;
        }

        if (_manaController.CurrentMana >= maximum)
        {
            failureReason = "El maná ya está al máximo.";
            return false;
        }

        if (!_manaController.TryRestore(amount))
        {
            failureReason = "No se pudo restaurar el maná.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    protected override void OnDamageApplied(float amount) => _consumableController?.CancelConsumption();
    private bool _reportedMissingExtractionController;
    private bool _hasCachedEquipmentStatistics;
    private EquipmentStatisticsModifiers _cachedEquipmentStatistics;
    private int _cachedEquipmentRevision = int.MinValue;
    private bool _hasCachedRuntimeStatistics;
    private PlayerRuntimeStatistics _cachedRuntimeStatistics;
    private int _cachedRuntimeAttributeRevision = int.MinValue;
    private int _cachedRuntimeEquipmentRevision = int.MinValue;
    private int _clampedAttributeRevision = int.MinValue;
    private int _clampedEquipmentRevision = int.MinValue;
    private bool _reportedInvalidEquipmentStatistics;
    private bool _reportedInvalidRuntimeStatistics;
    private bool _reportedInvalidMitigation;

    [Networked]
    public NetworkString<_32> ProfileIdString { get; set; }

    /// <summary>Indicates whether the player is in the Downed state (Health 0, reserve active).</summary>
    public bool IsDowned => _downedStateController != null &&
                            _downedStateController.Object != null &&
                            _downedStateController.Object.IsValid &&
                            _downedStateController.IsDowned;

    /// <summary>
    /// Contract for future crowd-control and status-effect systems: false while Downed or defeated.
    /// </summary>
    public bool CanReceiveStatusEffects => IsAlive && !IsDowned;

    /// <summary>A Downed player is still alive until the reserve is depleted.</summary>
    public override bool IsAlive => base.IsAlive || IsDowned;

    /// <summary>Only Active players may authorize a new knockback impact.</summary>
    public override bool IsKnockbackEligible => IsAlive && !IsDowned;

    /// <summary>
    /// Resolves equipment RE without primary-attribute scaling. Temporary effects granting RE
    /// will be summed here when their design is defined.
    /// </summary>
    protected override float ResolveKnockbackResistance()
    {
        return TryGetEquipmentStatistics(out EquipmentStatisticsModifiers statistics, out _)
            ? statistics.KnockbackResistance : 0f;
    }

    /// <summary>Conventional healing does not restore Health while Downed.</summary>
    protected override bool CanReceiveHealing => !IsDowned && base.CanReceiveHealing;

    /// <summary>
    /// Indicates whether the player character can receive damage.
    /// Returns false if the player is alive and in Extracted process state.
    /// </summary>
    public override bool CanReceiveDamage
    {
        get
        {
            if (Object == null || !Object.IsValid)
            {
                return base.CanReceiveDamage;
            }

            if (_extractionController == null)
            {
                if (!_reportedMissingExtractionController)
                {
                    Debug.LogWarning(
                        $"{nameof(PlayerCharacter)}: {nameof(PlayerExtractionController)} composition missing on object {name}.",
                        this);
                    _reportedMissingExtractionController = true;
                }

                return base.CanReceiveDamage;
            }

            if (_extractionController.State == ExtractionState.Extracted)
            {
                return false;
            }

            return base.CanReceiveDamage;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        CacheDependencies();
    }

    /// <summary>
    /// Derives the participant's effective maximum Health from Raid attributes and Equipment.
    /// A temporarily unresolved participant link keeps the prefab fallback available without
    /// caching it, so Host Migration remapping can resolve the authoritative snapshot later.
    /// </summary>
    protected override float ResolveMaximumHealth()
    {
        float maxHealth = TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics)
            ? statistics.MaximumHealth
            : base.ResolveMaximumHealth();

        if (_passiveEffectsController != null && _passiveEffectsController.HasChaosCoin)
        {
            maxHealth *= _passiveEffectsController.MaxHealthMultiplier;
        }

        return maxHealth;
    }

    /// <summary>Gets the effective local projection without duplicating authoritative state.</summary>
    public bool TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics)
    {
        statistics = default;
        if (_participantLink == null || _equipmentController == null ||
            !_participantLink.TryGetCharacterAttributeState(out CharacterAttributeState attributes) ||
            !_participantLink.TryGetCharacterAttributeRevision(out int attributeRevision) ||
            !TryGetEquipmentStatistics(out EquipmentStatisticsModifiers equipment, out int equipmentRevision))
        {
            return false;
        }

        if (_hasCachedRuntimeStatistics &&
            _cachedRuntimeAttributeRevision == attributeRevision &&
            _cachedRuntimeEquipmentRevision == equipmentRevision)
        {
            statistics = _cachedRuntimeStatistics;
            return true;
        }

        if (!PlayerRuntimeStatisticsCalculator.TryCalculate(
                attributes,
                ProgressionBalanceDefaults.InitialCharacterDerivedStatisticsConfiguration,
                equipment,
                out statistics,
                out CharacterDerivedStatisticsCalculationFailure failure))
        {
            if (!_reportedInvalidRuntimeStatistics)
            {
                Debug.LogError(
                    $"{nameof(PlayerCharacter)} could not derive runtime statistics. Failure={failure}.",
                    this);
                _reportedInvalidRuntimeStatistics = true;
            }

            return false;
        }

        _cachedRuntimeStatistics = statistics;
        _cachedRuntimeAttributeRevision = attributeRevision;
        _cachedRuntimeEquipmentRevision = equipmentRevision;
        _hasCachedRuntimeStatistics = true;
        _reportedInvalidRuntimeStatistics = false;
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _participantLink == null ||
            !_participantLink.TryGetCharacterAttributeRevision(out int revision) ||
            _equipmentController == null)
        {
            return;
        }

        int equipmentRevision = _equipmentController.ObservedEquipmentRevision;
        int lootRevision = _lootReceiver != null ? _lootReceiver.LootChangeSequence : 0;
        if (revision == _clampedAttributeRevision &&
            equipmentRevision == _clampedEquipmentRevision &&
            lootRevision == _clampedLootRevision)
        {
            return;
        }

        ClampCurrentHealthToMaximum(ResolveMaximumHealth());
        _clampedAttributeRevision = revision;
        _clampedEquipmentRevision = equipmentRevision;
        _clampedLootRevision = lootRevision;
    }

    protected override float CalculateMitigatedDamage(in DamageRequest request)
    {
        if (request.DamageType == DamageType.TrueDamage)
        {
            return request.Amount;
        }

        bool usesPhysicalDefense;
        switch (request.DamageType)
        {
            case DamageType.Physical:
                usesPhysicalDefense = true;
                break;
            case DamageType.Magical:
                usesPhysicalDefense = false;
                break;
            default:
                return request.Amount;
        }

        float mitigatedDamage = request.Amount;
        if (TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics))
        {
            int defense = usesPhysicalDefense
                ? statistics.PhysicalDefense
                : statistics.MagicalDefense;
            if (EquipmentDamageMitigationCalculator.TryCalculate(
                    request.Amount,
                    defense,
                    _defenseMitigationConstant,
                    out float armorMitigatedDamage))
            {
                mitigatedDamage = armorMitigatedDamage;
            }
            else if (!_reportedInvalidMitigation)
            {
                Debug.LogError(
                    $"{nameof(PlayerCharacter)} could not calculate Equipment damage mitigation.",
                    this);
                _reportedInvalidMitigation = true;
            }
        }

        float finalMitigated = _shieldDefenseController != null &&
            _shieldDefenseController.TryMitigateDamage(request, mitigatedDamage, out float shieldMitigatedDamage)
                ? shieldMitigatedDamage
                : mitigatedDamage;

        if (_passiveEffectsController != null && _passiveEffectsController.HasSanctuaryHeart)
        {
            finalMitigated *= _passiveEffectsController.DamageTakenMultiplier;
        }

        return finalMitigated;
    }

    protected override bool TryInterceptFatalDamage(in DamageRequest request)
    {
        return _downedStateController != null &&
            _downedStateController.TryEnterDowned(request.AttackerId);
    }

    protected override bool TryApplyAlternateDamage(
        in DamageRequest request,
        float mitigatedDamage,
        out DamageResult result)
    {
        result = default;
        if (IsDowned && request.IsAllyDirectDamage)
        {
            // No reserve mutation or NotifyDownedDamaged: revive must continue uninterrupted.
            result = new DamageResult(Id, true, 0f, Health, false, DamageFailureReason.None);
            return true;
        }

        if (!IsDowned ||
            !_downedStateController.TryApplyDownedDamage(
                mitigatedDamage,
                request.AttackerId,
                out float applied,
                out bool depleted))
        {
            return false;
        }

        result = new DamageResult(
            Id,
            true,
            applied,
            Health,
            depleted,
            DamageFailureReason.None);

        if (depleted)
        {
            HandleDeath();
        }

        return true;
    }

    /// <summary>
    /// Called by the Downed controller after its reserve drained to zero and the Downed state
    /// was cleared. Resolves the definitive defeat through the existing corpse path.
    /// </summary>
    internal void ResolveDefinitiveDefeatFromDowned()
    {
        HandleDeath();
    }

    /// <summary>
    /// Forces definitive defeat of this character due to teammate absence confirmation.
    /// Authoritatively clears Downed state without consuming reserves, sets Health to 0,
    /// and triggers death/corpse conversion without applying synthetic damage or awarding Kill XP.
    /// </summary>
    internal void ForceDefinitiveDefeatFromAbsence()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (IsDowned)
        {
            _downedStateController?.ClearDownedState();
        }

        Health = 0f;
        HandleDeath();

        if (_participantLink != null && _participantLink.TryResolveParticipant(out NetworkRaidParticipant participant))
        {
            if (participant.State == RaidParticipantState.Raiding)
            {
                participant.TryMarkDefeated(Object);
            }
        }
    }

    /// <summary>
    /// Forces definitive defeat of this character due to dungeon absolute collapse.
    /// Authoritatively clears Downed state without consuming reserves, sets Health to 0,
    /// and triggers death/corpse conversion and participant defeat.
    /// </summary>
    internal void ForceDefinitiveDefeatFromCollapse()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (IsDowned)
        {
            _downedStateController?.ClearDownedState();
        }

        Health = 0f;
        HandleDeath();

        if (_participantLink != null && _participantLink.TryResolveParticipant(out NetworkRaidParticipant participant))
        {
            if (participant.State == RaidParticipantState.Raiding)
            {
                participant.TryMarkDefeated(Object);
            }
        }
    }

    /// <summary>
    /// Completes a recovery from Downed in one authoritative step: leaves Downed (reserve
    /// discarded), restores Health and empties Stamina. Returns false with no side effects when a
    /// precondition fails. Grants no invulnerability and never touches Mana.
    /// </summary>
    internal bool TryRestoreFromDowned(float restoredHealth)
    {
        if (!HasStateAuthority || !IsDowned ||
            float.IsNaN(restoredHealth) || float.IsInfinity(restoredHealth) || restoredHealth <= 0f)
        {
            return false;
        }

        if (!_downedStateController.TryExitDownedToActive())
        {
            return false;
        }

        RestoreHealthAuthoritatively(restoredHealth);
        _staminaController?.ForceDepleteForRecovery();
        return true;
    }

    /// <summary>
    /// Starts the authoritative player-corpse transaction after this character's
    /// health has transitioned to zero through the shared damage pipeline.
    /// </summary>
    protected override void HandleDeath()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (_corpseGenerationController == null)
        {
            Debug.LogError(
                $"{nameof(PlayerCharacter)} requires {nameof(PlayerCorpseGenerationController)} on the player object.",
                this);
            return;
        }

        bool corpseReady = _corpseGenerationController.TryConvertInventoryToCorpseLoot(Runner.Tick);
        if (corpseReady)
        {
            _participantLink?.NotifyCorpseConversionCompleted();
        }

        var spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        spawnManager?.NotifyPendingReconnectCharacterDefeated(Object);
    }

    private void CacheDependencies()
    {
        _consumableController = GetComponent<PlayerConsumableNetworkController>();
        if (_corpseGenerationController == null)
        {
            _corpseGenerationController = GetComponent<PlayerCorpseGenerationController>();
        }

        if (_extractionController == null)
        {
            _extractionController = GetComponent<PlayerExtractionController>();
        }

        if (_participantLink == null)
        {
            _participantLink = GetComponent<RaidAvatarParticipantLink>();
        }

        if (_equipmentController == null)
        {
            _equipmentController = GetComponent<PlayerWeaponEquipmentNetworkController>();
        }

        if (_shieldDefenseController == null)
        {
            _shieldDefenseController = GetComponent<PlayerShieldDefenseNetworkController>();
        }

        if (_downedStateController == null)
        {
            _downedStateController = GetComponent<PlayerDownedStateNetworkController>();
        }

        if (_staminaController == null)
        {
            _staminaController = GetComponent<PlayerStaminaNetworkController>();
        }

        if (_manaController == null)
        {
            _manaController = GetComponent<PlayerManaNetworkController>();
        }

        if (_passiveEffectsController == null)
        {
            _passiveEffectsController = GetComponent<PlayerInventoryPassiveEffectsController>();
            if (_passiveEffectsController == null)
            {
                _passiveEffectsController = gameObject.AddComponent<PlayerInventoryPassiveEffectsController>();
            }
        }

        if (_lootReceiver == null)
        {
            _lootReceiver = GetComponent<PlayerLootReceiver>();
        }
    }

    private bool TryGetEquipmentStatistics(
        out EquipmentStatisticsModifiers statistics,
        out int revision)
    {
        statistics = default;
        revision = 0;
        if (_equipmentController == null)
        {
            return false;
        }

        revision = _equipmentController.ObservedEquipmentRevision;
        if (_hasCachedEquipmentStatistics && _cachedEquipmentRevision == revision)
        {
            statistics = _cachedEquipmentStatistics;
            return true;
        }

        EquipmentStatisticsCalculationFailure failure =
            EquipmentStatisticsCalculationFailure.InvalidArmorDefinition;
        if (!TryResolveArmorDefinition(EquipmentSlot.Helmet, out ArmorDefinition helmet) ||
            !TryResolveArmorDefinition(EquipmentSlot.Armor, out ArmorDefinition armor) ||
            !TryResolveArmorDefinition(EquipmentSlot.Gloves, out ArmorDefinition gloves) ||
            !TryResolveArmorDefinition(EquipmentSlot.Boots, out ArmorDefinition boots) ||
            !EquipmentStatisticsCalculator.TryCalculate(
                helmet,
                armor,
                gloves,
                boots,
                out statistics,
                out failure))
        {
            if (!_reportedInvalidEquipmentStatistics)
            {
                Debug.LogError(
                    $"{nameof(PlayerCharacter)} could not project Equipment statistics. " +
                    $"Failure={failure}.",
                    this);
                _reportedInvalidEquipmentStatistics = true;
            }

            return false;
        }

        _cachedEquipmentStatistics = statistics;
        _cachedEquipmentRevision = revision;
        _hasCachedEquipmentStatistics = true;
        _reportedInvalidEquipmentStatistics = false;
        return true;
    }

    private bool TryResolveArmorDefinition(EquipmentSlot slot, out ArmorDefinition armorDefinition)
    {
        armorDefinition = null;
        if (!_equipmentController.IsSlotOccupied(slot))
        {
            return true;
        }

        if (!_equipmentController.TryGetSlotDefinition(slot, out LootDefinition lootDefinition) ||
            lootDefinition?.ArmorDefinition == null)
        {
            return false;
        }

        armorDefinition = lootDefinition.ArmorDefinition;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
