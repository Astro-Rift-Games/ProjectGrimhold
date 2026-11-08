using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Screen-local modal that selects an exact partial amount of one stack. It owns no inventory
/// state: owners open it with the observed stack quantity and act on its confirmed amount.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuantitySelectorDialogView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private Slider _slider;
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    private QuantitySelectorRange _range;
    private bool _syncing;
    private bool _listening;

    public event Action<int> ConfirmRequested;
    public event Action CancelRequested;

    public bool IsOpen => _root != null && _root.activeSelf;
    public int SelectedAmount { get; private set; }
    public bool HasValidSelection => _range.Contains(SelectedAmount);
    public QuantitySelectorRange Range => _range;

    private void Awake()
    {
        EnsureListening();
        if (_root != null && !IsOpen)
        {
            _root.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (!_listening)
        {
            return;
        }

        _confirmButton.onClick.RemoveListener(OnConfirm);
        _cancelButton.onClick.RemoveListener(OnCancel);
        _slider.onValueChanged.RemoveListener(OnSliderChanged);
        _input.onValueChanged.RemoveListener(OnInputChanged);
        _input.onEndEdit.RemoveListener(OnInputEndEdit);
        _input.onValidateInput = null;
    }

    /// <summary>Opens over the current screen for stacks of two or more units.</summary>
    public bool Open(string title, int maxAmount)
    {
        if (!EnsureListening() || !QuantitySelectorRange.TryCreate(maxAmount, out QuantitySelectorRange range))
        {
            Close();
            return false;
        }

        _range = range;
        _title.text = title ?? string.Empty;
        _syncing = true;
        _slider.wholeNumbers = true;
        _slider.minValue = QuantitySelectorRange.Min;
        _slider.maxValue = range.Max;
        _syncing = false;
        _input.characterLimit = range.Max.ToString().Length;
        Select(QuantitySelectorRange.Min);
        transform.SetAsLastSibling();
        _root.SetActive(true);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(_input.gameObject);
        }

        return true;
    }

    public void Close()
    {
        _range = default;
        SelectedAmount = 0;
        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    private bool EnsureListening()
    {
        if (_listening)
        {
            return true;
        }

        if (_root == null || _title == null || _slider == null || _input == null ||
            _confirmButton == null || _cancelButton == null)
        {
            Debug.LogError($"{nameof(QuantitySelectorDialogView)} is missing authored references.", this);
            return false;
        }

        _input.contentType = TMP_InputField.ContentType.IntegerNumber;
        _input.onValidateInput = ValidateCharacter;
        _input.onValueChanged.AddListener(OnInputChanged);
        _input.onEndEdit.AddListener(OnInputEndEdit);
        _slider.onValueChanged.AddListener(OnSliderChanged);
        _confirmButton.onClick.AddListener(OnConfirm);
        _cancelButton.onClick.AddListener(OnCancel);
        _listening = true;
        return true;
    }

    private static char ValidateCharacter(string text, int charIndex, char addedChar) =>
        QuantitySelectorRange.IsAcceptedCharacter(addedChar) ? addedChar : '\0';

    private void OnInputChanged(string text)
    {
        if (_syncing)
        {
            return;
        }

        if (_range.TryParse(text, out int amount))
        {
            SelectedAmount = amount;
            _syncing = true;
            _slider.SetValueWithoutNotify(amount);
            _syncing = false;
        }
        else
        {
            SelectedAmount = 0;
        }

        _confirmButton.interactable = HasValidSelection;
    }

    private void OnInputEndEdit(string text)
    {
        if (!HasValidSelection && _range.IsValid)
        {
            Select((int)_slider.value);
        }
    }

    private void OnSliderChanged(float value)
    {
        if (!_syncing)
        {
            Select(Mathf.RoundToInt(value));
        }
    }

    private void Select(int amount)
    {
        SelectedAmount = _range.Clamp(amount);
        _syncing = true;
        _slider.SetValueWithoutNotify(SelectedAmount);
        _input.SetTextWithoutNotify(SelectedAmount.ToString());
        _syncing = false;
        _confirmButton.interactable = HasValidSelection;
    }

    private void OnConfirm()
    {
        if (!IsOpen || !HasValidSelection)
        {
            return;
        }

        int amount = SelectedAmount;
        Close();
        ConfirmRequested?.Invoke(amount);
    }

    private void OnCancel()
    {
        if (!IsOpen)
        {
            return;
        }

        Close();
        CancelRequested?.Invoke();
    }
}
