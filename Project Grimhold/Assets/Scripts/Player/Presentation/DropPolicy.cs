public static class DropPolicy
{
    public static bool CanAttemptDrop(DragSlotLocation source, DragSlotLocation target, EquipmentSlot targetEquipmentSlot)
    {
        if (source == DragSlotLocation.None || target == DragSlotLocation.None) return false;

        // Intra-area drops are never supported (no slot reordering, no Equipment→Equipment).
        if (source == target) return true;

        // Inter-area drops
        return (source, target) switch
        {
            (DragSlotLocation.Inventory, DragSlotLocation.Container) => targetEquipmentSlot == EquipmentSlot.None,
            (DragSlotLocation.Container, DragSlotLocation.Inventory) => targetEquipmentSlot == EquipmentSlot.None,
            (DragSlotLocation.Stash, DragSlotLocation.Inventory) => targetEquipmentSlot == EquipmentSlot.None,
            (DragSlotLocation.Inventory, DragSlotLocation.Stash) => targetEquipmentSlot == EquipmentSlot.None,
            
            (DragSlotLocation.Inventory, DragSlotLocation.Equipment) => targetEquipmentSlot != EquipmentSlot.None,
            (DragSlotLocation.Equipment, DragSlotLocation.Inventory) => targetEquipmentSlot == EquipmentSlot.None,
            (DragSlotLocation.Stash, DragSlotLocation.Equipment) => targetEquipmentSlot != EquipmentSlot.None,
            (DragSlotLocation.Container, DragSlotLocation.Equipment) => targetEquipmentSlot != EquipmentSlot.None,
            
            _ => false
        };
    }

    public static bool IsEquipmentCompatible(LootCategory category, WeaponHandedness handedness, EquipmentSlot targetSlot)
    {
        if (!EquipmentSlotRules.IsCompatible(category, targetSlot))
        {
            return false;
        }

        if (handedness == WeaponHandedness.TwoHanded)
        {
            if (!EquipmentSlotRules.IsMainHandSlot(targetSlot))
            {
                return false;
            }
        }

        return true;
    }

    public static bool CanAttemptEquipmentDrop(in DragPayload payload, EquipmentSlot targetSlot)
    {
        if (!CanAttemptDrop(payload.Source, DragSlotLocation.Equipment, targetSlot))
        {
            return false;
        }

        return IsEquipmentCompatible(payload.Category, payload.WeaponHandedness, targetSlot);
    }
}
