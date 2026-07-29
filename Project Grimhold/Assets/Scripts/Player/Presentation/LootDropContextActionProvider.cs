using System.Collections.Generic;

/// <summary>
/// Adapts the two supported inventory drop intentions to the authoritative drop controller.
/// </summary>
public sealed class LootDropContextActionProvider : ILootContextActionProvider
{
    public static readonly LootContextActionId DropSingleId = new("drop.single");
    public static readonly LootContextActionId DropAllId = new("drop.all");

    private PlayerLootDropNetworkController _controller;

    public void Bind(PlayerLootDropNetworkController controller)
    {
        _controller = controller;
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

        if (actionId == DropSingleId)
        {
            return _controller.TryRequestDrop(
                context.Entry.LootId,
                LootTransferQuantityMode.SingleUnit);
        }

        return actionId == DropAllId && _controller.TryRequestDrop(
            context.Entry.LootId,
            LootTransferQuantityMode.FullStack);
    }
}
