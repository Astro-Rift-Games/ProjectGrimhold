using System;
using System.Collections.Generic;

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

        return _loadoutService.TryAssignPreparedEquipment(_profileId, slot, lootId);
    }

    public StashOperationResult TryUnequip(EquipmentSlot slot)
    {
        return CanMutate && EquipmentSlotRules.IsEquipmentSlot(slot)
            ? _loadoutService.TryClearPreparedEquipment(_profileId, slot)
            : StashOperationResult.InvalidInventory;
    }

    private bool TryResolveDefinition(LootId lootId, out LootDefinition definition)
    {
        definition = null;
        return lootId.IsValid && _lootCatalog.TryGet(lootId.Value, out definition) &&
            definition != null && EquipmentSlotRules.IsEquippableCategory(definition.Category) &&
            definition.TryValidate(out _);
    }
}
