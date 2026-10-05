using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Passive window with a tab bar and a content area for the Town player menu.</summary>
[DisallowMultipleComponent]
public sealed class TownPlayerMenuView : MonoBehaviour
{
    [SerializeField] private GameObject _window;
    [SerializeField] private RectTransform _contentRoot;
    [SerializeField] private RectTransform _tabBar;
    [SerializeField] private Button _tabButtonTemplate;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Color _selectedTabColor = new(0.85f, 0.65f, 0.25f, 1f);
    [SerializeField] private Color _normalTabColor = new(0.25f, 0.2f, 0.15f, 1f);

    private readonly Dictionary<string, Button> _tabButtons = new(StringComparer.Ordinal);
    private readonly List<Action> _unsubscribe = new();

    public event Action<string> TabSelected;
    public event Action CloseRequested;

    public RectTransform ContentRoot => _contentRoot;
    public bool IsOpen => _window != null && _window.activeSelf;

    private void Awake()
    {
        if (_tabButtonTemplate != null)
        {
            _tabButtonTemplate.gameObject.SetActive(false);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        ClearTabs();
    }

    /// <summary>Rebuilds the tab bar from the given ids and labels, in order.</summary>
    public void SetTabs(IReadOnlyList<KeyValuePair<string, string>> tabs)
    {
        ClearTabs();
        if (_tabBar == null || _tabButtonTemplate == null || tabs == null)
        {
            return;
        }

        foreach (KeyValuePair<string, string> tab in tabs)
        {
            string id = tab.Key;
            Button button = Instantiate(_tabButtonTemplate, _tabBar, false);
            button.name = $"Tab_{id}";
            button.gameObject.SetActive(true);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = tab.Value;
            }

            UnityEngine.Events.UnityAction onClick = () => TabSelected?.Invoke(id);
            button.onClick.AddListener(onClick);
            _unsubscribe.Add(() => button.onClick.RemoveListener(onClick));
            _tabButtons[id] = button;
        }
    }

    public void SetSelected(string tabId)
    {
        foreach (KeyValuePair<string, Button> entry in _tabButtons)
        {
            if (entry.Value.targetGraphic != null)
            {
                entry.Value.targetGraphic.color = string.Equals(entry.Key, tabId, StringComparison.Ordinal)
                    ? _selectedTabColor
                    : _normalTabColor;
            }
        }

        if (_titleText != null && _tabButtons.TryGetValue(tabId ?? string.Empty, out Button selected))
        {
            TMP_Text label = selected.GetComponentInChildren<TMP_Text>(true);
            _titleText.text = label != null ? label.text : string.Empty;
        }
    }

    public void SetOpen(bool open)
    {
        if (_window != null && _window.activeSelf != open)
        {
            _window.SetActive(open);
        }
    }

    private void ClearTabs()
    {
        foreach (Action unsubscribe in _unsubscribe)
        {
            unsubscribe();
        }

        _unsubscribe.Clear();
        foreach (Button button in _tabButtons.Values)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        _tabButtons.Clear();
    }

    private void OnCloseClicked() => CloseRequested?.Invoke();
}
