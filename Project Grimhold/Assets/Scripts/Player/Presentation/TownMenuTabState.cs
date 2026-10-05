using System;
using System.Collections.Generic;

/// <summary>Outcome of a hotkey-style request to open a Town menu tab.</summary>
public enum TownMenuTabTransition
{
    Ignored,
    Opened,
    Switched,
    Closed
}

/// <summary>Pure open/closed and selected-tab state for the Town tabbed player menu.</summary>
public sealed class TownMenuTabState
{
    private readonly string[] _tabIds;
    private string _selectedTabId;

    public TownMenuTabState(IReadOnlyList<string> tabIds)
    {
        if (tabIds == null)
        {
            throw new ArgumentNullException(nameof(tabIds));
        }

        if (tabIds.Count == 0)
        {
            throw new ArgumentException("At least one tab is required.", nameof(tabIds));
        }

        _tabIds = new string[tabIds.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < tabIds.Count; i++)
        {
            var id = tabIds[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Tab ids must not be blank.", nameof(tabIds));
            }

            if (!seen.Add(id))
            {
                throw new ArgumentException($"Duplicate tab id '{id}'.", nameof(tabIds));
            }

            _tabIds[i] = id;
        }

        _selectedTabId = _tabIds[0];
    }

    public bool IsOpen { get; private set; }

    public string SelectedTabId => _selectedTabId;

    /// <summary>
    /// Opens the menu on the tab, switches to it when another tab is open, or closes the menu
    /// when that tab is already showing (hotkey toggle).
    /// </summary>
    public TownMenuTabTransition Open(string tabId)
    {
        if (!Contains(tabId))
        {
            return TownMenuTabTransition.Ignored;
        }

        if (!IsOpen)
        {
            _selectedTabId = tabId;
            IsOpen = true;
            return TownMenuTabTransition.Opened;
        }

        if (string.Equals(_selectedTabId, tabId, StringComparison.Ordinal))
        {
            IsOpen = false;
            return TownMenuTabTransition.Closed;
        }

        _selectedTabId = tabId;
        return TownMenuTabTransition.Switched;
    }

    /// <summary>Switches tab while open (tab button click). Returns true only when the selection changed.</summary>
    public bool Select(string tabId)
    {
        if (!IsOpen || !Contains(tabId) || string.Equals(_selectedTabId, tabId, StringComparison.Ordinal))
        {
            return false;
        }

        _selectedTabId = tabId;
        return true;
    }

    /// <summary>Closes the menu and remembers the selected tab. Returns true only when it was open.</summary>
    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        IsOpen = false;
        return true;
    }

    private bool Contains(string tabId)
    {
        if (string.IsNullOrEmpty(tabId))
        {
            return false;
        }

        for (var i = 0; i < _tabIds.Length; i++)
        {
            if (string.Equals(_tabIds[i], tabId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
