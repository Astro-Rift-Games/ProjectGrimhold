/// <summary>
/// Pure Quick Slot assignment rules. They never touch inventory contents, Origin or
/// quantities: a binding only references a loot definition.
/// </summary>
public static class QuickSlotAssignmentRules
{
    public static QuickSlotAssignmentResult TryAssign(
        QuickSlotBindings current,
        int slotIndex,
        LootId lootId,
        bool isCompatible,
        int availableAmount,
        out QuickSlotBindings next)
    {
        next = current;

        if (!QuickSlotBindings.IsValidSlotIndex(slotIndex))
        {
            return QuickSlotAssignmentResult.InvalidSlot;
        }

        if (!lootId.IsValid)
        {
            return QuickSlotAssignmentResult.InvalidItem;
        }

        if (!isCompatible)
        {
            return QuickSlotAssignmentResult.NotCompatible;
        }

        if (availableAmount <= 0)
        {
            return QuickSlotAssignmentResult.NotInInventory;
        }

        if (current.Get(slotIndex).Equals(lootId))
        {
            return QuickSlotAssignmentResult.Unchanged;
        }

        next = current.With(slotIndex, lootId);
        return QuickSlotAssignmentResult.Success;
    }

    public static QuickSlotAssignmentResult TryClear(
        QuickSlotBindings current,
        int slotIndex,
        out QuickSlotBindings next)
    {
        next = current;

        if (!QuickSlotBindings.IsValidSlotIndex(slotIndex))
        {
            return QuickSlotAssignmentResult.InvalidSlot;
        }

        if (!current.IsAssigned(slotIndex))
        {
            return QuickSlotAssignmentResult.Unchanged;
        }

        next = current.Without(slotIndex);
        return QuickSlotAssignmentResult.Success;
    }

    /// <summary>
    /// Clears every slot whose available amount dropped from positive to zero. A slot that
    /// was already at zero, or whose previous amount was never observed, is kept so a
    /// transient empty inventory (reservation, extraction, defeat) cannot erase bindings.
    /// Amounts are indexed by slot.
    /// </summary>
    public static bool TryReleaseDepleted(
        QuickSlotBindings current,
        int[] previousAmounts,
        int[] currentAmounts,
        out QuickSlotBindings next)
    {
        next = current;
        bool changed = false;

        if (previousAmounts == null || currentAmounts == null ||
            previousAmounts.Length != QuickSlotBindings.SlotCount ||
            currentAmounts.Length != QuickSlotBindings.SlotCount)
        {
            return false;
        }

        for (int i = 0; i < QuickSlotBindings.SlotCount; i++)
        {
            if (current.IsAssigned(i) && previousAmounts[i] > 0 && currentAmounts[i] <= 0)
            {
                next = next.Without(i);
                changed = true;
            }
        }

        return changed;
    }
}
