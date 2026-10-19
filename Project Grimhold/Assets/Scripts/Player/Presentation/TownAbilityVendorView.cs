using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Passive local Canvas of the Ability Vendor: the offer rows, the confirmed gold, a feedback line and
/// a close button. It never reads or writes the profile; the presenter pushes everything through
/// <see cref="Present"/> and receives only intentions through <see cref="BuyRequested"/> and
/// <see cref="CloseRequested"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownAbilityVendorView : MonoBehaviour
{
    private const int OverlaySortingOrder = 110;

    [SerializeField] private RectTransform _rowContainer;
    [SerializeField] private TownAbilityVendorRowView _rowPrefab;
    [SerializeField] private TMP_Text _goldText;
    [SerializeField] private TMP_Text _feedbackText;
    [SerializeField] private TMP_Text _noticeText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private GameObject _emptyNote;

    private readonly List<TownAbilityVendorRowView> _rows = new();
    private bool _initialized;
    private bool _isConfigured;

    public bool IsOpen => UiPanelVisibility.IsVisible(gameObject);
    public IReadOnlyList<TownAbilityVendorRowView> Rows => _rows;
    public TMP_Text GoldText => _goldText;
    public TMP_Text FeedbackText => _feedbackText;
    public TMP_Text NoticeText => _noticeText;

    public event Action<AbilityId> BuyRequested;
    public event Action CloseRequested;

    /// <summary>Instantiates the view under <paramref name="parent"/> as a closed overlay, or returns null when misconfigured.</summary>
    public static TownAbilityVendorView Create(Transform parent, TownAbilityVendorView prefab)
    {
        if (parent == null || prefab == null)
        {
            return null;
        }

        TownAbilityVendorView view = Instantiate(prefab, parent, false);
        view.name = prefab.name;
        if (!view.Initialize())
        {
            Destroy(view.gameObject);
            return null;
        }

        Canvas canvas = view.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;
        }

        view.gameObject.SetActive(false);
        return view;
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        for (int index = 0; index < _rows.Count; index++)
        {
            if (_rows[index] != null)
            {
                _rows[index].BuyRequested -= OnRowBuyRequested;
            }
        }

        _rows.Clear();
    }

    public void Open() => UiPanelVisibility.SetVisible(gameObject, true);

    public void Close() => UiPanelVisibility.SetVisible(gameObject, false);

    /// <summary>Shows one row per entry in the given order and the confirmed gold.</summary>
    public void Present(IReadOnlyList<AbilityVendorEntry> entries, long currency)
    {
        if (!_isConfigured)
        {
            return;
        }

        int count = entries != null ? entries.Count : 0;
        EnsureRows(count);
        for (int index = 0; index < _rows.Count; index++)
        {
            bool used = index < count;
            _rows[index].gameObject.SetActive(used);
            if (used)
            {
                _rows[index].Present(entries[index]);
            }
        }

        if (_emptyNote != null)
        {
            _emptyNote.SetActive(count == 0);
        }

        string notice = AbilityVendorText.Notice(entries);
        _noticeText.text = notice;
        _noticeText.gameObject.SetActive(notice.Length > 0);

        _goldText.text = AbilityVendorText.Gold(currency);
    }

    public void ShowFeedback(string message)
    {
        if (_isConfigured)
        {
            _feedbackText.text = message ?? string.Empty;
        }
    }

    /// <summary>Plays the optional feedback pulse for a confirmed purchase result.</summary>
    public void PulseFeedback(UiPulseKind kind)
    {
        if (_isConfigured)
        {
            UiFeedbackPulse.Play(_feedbackText.gameObject, kind);
        }
    }

    private bool Initialize()
    {
        if (_initialized)
        {
            return _isConfigured;
        }

        _initialized = true;
        if (!TryValidate(out string error))
        {
            Debug.LogError(error, this);
            return false;
        }

        _closeButton.onClick.AddListener(OnCloseClicked);
        _isConfigured = true;
        return true;
    }

    private bool TryValidate(out string error)
    {
        if (_rowContainer == null) return Fail(nameof(_rowContainer), out error);
        if (_rowPrefab == null) return Fail(nameof(_rowPrefab), out error);
        if (_goldText == null) return Fail(nameof(_goldText), out error);
        if (_feedbackText == null) return Fail(nameof(_feedbackText), out error);
        if (_noticeText == null) return Fail(nameof(_noticeText), out error);
        if (_closeButton == null) return Fail(nameof(_closeButton), out error);

        if (!_rowPrefab.TryValidate(out string rowError))
        {
            error = $"{nameof(TownAbilityVendorView)} '{name}' has an invalid row prefab: {rowError}";
            return false;
        }

        error = null;
        return true;
    }

    private bool Fail(string fieldName, out string error)
    {
        error = $"{nameof(TownAbilityVendorView)} '{name}' is missing serialized reference '{fieldName}'.";
        return false;
    }

    private void EnsureRows(int count)
    {
        while (_rows.Count < count)
        {
            TownAbilityVendorRowView row = Instantiate(_rowPrefab, _rowContainer, false);
            row.BuyRequested += OnRowBuyRequested;
            _rows.Add(row);
        }
    }

    private void OnRowBuyRequested(AbilityId abilityId) => BuyRequested?.Invoke(abilityId);

    private void OnCloseClicked() => CloseRequested?.Invoke();
}
