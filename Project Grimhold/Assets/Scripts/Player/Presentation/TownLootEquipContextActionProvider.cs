using System.Collections.Generic;

/// <summary>Offers only the persistent Equip action enabled by the current Town capability.</summary>
public sealed class TownLootEquipContextActionProvider : ILootContextActionProvider
{
    private ITownEquipmentMutationEndpoint _endpoint;

    public void Bind(ITownEquipmentMutationEndpoint endpoint)
    {
        _endpoint = endpoint;
    }

    public void CollectActions(
        in LootContextActionContext context,
        List<LootContextActionDescriptor> actions)
    {
        if (!context.IsValid || actions == null ||
            !EquipmentSlotRules.IsEquippableCategory(context.Definition.Category))
        {
            return;
        }

        actions.Add(new LootContextActionDescriptor(
            LootEquipContextActionProvider.EquipId,
            "Equipar",
            _endpoint != null && _endpoint.CanEquip(context.Entry.LootId),
            this));
    }

    public bool TryExecute(
        LootContextActionId actionId,
        in LootContextActionContext context)
    {
        return actionId == LootEquipContextActionProvider.EquipId && context.IsValid &&
            _endpoint != null && _endpoint.TryEquip(context.Entry.LootId) == StashOperationResult.Success;
    }
}
