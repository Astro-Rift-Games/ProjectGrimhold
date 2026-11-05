using System.Collections.Generic;

/// <summary>Town-only action on the captured gem stack; Equipment and Raid never bind this provider.</summary>
public sealed class TownLootGemInstallContextActionProvider : ILootContextActionProvider
{
    private static readonly LootContextActionId InstallId = new("town.gem.install");
    private TownGemInstallSession _session;
    private InventoryList _list;
    private int _slot;
    public void Bind(TownGemInstallSession session, InventoryList list, int slot)
    { _session = session; _list = list; _slot = slot; }
    public void CollectActions(in LootContextActionContext context, List<LootContextActionDescriptor> actions)
    {
        if (actions == null || !context.IsValid || context.Definition.GemDefinition == null || _session == null) return;
        actions.Add(new LootContextActionDescriptor(InstallId, "Incrustar", !_session.LocksMutations, this));
    }
    public bool TryExecute(LootContextActionId id, in LootContextActionContext context) =>
        id == InstallId && context.IsValid && context.Definition.GemDefinition != null &&
        _session != null && !_session.LocksMutations && _session.Begin(_list, _slot);
}
