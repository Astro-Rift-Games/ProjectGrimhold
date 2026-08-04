using System.Collections.Generic;

/// <summary>
/// Provee la acción de consumo en el menú contextual del inventario.
/// Depende del PlayerConsumableNetworkController para ejecutar la solicitud.
/// </summary>
public sealed class LootConsumeContextActionProvider : ILootContextActionProvider
{
    public static readonly LootContextActionId ConsumeId = new("consume.use");

    private PlayerConsumableNetworkController _controller;

    public void Bind(PlayerConsumableNetworkController controller)
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

        // Solo mostramos la acción si el objeto tiene un efecto consumible asociado
        if (context.Definition.ConsumableDefinition == null || context.Definition.ConsumableDefinition.Effect == null)
        {
            return;
        }

        bool enabled = _controller != null && !_controller.HasRequestInFlight;
        
        actions.Add(new LootContextActionDescriptor(
            ConsumeId,
            "Usar",
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

        if (actionId == ConsumeId)
        {
            return _controller.TryRequestConsume(context.Entry.LootId);
        }

        return false;
    }
}
