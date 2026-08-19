using System.Collections.Generic;

/// <summary>
/// Exposes the next-free-slot weapon equip intention through the Raid inventory context menu.
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
        if (!IsValidWeapon(context) || actions == null)
        {
            return;
        }

        bool enabled = _controller != null && _controller.HasFreeSlot &&
            !_controller.HasRequestInFlight;
        actions.Add(new LootContextActionDescriptor(EquipId, "Equipar", enabled, this));
    }

    public bool TryExecute(
        LootContextActionId actionId,
        in LootContextActionContext context)
    {
        return actionId == EquipId && IsValidWeapon(context) && _controller != null &&
            _controller.HasFreeSlot && !_controller.HasRequestInFlight &&
            _controller.TryRequestEquip(context.Entry.LootId);
    }

    private static bool IsValidWeapon(in LootContextActionContext context)
    {
        return context.IsValid && context.Definition.Category == LootCategory.Weapon &&
            context.Definition.WeaponDefinition != null &&
            context.Definition.WeaponDefinition.TryValidate(out _);
    }
}
