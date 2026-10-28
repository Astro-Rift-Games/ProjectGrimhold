using System;

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

        int existingSlot = current.FindSlot(lootId);
        if (existingSlot >= 0)
        {
            if (existingSlot == slotIndex)
            {
                return QuickSlotAssignmentResult.Unchanged;
            }

            return QuickSlotAssignmentResult.AlreadyAssigned;
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
    /// Evaluates slot depletion. Under Doc 14 §12.2 (TASK-230), reaching zero units retains the slot
    /// assignment in an inactive state instead of clearing it, so this always returns false.
    /// </summary>
    [Obsolete("Depleted slots are retained with count 0 and inactive state (Doc 14 TASK-230).")]
    public static bool TryReleaseDepleted(
        QuickSlotBindings current,
        int[] previousAmounts,
        int[] currentAmounts,
        out QuickSlotBindings next)
    {
        next = current;
        return false;
    }
}
