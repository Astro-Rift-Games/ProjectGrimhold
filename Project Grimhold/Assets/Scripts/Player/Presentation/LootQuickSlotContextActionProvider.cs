using System.Collections.Generic;

/// <summary>
/// Offers "assign to Quick Slot N" for Quick Slot compatible definitions. Assigning only binds the
/// definition; it never changes the items, so it stays available whatever the equipment gate says.
/// </summary>
public sealed class LootQuickSlotContextActionProvider : ILootContextActionProvider
{
    private const string AssignPrefix = "quickslot.assign.";

    private QuickSlotController _controller;
    private System.Func<bool> _canMutate;

    public void Bind(QuickSlotController controller, System.Func<bool> canMutate)
    {
        _controller = controller;
        _canMutate = canMutate;
    }

    public void Clear()
    {
        _controller = null;
        _canMutate = null;
    }

    public void CollectActions(
        in LootContextActionContext context,
        List<LootContextActionDescriptor> actions)
    {
        if (!context.IsValid || actions == null || _controller == null ||
            !context.Definition.QuickSlotCompatible)
        {
            return;
        }

        bool enabled = _canMutate == null || _canMutate();
        QuickSlotBindings bindings = _controller.Bindings;
        for (int slot = 0; slot < QuickSlotBindings.SlotCount; slot++)
        {
            if (bindings.Get(slot).Equals(context.Entry.LootId))
            {
                continue;
            }

            actions.Add(new LootContextActionDescriptor(
                new LootContextActionId(AssignPrefix + slot),
                $"Asignar a Quick Slot {slot + 1}",
                enabled,
                this));
        }
    }

    public bool TryExecute(
        LootContextActionId actionId,
        in LootContextActionContext context)
    {
        if (_controller == null || !context.IsValid || (_canMutate != null && !_canMutate()) ||
            !TryParseSlot(actionId, out int slot))
        {
            return false;
        }

        QuickSlotAssignmentResult result = _controller.TryAssign(slot, context.Entry.LootId);
        return result == QuickSlotAssignmentResult.Success || result == QuickSlotAssignmentResult.Unchanged;
    }

    private static bool TryParseSlot(LootContextActionId actionId, out int slot)
    {
        slot = -1;
        string value = actionId.Value;
        return value != null && value.StartsWith(AssignPrefix, System.StringComparison.Ordinal) &&
            int.TryParse(value.Substring(AssignPrefix.Length), out slot) &&
            QuickSlotBindings.IsValidSlotIndex(slot);
    }
}
