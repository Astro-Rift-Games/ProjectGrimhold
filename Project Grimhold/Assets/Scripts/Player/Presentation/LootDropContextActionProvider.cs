using System;
using System.Collections.Generic;

/// <summary>
/// Adapts the supported inventory drop intentions to the authoritative drop controller.
/// A partial drop is handed to the owner, which selects the exact amount before requesting it.
/// </summary>
public sealed class LootDropContextActionProvider : ILootContextActionProvider
{
    public static readonly LootContextActionId DropSingleId = new("drop.single");
    public static readonly LootContextActionId DropAllId = new("drop.all");
    public static readonly LootContextActionId DropAmountId = new("drop.amount");

    private Func<LootContextActionContext, bool> _partialDropHandler;

    private PlayerLootDropNetworkController _controller;

    public void Bind(PlayerLootDropNetworkController controller)
    {
        _controller = controller;
    }

    /// <summary>Owner seam for "Soltar cantidad...": opens the quantity selector and reports whether it did.</summary>
    public void SetPartialDropHandler(Func<LootContextActionContext, bool> handler)
    {
        _partialDropHandler = handler;
    }

    public void CollectActions(
        in LootContextActionContext context,
        List<LootContextActionDescriptor> actions)
    {
        if (!context.IsValid || actions == null)
        {
            return;
        }

        bool enabled = _controller != null && !_controller.HasRequestInFlight;
        actions.Add(new LootContextActionDescriptor(
            DropSingleId,
            "Soltar",
            enabled,
            this));
        actions.Add(new LootContextActionDescriptor(
            DropAmountId,
            "Soltar cantidad...",
            enabled && CanSelectPartialAmount(context),
            this));
        actions.Add(new LootContextActionDescriptor(
            DropAllId,
            "Soltar todo",
            enabled,
            this));
    }

    public bool TryExecute(
        LootContextActionId actionId,
        in LootContextActionContext context)
    {
        if (_controller == null || _controller.HasRequestInFlight || !context.IsValid)
        {
            return false;
        }

        if (actionId == DropAmountId)
        {
            return CanSelectPartialAmount(context) && _partialDropHandler != null && _partialDropHandler(context);
        }

        if (actionId != DropSingleId && actionId != DropAllId) return false;
        LootTransferQuantityMode mode = actionId == DropSingleId
            ? LootTransferQuantityMode.SingleUnit : LootTransferQuantityMode.FullStack;
        return context.HasInventorySlotAddress
            ? _controller.TryRequestDrop(context.Entry.LootId, mode,
                context.SourceInventorySlotIndex, context.ExpectedInventoryRevision)
            : _controller.TryRequestDrop(context.Entry.LootId, mode);
    }

    private static bool CanSelectPartialAmount(in LootContextActionContext context) =>
        context.HasInventorySlotAddress && QuantitySelectorRange.TryCreate(context.Entry.Amount, out _);
}
