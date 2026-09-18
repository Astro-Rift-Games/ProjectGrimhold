using UnityEngine;

public readonly struct DragPayload
{
    public readonly LootId LootId;
    public readonly DragSlotLocation Source;
    public readonly EquipmentSlot EquipmentSlot;
    public readonly Sprite Icon;
    public readonly int Amount;
    public readonly LootCategory Category;
    public readonly WeaponHandedness WeaponHandedness;

    public bool IsValid => LootId.IsValid && Source != DragSlotLocation.None;

    public static DragPayload Empty => default;

    public DragPayload(
        LootId lootId,
        DragSlotLocation source,
        EquipmentSlot equipmentSlot,
        Sprite icon,
        int amount,
        LootCategory category,
        WeaponHandedness weaponHandedness)
    {
        LootId = lootId;
        Source = source;
        EquipmentSlot = equipmentSlot;
        Icon = icon;
        Amount = amount;
        Category = category;
        WeaponHandedness = weaponHandedness;
    }

    public static DragPayload Create(RaidInventorySlotData data, DragSlotLocation source, EquipmentSlot equipmentSlot)
    {
        return new DragPayload(
            data.LootId,
            source,
            equipmentSlot,
            data.Icon,
            data.Amount,
            data.Category,
            data.WeaponHandedness
        );
    }
}
