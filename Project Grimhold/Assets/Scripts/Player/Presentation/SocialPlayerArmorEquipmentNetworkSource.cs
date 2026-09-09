using Fusion;
using UnityEngine;

/// <summary>
/// Replicates only the visible body Equipment prepared by this SocialPlayer in Town.
/// Weapon assignments remain in the persistent Loadout but are deliberately excluded so Town
/// cannot present a held weapon or acquire any weapon-selection/combat behaviour.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class SocialPlayerArmorEquipmentNetworkSource : NetworkBehaviour, IEquipmentVisualSource
{
    [SerializeField] private LootDefinitionCatalog _lootCatalog;

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
        ApplyPreparedArmor(_profileContext.LoadoutService.GetPreparedEquipment(_profileId));
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
        if (Object == null || !Object.IsValid || !EquipmentSlotRules.IsArmorSlot(slot))
        {
            return false;
        }

        int catalogIndex = GetCatalogIndexPlusOne(slot) - 1;
        return catalogIndex >= 0 && _lootCatalog != null &&
            _lootCatalog.TryGetByIndex(catalogIndex, out definition) && definition != null;
    }

    private void ApplyPreparedArmor(in PreparedEquipmentLoadout prepared)
    {
        int helmet = ResolveCatalogIndexPlusOne(prepared.Helmet, EquipmentSlot.Helmet);
        int armor = ResolveCatalogIndexPlusOne(prepared.Armor, EquipmentSlot.Armor);
        int gloves = ResolveCatalogIndexPlusOne(prepared.Gloves, EquipmentSlot.Gloves);
        int boots = ResolveCatalogIndexPlusOne(prepared.Boots, EquipmentSlot.Boots);
        if (HelmetCatalogIndexPlusOne == helmet && ArmorCatalogIndexPlusOne == armor &&
            GlovesCatalogIndexPlusOne == gloves && BootsCatalogIndexPlusOne == boots)
        {
            return;
        }

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
