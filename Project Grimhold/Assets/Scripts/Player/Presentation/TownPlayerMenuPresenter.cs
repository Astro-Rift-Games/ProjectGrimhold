using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the Town tabbed player menu: the open/close and tab-switch hotkeys, the single
/// input-suppression token and the Escape handling. Features plug in through
/// <see cref="RegisterTab"/>; they keep ownership of their own content and state.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownPlayerMenuPresenter : MonoBehaviour
{
    [SerializeField] private TownPlayerMenuView _viewPrefab;
    [SerializeField] private GameObject[] _hudRoots;

    private readonly TownHudVisibility _hud = new();
    private readonly Dictionary<string, TownMenuTabRegistration> _tabs = new(StringComparer.Ordinal);
    private readonly TownMenuTabState _state = new(TownMenuTabIds.All);
    private TownPlayerMenuView _view;
    private PlayerInputReader _reader;
    private IDisposable _inputSuppression;

    public bool IsOpen => _state.IsOpen;
    public string SelectedTabId => _state.SelectedTabId;

    /// <summary>Adds or replaces a tab. Safe to call before or after <see cref="Bind"/>.</summary>
    public void RegisterTab(TownMenuTabRegistration registration)
    {
        if (registration == null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        if (_tabs.ContainsKey(registration.Id))
        {
            UnregisterTab(registration.Id);
        }

        _tabs[registration.Id] = registration;
        if (_view != null)
        {
            HostContent(registration);
            RefreshTabBar();
        }
    }

    /// <summary>Removes a tab, closing the menu first when that tab is the one being shown.</summary>
    public void UnregisterTab(string tabId)
    {
        if (string.IsNullOrEmpty(tabId) || !_tabs.TryGetValue(tabId, out TownMenuTabRegistration registration))
        {
            return;
        }

        if (_state.IsOpen && string.Equals(_state.SelectedTabId, tabId, StringComparison.Ordinal))
        {
            CloseMenu();
        }

        _tabs.Remove(tabId);
        if (_view != null)
        {
            RefreshTabBar();
        }
    }

    /// <summary>Hides this HUD root while the menu is open. Safe to call at any time.</summary>
    public void RegisterHud(GameObject hudRoot) => _hud.Register(hudRoot);

    public void UnregisterHud(GameObject hudRoot) => _hud.Unregister(hudRoot);

    private void Awake()
    {
        if (_hudRoots == null)
        {
            return;
        }

        foreach (GameObject hudRoot in _hudRoots)
        {
            _hud.Register(hudRoot);
        }
    }

    /// <summary>Starts listening to the reader's hotkeys and creates the window under the UI parent.</summary>
    public void Bind(PlayerInputReader reader, Transform uiParent)
    {
        Unbind();
        if (reader == null || uiParent == null)
        {
            return;
        }

        if (_view == null)
        {
            if (_viewPrefab == null)
            {
                Debug.LogError($"{nameof(TownPlayerMenuPresenter)} is missing its serialized view prefab.", this);
                return;
            }

            _view = Instantiate(_viewPrefab, uiParent, false);
            _view.name = _viewPrefab.name;
            _view.transform.SetAsFirstSibling();
            _view.SetOpen(false);
            _view.TabSelected += OnTabSelected;
            _view.CloseRequested += OnCloseButton;
            foreach (TownMenuTabRegistration registration in _tabs.Values)
            {
                HostContent(registration);
            }

            RefreshTabBar();
        }

        _reader = reader;
        _reader.InventoryToggleRequested += OnInventoryToggleRequested;
        _reader.AttributesToggleRequested += OnAttributesToggleRequested;
        _reader.InventoryCloseRequested += OnCloseRequested;
        _reader.MenuToggleRequested += OnMenuToggleRequested;
    }

    /// <summary>Stops listening, closes the menu and releases input suppression.</summary>
    public void Unbind()
    {
        if (_reader != null)
        {
            _reader.InventoryToggleRequested -= OnInventoryToggleRequested;
            _reader.AttributesToggleRequested -= OnAttributesToggleRequested;
            _reader.InventoryCloseRequested -= OnCloseRequested;
            _reader.MenuToggleRequested -= OnMenuToggleRequested;
        }

        CloseMenu();
        _reader = null;
    }

    private void OnDestroy()
    {
        Unbind();
        if (_view != null)
        {
            _view.TabSelected -= OnTabSelected;
            _view.CloseRequested -= OnCloseButton;
            Destroy(_view.gameObject);
            _view = null;
        }
    }

    private void OnInventoryToggleRequested() => TryOpenTab(TownMenuTabIds.Inventory);

    private void OnAttributesToggleRequested() => TryOpenTab(TownMenuTabIds.Attributes);

    private bool OnCloseRequested() => CloseMenu();

    /// <summary>
    /// Raised by the reader only when no <c>InventoryCloseRequested</c> handler consumed Escape, so
    /// reaching it means no panel was open: Escape then opens the Options tab.
    /// </summary>
    private void OnMenuToggleRequested() => TryOpenTab(TownMenuTabIds.Options);

    private void OnCloseButton() => CloseMenu();

    private void OnTabSelected(string tabId)
    {
        if (!_tabs.ContainsKey(tabId))
        {
            return;
        }

        string previous = _state.SelectedTabId;
        if (_state.Select(tabId))
        {
            HideTab(previous);
            _view.SetSelected(tabId);
            ShowTab(tabId);
        }
    }

    private void TryOpenTab(string tabId)
    {
        if (_view == null || _reader == null || !_tabs.ContainsKey(tabId))
        {
            return;
        }

        string previous = _state.SelectedTabId;
        if (!_state.IsOpen && _reader.IsGameplayInputSuppressed)
        {
            return;
        }

        switch (_state.Open(tabId))
        {
            case TownMenuTabTransition.Opened:
                _inputSuppression = _reader.AcquireGameplayInputSuppression();
                _hud.SetHidden(true);
                _view.SetOpen(true);
                _view.SetSelected(tabId);
                ShowTab(tabId);
                break;
            case TownMenuTabTransition.Switched:
                HideTab(previous);
                _view.SetSelected(tabId);
                ShowTab(tabId);
                break;
            case TownMenuTabTransition.Closed:
                HideTab(tabId);
                _view.SetOpen(false);
                _hud.SetHidden(false);
                ReleaseSuppression();
                break;
        }
    }

    private bool CloseMenu()
    {
        if (!_state.Close())
        {
            return false;
        }

        HideTab(_state.SelectedTabId);
        _view?.SetOpen(false);
        _hud.SetHidden(false);
        ReleaseSuppression();
        return true;
    }

    private void ShowTab(string tabId)
    {
        if (_tabs.TryGetValue(tabId, out TownMenuTabRegistration registration))
        {
            registration.Shown?.Invoke();
        }
    }

    private void HideTab(string tabId)
    {
        if (_tabs.TryGetValue(tabId, out TownMenuTabRegistration registration))
        {
            registration.Hidden?.Invoke();
        }
    }

    private void ReleaseSuppression()
    {
        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

    private void RefreshTabBar()
    {
        var tabs = new List<KeyValuePair<string, string>>();
        foreach (string id in TownMenuTabIds.All)
        {
            if (_tabs.TryGetValue(id, out TownMenuTabRegistration registration))
            {
                tabs.Add(new KeyValuePair<string, string>(id, registration.Label));
            }
        }

        _view.SetTabs(tabs);
        _view.SetSelected(_state.SelectedTabId);
    }

    /// <summary>
    /// Reparents the tab content under the window and stretches it. Content is never moved back: it
    /// belongs to the same hierarchy as the window and is destroyed with it, and moving it during
    /// teardown would target a parent that is already being destroyed.
    /// </summary>
    private void HostContent(TownMenuTabRegistration registration)
    {
        RectTransform content = registration.Content;
        if (content == null || _view == null || _view.ContentRoot == null)
        {
            return;
        }

        content.SetParent(_view.ContentRoot, false);
        // A prefab whose root is a Canvas can be saved with a zero scale (Unity drives it while it is a
        // root canvas); nothing restores it once the content lives under the window.
        content.localScale = Vector3.one;
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
    }
}
