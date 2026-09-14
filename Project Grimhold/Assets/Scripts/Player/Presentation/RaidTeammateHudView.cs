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

    public GameObject Root => _root;
    public TMP_Text HealthText => _healthText;
    public Image HealthFill => _healthFill;

    public void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    public void PresentHealth(float currentHealth, float maximumHealth)
    {
        float safeCurrent = IsFinite(currentHealth) ? Mathf.Max(0f, currentHealth) : 0f;
        float safeMaximum = IsFinite(maximumHealth) ? Mathf.Max(0f, maximumHealth) : 0f;
        SetText($"Compañero: {safeCurrent:0.#} / {safeMaximum:0.#}");
        SetFill(safeMaximum > 0f ? safeCurrent / safeMaximum : 0f);
    }

    public void PresentDefeated(float maximumHealth, bool hasMaximumHealth)
    {
        if (hasMaximumHealth && IsFinite(maximumHealth) && maximumHealth >= 0f)
        {
            SetText($"Compañero: 0 / {maximumHealth:0.#}");
        }
        else
        {
            SetText($"Compañero: 0 / {UnavailableValue}");
        }

        SetFill(0f);
    }

    public void PresentUnavailable()
    {
        SetText($"Compañero: {UnavailableValue} / {UnavailableValue}");
        SetFill(0f);
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
