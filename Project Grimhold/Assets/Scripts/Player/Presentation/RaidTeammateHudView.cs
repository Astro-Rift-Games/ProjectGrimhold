using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the local teammate Health projection without reading gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidTeammateHudView : MonoBehaviour
{
    private const string UnavailableValue = "—";

    [SerializeField]
    private GameObject _root;

    [SerializeField]
    private TMP_Text _healthText;

    [SerializeField]
    private Image _healthFill;

    [SerializeField]
    private Button _continueWithoutTeammateButton;

    [SerializeField]
    private TMP_Text _continueWithoutTeammateButtonText;

    public event System.Action ContinueWithoutTeammateRequested;

    public GameObject Root => _root;
    public TMP_Text HealthText => _healthText;
    public Image HealthFill => _healthFill;
    public Button ContinueWithoutTeammateButton => _continueWithoutTeammateButton;

    private void Awake()
    {
        EnsureContinueButton();
    }

    public void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    public void SetContinueButtonVisible(bool visible)
    {
        EnsureContinueButton();
        if (_continueWithoutTeammateButton != null && _continueWithoutTeammateButton.gameObject.activeSelf != visible)
        {
            _continueWithoutTeammateButton.gameObject.SetActive(visible);
        }
    }

    public void PresentHealth(float currentHealth, float maximumHealth)
    {
        PresentHealth(currentHealth, maximumHealth, false, -1, false);
    }

    public void PresentHealth(
        float currentHealth,
        float maximumHealth,
        bool isDisconnected,
        int remainingBudgetSeconds,
        bool isDecisionAvailable)
    {
        float safeCurrent = IsFinite(currentHealth) ? Mathf.Max(0f, currentHealth) : 0f;
        float safeMaximum = IsFinite(maximumHealth) ? Mathf.Max(0f, maximumHealth) : 0f;

        if (isDisconnected)
        {
            if (remainingBudgetSeconds > 0)
            {
                SetText($"Compañero: {safeCurrent:0.#} / {safeMaximum:0.#} [Desconectado: {remainingBudgetSeconds}s]");
            }
            else
            {
                SetText($"Compañero: {safeCurrent:0.#} / {safeMaximum:0.#} [Desconectado]");
            }
        }
        else
        {
            SetText($"Compañero: {safeCurrent:0.#} / {safeMaximum:0.#}");
        }

        SetFill(safeMaximum > 0f ? safeCurrent / safeMaximum : 0f);
        SetContinueButtonVisible(isDecisionAvailable);
    }

    public void PresentDefeated(float maximumHealth, bool hasMaximumHealth)
    {
        PresentDefeated(maximumHealth, hasMaximumHealth, false);
    }

    public void PresentDefeated(float maximumHealth, bool hasMaximumHealth, bool isAbandoned)
    {
        if (isAbandoned)
        {
            SetText("Compañero: Abandonado");
        }
        else if (hasMaximumHealth && IsFinite(maximumHealth) && maximumHealth >= 0f)
        {
            SetText($"Compañero: 0 / {maximumHealth:0.#}");
        }
        else
        {
            SetText($"Compañero: 0 / {UnavailableValue}");
        }

        SetFill(0f);
        SetContinueButtonVisible(false);
    }

    public void PresentUnavailable()
    {
        SetText($"Compañero: {UnavailableValue} / {UnavailableValue}");
        SetFill(0f);
        SetContinueButtonVisible(false);
    }

    private void EnsureContinueButton()
    {
        if (_continueWithoutTeammateButton == null)
        {
            Transform parent = _root != null ? _root.transform : transform;
            _continueWithoutTeammateButton = CreateButton(
                "Continuar sin compañero",
                parent,
                () => ContinueWithoutTeammateRequested?.Invoke(),
                out _continueWithoutTeammateButtonText);
            _continueWithoutTeammateButton.gameObject.SetActive(false);
        }
        else
        {
            _continueWithoutTeammateButton.onClick.RemoveAllListeners();
            _continueWithoutTeammateButton.onClick.AddListener(() => ContinueWithoutTeammateRequested?.Invoke());
        }
    }

    private static Button CreateButton(string label, Transform parent, System.Action callback, out TMP_Text labelText)
    {
        var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.6f, 0.15f, 0.15f, 0.95f);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => callback?.Invoke());
        labelText = CreateText("Label", buttonObject.transform, 16f, TextAlignmentOptions.Center);
        labelText.text = label;
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        return text;
    }

    private void SetText(string value)
    {
        if (_healthText != null && _healthText.text != value)
        {
            _healthText.text = value;
        }
    }

    private void SetFill(float value)
    {
        if (_healthFill == null)
        {
            return;
        }

        float safeValue = IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        if (!Mathf.Approximately(_healthFill.fillAmount, safeValue))
        {
            _healthFill.fillAmount = safeValue;
        }

        Vector3 scale = _healthFill.rectTransform.localScale;
        if (!Mathf.Approximately(scale.x, safeValue))
        {
            scale.x = safeValue;
            _healthFill.rectTransform.localScale = scale;
        }
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
