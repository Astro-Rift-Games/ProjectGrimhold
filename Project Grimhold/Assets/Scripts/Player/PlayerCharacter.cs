using UnityEngine;
using Fusion;

/// <summary>
/// Concrete network character implementation used by player-controlled entities.
/// Character identity, health and damage behavior are inherited from CharacterBase.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCharacter : CharacterBase
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

    [SerializeField, Min(0.0001f)]
    private float _defenseMitigationConstant = 100f;

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
        if (TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics))
        {
            return statistics.MaximumHealth;
        }

        return base.ResolveMaximumHealth();
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
        if (revision == _clampedAttributeRevision &&
            equipmentRevision == _clampedEquipmentRevision)
        {
            return;
        }

        if (!TryGetRuntimeStatistics(out PlayerRuntimeStatistics statistics))
        {
            return;
        }

        ClampCurrentHealthToMaximum(statistics.MaximumHealth);
        _clampedAttributeRevision = revision;
        _clampedEquipmentRevision = equipmentRevision;
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

        return _shieldDefenseController != null &&
            _shieldDefenseController.TryMitigateDamage(request, mitigatedDamage, out float shieldMitigatedDamage)
                ? shieldMitigatedDamage
                : mitigatedDamage;
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
