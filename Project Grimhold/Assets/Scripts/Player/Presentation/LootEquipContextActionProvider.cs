using System.Collections.Generic;

/// <summary>
/// Exposes the equip intention through the Raid inventory context menu for every equippable
/// category. Weapons target the next free quick slot; armor targets its fixed slot.
/// </summary>
public sealed class LootEquipContextActionProvider : ILootContextActionProvider
{
    public static readonly LootContextActionId EquipId = new("equipment.equip");

    private PlayerWeaponEquipmentNetworkController _controller;

    public void Bind(PlayerWeaponEquipmentNetworkController controller)
    {
        _controller = controller;
    }

    public void CollectActions(
        in LootContextActionContext context,
        List<LootContextActionDescriptor> actions)
    {
        if (!IsValidEquipment(context) || actions == null)
        {
            return;
        }

        bool enabled = _controller != null && !_controller.HasRequestInFlight &&
            _controller.CanEquip(context.Entry.LootId);
        actions.Add(new LootContextActionDescriptor(EquipId, "Equipar", enabled, this));
    }

    public bool TryExecute(
        LootContextActionId actionId,
        in LootContextActionContext context)
    {
        return actionId == EquipId && IsValidEquipment(context) && _controller != null &&
            !_controller.HasRequestInFlight &&
            _controller.TryRequestEquip(context.Entry.LootId);
    }

    /// <summary>
    /// Loot only classifies the unit; whether the category is equippable is an Equipment rule.
    /// Weapons additionally need a usable <see cref="WeaponDefinition"/> to reach combat.
    /// </summary>
    private static bool IsValidEquipment(in LootContextActionContext context)
    {
        if (!context.IsValid || !EquipmentSlotRules.IsEquippableCategory(context.Definition.Category))
        {
            return false;
        }

        return context.Definition.Category != LootCategory.Weapon ||
            (context.Definition.WeaponDefinition != null &&
                context.Definition.WeaponDefinition.TryValidate(out _));
    }
}
