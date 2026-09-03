using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the local raid HUD using values supplied by its presenter.
/// It owns only uGUI/TMP presentation and never reads or modifies gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidHudView : MonoBehaviour
{
    private const string UnavailableValue = "—";

    [SerializeField]
    private GameObject _mainHudRoot;

    [SerializeField]
    private TMP_Text _healthText;

    [SerializeField]
    private Image _healthFill;

    [SerializeField]
    private TMP_Text _staminaText;

    [SerializeField]
    private Image _staminaFill;

    [SerializeField]
    private TMP_Text _attackText;

    [SerializeField]
    private RectTransform _cooldownRoot;

    [SerializeField]
    private Image _cooldownIcon;

    private Sprite _defaultCooldownIcon;

    private void Awake()
    {
        _defaultCooldownIcon = _cooldownIcon != null ? _cooldownIcon.sprite : null;
    }

    [SerializeField]
    private Image _cooldownFill;

    [SerializeField]
    private TMP_Text _cooldownSecondsText;

    [SerializeField]
    private TMP_Text _inventoryText;

    [SerializeField]
    private TMP_Text _extractionText;

    [SerializeField]
    private GameObject _defeatedRoot;

    /// <summary>Gets the visual root controlled by this view.</summary>
    public GameObject MainHudRoot => _mainHudRoot;

    /// <summary>Gets the health label for presentation verification.</summary>
    public TMP_Text HealthText => _healthText;

    /// <summary>Gets the health fill for presentation verification.</summary>
    public Image HealthFill => _healthFill;

    /// <summary>Gets the Stamina label for presentation verification.</summary>
    public TMP_Text StaminaText => _staminaText;

    /// <summary>Gets the Stamina fill for presentation verification.</summary>
    public Image StaminaFill => _staminaFill;

    /// <summary>Gets the primary-attack label for presentation verification.</summary>
    public TMP_Text AttackText => _attackText;

    /// <summary>Gets the bottom-centered cooldown widget root.</summary>
    public RectTransform CooldownRoot => _cooldownRoot;

    /// <summary>Gets the class-specific primary-attack icon.</summary>
    public Image CooldownIcon => _cooldownIcon;

    /// <summary>Gets the cooldown fill for presentation verification.</summary>
    public Image CooldownFill => _cooldownFill;

    /// <summary>Gets the compact remaining-seconds label.</summary>
    public TMP_Text CooldownSecondsText => _cooldownSecondsText;

    /// <summary>Gets the inventory-capacity label for presentation verification.</summary>
    public TMP_Text InventoryText => _inventoryText;

    /// <summary>Gets the extraction label for presentation verification.</summary>
    public TMP_Text ExtractionText => _extractionText;

    /// <summary>Gets the defeated-state visual root.</summary>
    public GameObject DefeatedRoot => _defeatedRoot;

    /// <summary>Presents current and configured maximum health.</summary>
    public void PresentHealth(float currentHealth, float maximumHealth)
    {
        float safeCurrent = IsFinite(currentHealth) ? Mathf.Max(0f, currentHealth) : 0f;
        float safeMaximum = IsFinite(maximumHealth) ? Mathf.Max(0f, maximumHealth) : 0f;
        SetText(_healthText, $"Salud: {safeCurrent:0.#} / {safeMaximum:0.#}");
        SetFill(_healthFill, safeMaximum > 0f ? safeCurrent / safeMaximum : 0f);
    }

    /// <summary>Restores the unavailable health placeholder and empty fill.</summary>
    public void ClearHealth()
    {
        SetText(_healthText, $"Salud: {UnavailableValue} / {UnavailableValue}");
        SetFill(_healthFill, 0f);
    }

    /// <summary>Presents current, maximum and Exhaustion state without owning gameplay state.</summary>
    public void PresentStamina(float currentStamina, float maximumStamina, bool isExhausted)
    {
        float safeCurrent = IsFinite(currentStamina) ? Mathf.Max(0f, currentStamina) : 0f;
        float safeMaximum = IsFinite(maximumStamina) ? Mathf.Max(0f, maximumStamina) : 0f;
        string exhaustionSuffix = isExhausted ? " (Agotado)" : string.Empty;
        SetText(
            _staminaText,
            $"Stamina: {safeCurrent:0} / {safeMaximum:0}{exhaustionSuffix}");
        SetFill(_staminaFill, safeMaximum > 0f ? safeCurrent / safeMaximum : 0f);
    }

    /// <summary>Restores the unavailable Stamina placeholder and empty fill.</summary>
    public void ClearStamina()
    {
        SetText(_staminaText, $"Stamina: {UnavailableValue} / {UnavailableValue}");
        SetFill(_staminaFill, 0f);
    }

    /// <summary>Presents primary-attack availability and normalized cooldown.</summary>
    public void PresentAttack(bool isAvailable, float remainingSeconds, float normalizedRemaining, Sprite attackIcon = null)
    {
        SetText(_attackText, string.Empty);
        SetCooldownIcon(attackIcon ?? _defaultCooldownIcon);
        if (_cooldownRoot != null && !_cooldownRoot.gameObject.activeSelf)
        {
            _cooldownRoot.gameObject.SetActive(true);
        }

        bool showCooldown = !isAvailable && IsFinite(remainingSeconds) && remainingSeconds > 0f;
        SetText(_cooldownSecondsText, showCooldown ? remainingSeconds.ToString("0.0") : string.Empty);
        SetRadialFill(_cooldownFill, showCooldown ? normalizedRemaining : 0f);
        if (_cooldownFill != null && _cooldownFill.enabled != showCooldown)
        {
            _cooldownFill.enabled = showCooldown;
        }
    }

    /// <summary>Restores the unavailable attack placeholder and empty fill.</summary>
    public void ClearAttack()
    {
        SetText(_attackText, string.Empty);
        SetText(_cooldownSecondsText, string.Empty);
        SetRadialFill(_cooldownFill, 0f);
        if (_cooldownFill != null)
        {
            _cooldownFill.enabled = false;
        }

        if (_cooldownRoot != null)
        {
            _cooldownRoot.gameObject.SetActive(false);
        }

        SetCooldownIcon(_defaultCooldownIcon);
    }

    private void SetCooldownIcon(Sprite icon)
    {
        if (_cooldownIcon == null)
        {
            return;
        }

        _cooldownIcon.sprite = icon;
        _cooldownIcon.enabled = icon != null;
    }

    /// <summary>Presents occupied and available inventory slots.</summary>
    public void PresentInventory(int occupiedSlots, int slotCapacity)
    {
        SetText(
            _inventoryText,
            $"Inventario: {Mathf.Max(0, occupiedSlots)} / {Mathf.Max(0, slotCapacity)}");
    }

    /// <summary>Restores the unavailable inventory placeholder.</summary>
    public void ClearInventory()
    {
        SetText(_inventoryText, $"Inventario: {UnavailableValue} / {UnavailableValue}");
    }

    /// <summary>Presents the unavailable extraction state.</summary>
    public void PresentExtractionUnavailable()
    {
        SetText(_extractionText, "Extracción: no disponible");
    }

    /// <summary>Presents the confirmed remaining duration of the local extraction process.</summary>
    public void PresentExtractionCountdown(float remainingSeconds)
    {
        float safeRemaining = IsFinite(remainingSeconds) ? Mathf.Max(0f, remainingSeconds) : 0f;
        SetText(_extractionText, $"Extracción: {safeRemaining:0.0} s");
    }

    /// <summary>Presents a transient cancellation confirmation for the local extraction process.</summary>
    public void PresentExtractionCancelled()
    {
        SetText(_extractionText, "Extracción: cancelada");
    }

    /// <summary>Presents the terminal confirmed extracted state.</summary>
    public void PresentExtractionCompleted()
    {
        SetText(_extractionText, "EXTRAÍDO");
    }

    /// <summary>Presents the local player's confirmed individual quota progress.</summary>
    public void PresentExtractionProgress(int currentProgress, int quota)
    {
        SetText(_extractionText, $"Progreso: {Mathf.Max(0, currentProgress)} / {Mathf.Max(0, quota)}");
    }

    /// <summary>Presents a transient confirmation that the individual quota was completed.</summary>
    public void PresentQuotaCompleted()
    {
        SetText(_extractionText, "Cuota completada");
    }

    /// <summary>Presents the confirmed individual Sanctuary assignment.</summary>
    public void PresentSanctuaryAssigned()
    {
        SetText(_extractionText, "Santuario asignado");
    }

    /// <summary>Presents the confirmed ritual progress derived from Fusion's snapshot.</summary>
    public void PresentRitualProgress(float remainingSeconds)
    {
        float safeRemaining = IsFinite(remainingSeconds) ? Mathf.Max(0f, remainingSeconds) : 0f;
        SetText(_extractionText, $"Ritual: {safeRemaining:0.0} s");
    }

    /// <summary>Presents the terminal ritual cancellation state.</summary>
    public void PresentRitualCancelled()
    {
        SetText(_extractionText, "Ritual cancelado");
    }

    /// <summary>Presents the permanent enabled Sanctuary state.</summary>
    public void PresentSanctuaryEnabled()
    {
        SetText(_extractionText, "Santuario habilitado");
    }

    /// <summary>Shows or hides the local defeated indicator without hiding the HUD.</summary>
    public void PresentDefeated(bool isDefeated)
    {
        if (_defeatedRoot != null && _defeatedRoot.activeSelf != isDefeated)
        {
            _defeatedRoot.SetActive(isDefeated);
        }
    }

    public void Clear()
    {
        if (_mainHudRoot != null && !_mainHudRoot.activeSelf)
        {
            _mainHudRoot.SetActive(true);
        }

        ClearHealth();
        ClearStamina();
        ClearAttack();
        ClearInventory();
        PresentExtractionUnavailable();
        PresentDefeated(false);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
        {
            target.text = value;
        }
    }

    private static void SetFill(Image target, float value)
    {
        if (target == null)
        {
            return;
        }

        float safeValue = IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        if (!Mathf.Approximately(target.fillAmount, safeValue))
        {
            target.fillAmount = safeValue;
        }

        Vector3 scale = target.rectTransform.localScale;
        if (!Mathf.Approximately(scale.x, safeValue))
        {
            scale.x = safeValue;
            target.rectTransform.localScale = scale;
        }
    }

    private static void SetRadialFill(Image target, float value)
    {
        if (target == null)
        {
            return;
        }

        float safeValue = IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        if (!Mathf.Approximately(target.fillAmount, safeValue))
        {
            target.fillAmount = safeValue;
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
