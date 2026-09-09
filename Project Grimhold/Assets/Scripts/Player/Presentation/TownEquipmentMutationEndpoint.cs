using System;
using System.Collections.Generic;

/// <summary>
/// Adapts Town Equipment intentions to the application Loadout service. It keeps preparation
/// locking outside persistence and resolves only the six destinations supported by the current
/// technical model.
/// </summary>
public sealed class TownEquipmentMutationEndpoint : ITownEquipmentMutationEndpoint
{
    private readonly IPlayerLoadoutService _loadoutService;
    private readonly ProfileId _profileId;
    private readonly LootDefinitionCatalog _lootCatalog;
    private readonly Func<bool> _canMutate;

    public TownEquipmentMutationEndpoint(
        IPlayerLoadoutService loadoutService,
        ProfileId profileId,
        LootDefinitionCatalog lootCatalog,
        Func<bool> canMutate)
    {
        _loadoutService = loadoutService ?? throw new ArgumentNullException(nameof(loadoutService));
        _lootCatalog = lootCatalog ?? throw new ArgumentNullException(nameof(lootCatalog));
        _canMutate = canMutate ?? throw new ArgumentNullException(nameof(canMutate));
        if (!profileId.IsValid)
        {
            throw new ArgumentException("The mutated profile must be valid.", nameof(profileId));
        }

        _profileId = profileId;
    }

    public bool CanMutate => _canMutate();

    public bool CanEquip(LootId lootId)
    {
        if (!CanMutate || !TryResolveDefinition(lootId, out _))
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

    public StashOperationResult TryEquip(LootId lootId)
    {
        if (!CanEquip(lootId) || !TryResolveDefinition(lootId, out LootDefinition definition))
        {
            return StashOperationResult.InvalidInventory;
        }

        EquipmentSlot target = ResolveTargetSlot(definition.Category);
        return target != EquipmentSlot.None
            ? _loadoutService.TryAssignPreparedEquipment(_profileId, target, lootId)
            : StashOperationResult.InvalidInventory;
    }

    public StashOperationResult TryUnequip(EquipmentSlot slot)
    {
        return CanMutate && EquipmentSlotRules.IsEquipmentSlot(slot)
            ? _loadoutService.TryClearPreparedEquipment(_profileId, slot)
            : StashOperationResult.InvalidInventory;
    }

    private EquipmentSlot ResolveTargetSlot(LootCategory category)
    {
        EquipmentSlot fixedSlot = EquipmentSlotRules.ResolveFixedSlot(category);
        if (fixedSlot != EquipmentSlot.None)
        {
            return fixedSlot;
        }

        if (category != LootCategory.Weapon)
        {
            return EquipmentSlot.None;
        }

        PreparedEquipmentLoadout prepared = _loadoutService.GetPreparedEquipment(_profileId);
        if (!prepared.HasWeaponSlot1)
        {
            return EquipmentSlot.WeaponSlot1;
        }

        return !prepared.HasWeaponSlot2
            ? EquipmentSlot.WeaponSlot2
            : EquipmentSlot.WeaponSlot1;
    }

    private bool TryResolveDefinition(LootId lootId, out LootDefinition definition)
    {
        definition = null;
        return lootId.IsValid && _lootCatalog.TryGet(lootId.Value, out definition) &&
            definition != null && EquipmentSlotRules.IsEquippableCategory(definition.Category) &&
            (definition.Category != LootCategory.Weapon ||
                definition.WeaponDefinition != null && definition.WeaponDefinition.TryValidate(out _));
    }
}
