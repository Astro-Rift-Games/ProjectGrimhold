using System.Collections.Generic;

/// <summary>Exposes explicit Equipment destinations through the Raid inventory context menu.</summary>
public sealed class LootEquipContextActionProvider : ILootContextActionProvider
{
    private static readonly LootContextActionId SetAMainId = new("equipment.set-a.main");
    private static readonly LootContextActionId SetAOffId = new("equipment.set-a.off");
    private static readonly LootContextActionId SetBMainId = new("equipment.set-b.main");
    private static readonly LootContextActionId SetBOffId = new("equipment.set-b.off");
    private static readonly LootContextActionId FixedSlotId = new("equipment.fixed-slot");

    private PlayerWeaponEquipmentNetworkController _controller;

    public void Bind(PlayerWeaponEquipmentNetworkController controller) => _controller = controller;

    public void CollectActions(
        in LootContextActionContext context,
        List<LootContextActionDescriptor> actions)
    {
        if (!IsValidEquipment(context) || actions == null) return;
        if (context.Definition.Category == LootCategory.Shield)
        {
            Add(actions, SetAOffId, "Equipar en Set A / Off Hand", context.Entry.LootId, EquipmentSlot.WeaponSetAOffHand);
            Add(actions, SetBOffId, "Equipar en Set B / Off Hand", context.Entry.LootId, EquipmentSlot.WeaponSetBOffHand);
            return;
        }

        if (context.Definition.Category != LootCategory.Weapon)
        {
            EquipmentSlot fixedSlot = EquipmentSlotRules.ResolveFixedSlot(context.Definition.Category);
            Add(actions, FixedSlotId, $"Equipar en {fixedSlot}", context.Entry.LootId, fixedSlot);
            return;
        }

        Add(actions, SetAMainId, "Equipar en Set A / Main Hand", context.Entry.LootId, EquipmentSlot.WeaponSetAMainHand);
        Add(actions, SetAOffId, "Equipar en Set A / Off Hand", context.Entry.LootId, EquipmentSlot.WeaponSetAOffHand);
        Add(actions, SetBMainId, "Equipar en Set B / Main Hand", context.Entry.LootId, EquipmentSlot.WeaponSetBMainHand);
        Add(actions, SetBOffId, "Equipar en Set B / Off Hand", context.Entry.LootId, EquipmentSlot.WeaponSetBOffHand);
    }

    public bool TryExecute(LootContextActionId actionId, in LootContextActionContext context)
    {
        EquipmentSlot slot = ResolveSlot(actionId, context.Definition.Category);
        return slot != EquipmentSlot.None && IsValidEquipment(context) && _controller != null &&
            !_controller.HasRequestInFlight && _controller.TryRequestEquip(context.Entry.LootId, slot);
    }

    private void Add(
        List<LootContextActionDescriptor> actions,
        LootContextActionId id,
        string label,
        LootId lootId,
        EquipmentSlot slot)
    {
        bool enabled = _controller != null && !_controller.HasRequestInFlight &&
            _controller.CanEquip(lootId, slot);
        actions.Add(new LootContextActionDescriptor(id, label, enabled, this));
    }

    private static EquipmentSlot ResolveSlot(LootContextActionId id, LootCategory category)
    {
        if (id == SetAMainId) return EquipmentSlot.WeaponSetAMainHand;
        if (id == SetAOffId) return EquipmentSlot.WeaponSetAOffHand;
        if (id == SetBMainId) return EquipmentSlot.WeaponSetBMainHand;
        if (id == SetBOffId) return EquipmentSlot.WeaponSetBOffHand;
        return id == FixedSlotId ? EquipmentSlotRules.ResolveFixedSlot(category) : EquipmentSlot.None;
    }

    private static bool IsValidEquipment(in LootContextActionContext context) =>
        context.IsValid && EquipmentSlotRules.IsEquippableCategory(context.Definition.Category) &&
        EquipmentSlotRules.IsCompatible(
            context.Definition,
            context.Definition.Category == LootCategory.Shield
                ? EquipmentSlot.WeaponSetAOffHand
                : context.Definition.Category == LootCategory.Weapon
                    ? EquipmentSlot.WeaponSetAMainHand
                    : EquipmentSlotRules.ResolveFixedSlot(context.Definition.Category));
}
