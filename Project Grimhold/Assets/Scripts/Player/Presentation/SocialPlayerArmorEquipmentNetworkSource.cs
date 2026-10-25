using Fusion;
using UnityEngine;

/// <summary>
/// Replicates the full prepared Equipment for this SocialPlayer in Town.
/// The visual contract renders only body armor; weapons are exposed for read-only inspection
/// only, without held-weapon presentation or weapon-selection/combat behaviour.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class SocialPlayerArmorEquipmentNetworkSource : NetworkBehaviour, IEquipmentVisualSource
{
    [SerializeField] private LootDefinitionCatalog _lootCatalog;

    [Networked] private int WeaponSetAMainHandCatalogIndexPlusOne { get; set; }
    [Networked] private int WeaponSetAOffHandCatalogIndexPlusOne { get; set; }
    [Networked] private int WeaponSetBMainHandCatalogIndexPlusOne { get; set; }
    [Networked] private int WeaponSetBOffHandCatalogIndexPlusOne { get; set; }
    [Networked] private int HelmetCatalogIndexPlusOne { get; set; }
    [Networked] private int ArmorCatalogIndexPlusOne { get; set; }
    [Networked] private int GlovesCatalogIndexPlusOne { get; set; }
    [Networked] private int BootsCatalogIndexPlusOne { get; set; }
    [Networked] private int EquipmentRevision { get; set; }

    private ApplicationStashContext _profileContext;
    private ProfileId _profileId;
    private bool _refreshRequested;
    private bool _missingDependenciesReported;

    public int ObservedEquipmentRevision => Object != null && Object.IsValid
        ? EquipmentRevision
        : 0;

    public override void Spawned()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        LocalPlayerJoinContext joinContext = Runner.GetComponent<LocalPlayerJoinContext>();
        _profileContext = FindAnyObjectByType<ApplicationStashContext>();
        _profileId = joinContext != null ? joinContext.JoinData.ProfileId : default;
        if (_profileContext == null || !_profileContext.IsAvailable ||
            _profileContext.LoadoutService == null || !_profileId.IsValid ||
            _profileContext.ProfileId != _profileId || _lootCatalog == null)
        {
            ReportMissingDependencies();
            return;
        }

        _profileContext.ProfileCommitted += OnProfileCommitted;
        _refreshRequested = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !_refreshRequested || _profileContext == null)
        {
            return;
        }

        _refreshRequested = false;
        ApplyPreparedEquipment(_profileContext.LoadoutService.GetPreparedEquipment(_profileId));
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public bool TryGetSlotDefinition(EquipmentSlot slot, out LootDefinition definition)
    {
        definition = null;
        return EquipmentSlotRules.IsArmorSlot(slot) && TryGetPreparedSlotDefinition(slot, out definition);
    }

    /// <summary>Resolves replicated prepared Equipment for read-only inspection, not combat.</summary>
    public bool TryGetPreparedSlotDefinition(EquipmentSlot slot, out LootDefinition definition)
    {
        definition = null;
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        int catalogIndex = GetCatalogIndexPlusOne(slot) - 1;
        return catalogIndex >= 0 && _lootCatalog != null &&
            _lootCatalog.TryGetByIndex(catalogIndex, out definition) && definition != null;
    }

    private void ApplyPreparedEquipment(in PreparedEquipmentLoadout prepared)
    {
        int mainA = ResolveCatalogIndexPlusOne(prepared.WeaponSetAMainHand, EquipmentSlot.WeaponSetAMainHand);
        int offA = ResolveCatalogIndexPlusOne(prepared.WeaponSetAOffHand, EquipmentSlot.WeaponSetAOffHand);
        int mainB = ResolveCatalogIndexPlusOne(prepared.WeaponSetBMainHand, EquipmentSlot.WeaponSetBMainHand);
        int offB = ResolveCatalogIndexPlusOne(prepared.WeaponSetBOffHand, EquipmentSlot.WeaponSetBOffHand);
        int helmet = ResolveCatalogIndexPlusOne(prepared.Helmet, EquipmentSlot.Helmet);
        int armor = ResolveCatalogIndexPlusOne(prepared.Armor, EquipmentSlot.Armor);
        int gloves = ResolveCatalogIndexPlusOne(prepared.Gloves, EquipmentSlot.Gloves);
        int boots = ResolveCatalogIndexPlusOne(prepared.Boots, EquipmentSlot.Boots);
        if (WeaponSetAMainHandCatalogIndexPlusOne == mainA && WeaponSetAOffHandCatalogIndexPlusOne == offA &&
            WeaponSetBMainHandCatalogIndexPlusOne == mainB && WeaponSetBOffHandCatalogIndexPlusOne == offB &&
            HelmetCatalogIndexPlusOne == helmet && ArmorCatalogIndexPlusOne == armor &&
            GlovesCatalogIndexPlusOne == gloves && BootsCatalogIndexPlusOne == boots)
        {
            return;
        }

        WeaponSetAMainHandCatalogIndexPlusOne = mainA;
        WeaponSetAOffHandCatalogIndexPlusOne = offA;
        WeaponSetBMainHandCatalogIndexPlusOne = mainB;
        WeaponSetBOffHandCatalogIndexPlusOne = offB;
        HelmetCatalogIndexPlusOne = helmet;
        ArmorCatalogIndexPlusOne = armor;
        GlovesCatalogIndexPlusOne = gloves;
        BootsCatalogIndexPlusOne = boots;
        EquipmentRevision++;
    }

    private int ResolveCatalogIndexPlusOne(LootId lootId, EquipmentSlot slot)
    {
        return PreparedEquipmentLoadout.IsUsableEquipmentDefinition(lootId, slot, _lootCatalog) &&
            _lootCatalog.TryGetIndex(lootId, out int catalogIndex)
                ? catalogIndex + 1
                : 0;
    }

    private int GetCatalogIndexPlusOne(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSetAMainHand => WeaponSetAMainHandCatalogIndexPlusOne,
        EquipmentSlot.WeaponSetAOffHand => WeaponSetAOffHandCatalogIndexPlusOne,
        EquipmentSlot.WeaponSetBMainHand => WeaponSetBMainHandCatalogIndexPlusOne,
        EquipmentSlot.WeaponSetBOffHand => WeaponSetBOffHandCatalogIndexPlusOne,
        EquipmentSlot.Helmet => HelmetCatalogIndexPlusOne,
        EquipmentSlot.Armor => ArmorCatalogIndexPlusOne,
        EquipmentSlot.Gloves => GlovesCatalogIndexPlusOne,
        EquipmentSlot.Boots => BootsCatalogIndexPlusOne,
        _ => 0
    };

    private void OnProfileCommitted(ProfileId profileId)
    {
        if (profileId == _profileId)
        {
            _refreshRequested = true;
        }
    }

    private void Unbind()
    {
        if (_profileContext != null)
        {
            _profileContext.ProfileCommitted -= OnProfileCommitted;
        }

        _profileContext = null;
        _profileId = default;
        _refreshRequested = false;
    }

    private void ReportMissingDependencies()
    {
        if (_missingDependenciesReported)
        {
            return;
        }

        _missingDependenciesReported = true;
        Debug.LogError(
            $"{nameof(SocialPlayerArmorEquipmentNetworkSource)} could not bind the local prepared Equipment.",
            this);
    }
}
