using System.Collections.Generic;

/// <summary>
/// Supplies only the local inventory actions applicable to a projected loot definition.
/// Future consume or equipment systems can implement this variation point without changing the menu view.
/// </summary>
public interface ILootContextActionProvider
{
    void CollectActions(
        in LootContextActionContext context,
        List<LootContextActionDescriptor> actions);

    bool TryExecute(
        LootContextActionId actionId,
        in LootContextActionContext context);
}
