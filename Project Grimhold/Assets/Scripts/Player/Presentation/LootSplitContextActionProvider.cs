using System;
using System.Collections.Generic;

/// <summary>Offers a partial physical-stack split without choosing or mutating quantities itself.</summary>
public sealed class LootSplitContextActionProvider : ILootContextActionProvider
{
    public static readonly LootContextActionId SplitId = new("inventory.split");
    private PlayerLootSplitNetworkController _controller;
    private Func<bool> _hasEmptySlot;
    private Func<LootContextActionContext, bool> _handler;

    public void Bind(PlayerLootSplitNetworkController controller, Func<bool> hasEmptySlot)
    {
        _controller = controller;
        _hasEmptySlot = hasEmptySlot;
    }

    public void SetSplitHandler(Func<LootContextActionContext, bool> handler) => _handler = handler;

    public void CollectActions(in LootContextActionContext context, List<LootContextActionDescriptor> actions)
    {
        if (!context.IsValid || actions == null) return;
        actions.Add(new LootContextActionDescriptor(SplitId, "Dividir", CanSplit(context), this));
    }

    public bool TryExecute(LootContextActionId actionId, in LootContextActionContext context) =>
        actionId == SplitId && CanSplit(context) && _handler != null && _handler(context);

    private bool CanSplit(in LootContextActionContext context) => context.IsValid && _controller != null &&
        !_controller.HasRequestInFlight && context.HasInventorySlotAddress &&
        !EquipmentSlotRules.IsEquippableCategory(context.Definition.Category) &&
        QuantitySelectorRange.TryCreate(context.Entry.Amount, out _) && _hasEmptySlot != null && _hasEmptySlot();
}
