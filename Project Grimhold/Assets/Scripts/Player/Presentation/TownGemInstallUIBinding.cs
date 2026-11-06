using System;
using System.Collections.Generic;

/// <summary>Binds existing cells and the authored modal to one screen-local gem intention.</summary>
public sealed class TownGemInstallUIBinding : IDisposable
{
    private readonly TownGemInstallSession _session;
    private readonly GemInstallConfirmationView _modal;
    private readonly List<Action> _unsubscribe = new();
    public TownGemInstallUIBinding(TownGemInstallSession session, GemInstallConfirmationView modal)
    {
        _session = session;
        _modal = modal;
        if (_modal != null)
        {
            _modal.ConfirmRequested += Confirm;
            _modal.CancelRequested += Cancel;
        }
    }
    public void BindPanel(RaidLootPanelView panel, InventoryList list, Func<int> pageStart)
    {
        if (panel == null) return;
        for (int i = 0; i < panel.AuthoredSlotCount; i++)
        {
            int index = i;
            var cell = panel.GetAuthoredSlot(i);
            if (cell == null) continue;
            Action pick = () => { if (_session.IsSelecting) _session.Choose(list, pageStart() + index); };
            cell.PrimaryClicked += pick;
            _unsubscribe.Add(() => { if (cell != null) cell.PrimaryClicked -= pick; });
        }
    }
    public void RefreshPanel(RaidLootPanelView panel, InventoryList list, int start = 0)
    {
        if (panel == null) return;
        for (int i = 0; i < panel.AuthoredSlotCount; i++)
        {
            var cell = panel.GetAuthoredSlot(i);
            if (cell == null) continue;
            bool eligible = _session.IsSelecting && _session.CanTarget(list, start + i);
            cell.SetDropHighlight(eligible ? DropHighlightState.Valid : DropHighlightState.None);
            if (_session.LocksMutations)
                cell.SetInteraction(eligible ? RaidLootSlotInteractionMode.ContextMenu : RaidLootSlotInteractionMode.ReadOnly, false);
        }
        _modal?.Present(_session);
    }
    private void Confirm() => _ = _session.ConfirmAsync();
    private void Cancel() => _session.Cancel();
    public void Dispose()
    {
        foreach (var unsubscribe in _unsubscribe) unsubscribe();
        if (_modal != null)
        {
            _modal.ConfirmRequested -= Confirm;
            _modal.CancelRequested -= Cancel;
            _modal.Present(null);
        }
    }
}
