using System;
using System.Collections.Generic;

/// <summary>Provides a contextual Unequip action for one explicitly anchored Equipment slot.</summary>
public sealed class EquipmentUnequipContextActionProvider : ILootContextActionProvider
{
    public static readonly LootContextActionId UnequipId = new("equipment.unequip");

    private EquipmentSlot _slot;
    private Func<EquipmentSlot, bool> _canExecute;
    private Func<EquipmentSlot, bool> _execute;

    public void Bind(
        EquipmentSlot slot,
        Func<EquipmentSlot, bool> canExecute,
        Func<EquipmentSlot, bool> execute)
    {
        _slot = slot;
        _canExecute = canExecute;
        _execute = execute;
    }

    public void Clear()
    {
        _slot = EquipmentSlot.None;
        _canExecute = null;
        _execute = null;
    }

    public void CollectActions(
        in LootContextActionContext context,
        List<LootContextActionDescriptor> actions)
    {
        if (!context.IsValid || actions == null || !EquipmentSlotRules.IsEquipmentSlot(_slot))
        {
            return;
        }

        actions.Add(new LootContextActionDescriptor(
            UnequipId,
            "Desequipar",
            _canExecute != null && _canExecute(_slot),
            this));
    }

    public bool TryExecute(
        LootContextActionId actionId,
        in LootContextActionContext context)
    {
        return actionId == UnequipId && context.IsValid && _execute != null && _execute(_slot);
    }
}
