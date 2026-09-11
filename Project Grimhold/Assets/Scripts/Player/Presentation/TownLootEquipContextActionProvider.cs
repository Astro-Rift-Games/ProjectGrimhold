using System.Collections.Generic;

/// <summary>Offers explicit persistent Equipment destinations enabled by the Town capability.</summary>
public sealed class TownLootEquipContextActionProvider : ILootContextActionProvider
{
    private static readonly LootContextActionId SetAMainId = new("town-equipment.set-a.main");
    private static readonly LootContextActionId SetAOffId = new("town-equipment.set-a.off");
    private static readonly LootContextActionId SetBMainId = new("town-equipment.set-b.main");
    private static readonly LootContextActionId SetBOffId = new("town-equipment.set-b.off");
    private static readonly LootContextActionId FixedSlotId = new("town-equipment.fixed-slot");

    private ITownEquipmentMutationEndpoint _endpoint;

    public void Bind(ITownEquipmentMutationEndpoint endpoint)
    {
        _endpoint = endpoint;
    }

    public void CollectActions(
        in LootContextActionContext context,
        List<LootContextActionDescriptor> actions)
    {
        if (!context.IsValid || actions == null ||
            !EquipmentSlotRules.IsEquippableCategory(context.Definition.Category))
        {
            return;
        }

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

    public bool TryExecute(
        LootContextActionId actionId,
        in LootContextActionContext context)
    {
        EquipmentSlot slot = ResolveSlot(actionId, context.Definition.Category);
        return slot != EquipmentSlot.None && context.IsValid && _endpoint != null &&
            _endpoint.TryEquip(context.Entry.LootId, slot) == StashOperationResult.Success;
    }

    private void Add(List<LootContextActionDescriptor> actions, LootContextActionId id, string label, LootId lootId, EquipmentSlot slot)
    {
        actions.Add(new LootContextActionDescriptor(
            id,
            label,
            _endpoint != null && _endpoint.CanEquip(lootId, slot),
            this));
    }

    private static EquipmentSlot ResolveSlot(LootContextActionId id, LootCategory category)
    {
        if (id == SetAMainId) return EquipmentSlot.WeaponSetAMainHand;
        if (id == SetAOffId) return EquipmentSlot.WeaponSetAOffHand;
        if (id == SetBMainId) return EquipmentSlot.WeaponSetBMainHand;
        if (id == SetBOffId) return EquipmentSlot.WeaponSetBOffHand;
        return id == FixedSlotId ? EquipmentSlotRules.ResolveFixedSlot(category) : EquipmentSlot.None;
    }
}
