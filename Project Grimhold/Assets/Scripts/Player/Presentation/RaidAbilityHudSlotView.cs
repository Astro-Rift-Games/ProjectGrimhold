using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one Raid ability slot from a display model. It owns only uGUI/TMP presentation and
/// never reads or modifies gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidAbilityHudSlotView : MonoBehaviour
{
    [SerializeField]
    private UniversalAbilitySlot _slot = UniversalAbilitySlot.Slot1;

    [SerializeField]
    private Image _icon;

    [SerializeField]
    private GameObject _emptyRoot;

    [SerializeField]
    private TMP_Text _keyLabel;

    [SerializeField]
    private Image _cooldownFill;

    [SerializeField]
    private TMP_Text _secondsText;

    [SerializeField]
    private Image _phaseHighlight;

    [SerializeField]
    private GameObject _insufficientOverlay;

    [SerializeField]
    private Image _flash;

    [SerializeField]
    private RectTransform _shakeTarget;

    [SerializeField]
    private TMP_Text _messageText;

    [SerializeField]
    private Color _preparingColor = new Color(1f, 0.82f, 0.25f, 0.55f);

    [SerializeField]
    private Color _executingColor = new Color(1f, 1f, 1f, 0.75f);

    [SerializeField]
    private Color _flashColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    [SerializeField]
    [Min(0.01f)]
    private float _flashDuration = 0.35f;

    [SerializeField]
    [Min(0f)]
    private float _shakeAmplitude = 4f;

    private float _flashUntil;
    private Vector2 _shakeRest;
    private bool _hasShakeRest;

    public UniversalAbilitySlot Slot => _slot;
    public Image Icon => _icon;
    public GameObject EmptyRoot => _emptyRoot;
    public TMP_Text KeyLabel => _keyLabel;
    public Image CooldownFill => _cooldownFill;
    public TMP_Text SecondsText => _secondsText;
    public Image PhaseHighlight => _phaseHighlight;
    public GameObject InsufficientOverlay => _insufficientOverlay;
    public Image Flash => _flash;
    public TMP_Text MessageText => _messageText;

    /// <summary>Number of confirmed-start cues played since creation (presentation verification).</summary>
    public int StartedCueCount { get; private set; }

    /// <summary>Number of rejection flashes played since creation (presentation verification).</summary>
    public int RejectionFlashCount { get; private set; }

    private void Awake()
    {
        CaptureShakeRest();
        SetText(_keyLabel, TownAbilitySlotKeyLabels.For(_slot));
    }

    private void Update()
    {
        if (_flashUntil <= 0f)
        {
            return;
        }

        float remaining = _flashUntil - Time.unscaledTime;
        if (remaining <= 0f)
        {
            StopFlash();
            return;
        }

        float t = Mathf.Clamp01(remaining / _flashDuration);
        if (_flash != null)
        {
            Color color = _flashColor;
            color.a *= t;
            _flash.color = color;
        }

        if (_shakeTarget != null && _hasShakeRest)
        {
            float offset = Mathf.Sin(Time.unscaledTime * 80f) * _shakeAmplitude * t;
            _shakeTarget.anchoredPosition = _shakeRest + new Vector2(offset, 0f);
        }
    }

    /// <summary>Presents the slot icon and the display model.</summary>
    public void Present(in RaidAbilityHudSlotModel model, Sprite icon)
    {
        bool isEmpty = model.State == RaidAbilityHudSlotState.Empty;
        SetText(_keyLabel, TownAbilitySlotKeyLabels.For(_slot));
        TownAbilityIconUtility.Apply(_icon, null, isEmpty ? null : icon);
        SetActive(_emptyRoot, isEmpty);

        bool showCooldown = model.CooldownSeconds > 0f;
        SetText(_secondsText, showCooldown ? model.CooldownSeconds.ToString("0.0") : string.Empty);
        SetRadialFill(_cooldownFill, showCooldown ? model.CooldownFill : 0f);
        if (_cooldownFill != null && _cooldownFill.enabled != showCooldown)
        {
            _cooldownFill.enabled = showCooldown;
        }

        PresentPhase(model.State);
        SetActive(_insufficientOverlay, !isEmpty && model.InsufficientResource);
    }

    /// <summary>Presents the unavailable (empty) slot and drops transient feedback.</summary>
    public void Clear()
    {
        Present(new RaidAbilityHudSlotModel(RaidAbilityHudSlotState.Empty, 0f, 0f, false), null);
        ClearMessage();
        StopFlash();
    }

    /// <summary>Plays the cue for a newly confirmed activation. Never called for a rejection.</summary>
    public void PlayStartedCue()
    {
        StartedCueCount++;
    }

    /// <summary>Shows a short owner-only message with a red flash and a small shake.</summary>
    public void PresentRejection(string message)
    {
        RejectionFlashCount++;
        SetText(_messageText, message);
        StartFlash();
    }

    /// <summary>Shows a short owner-only interruption message without the rejection flash.</summary>
    public void PresentInterruption(string message)
    {
        SetText(_messageText, message);
    }

    public void ClearMessage() => SetText(_messageText, string.Empty);

    private void PresentPhase(RaidAbilityHudSlotState state)
    {
        if (_phaseHighlight == null)
        {
            return;
        }

        bool visible = state == RaidAbilityHudSlotState.Preparing || state == RaidAbilityHudSlotState.Executing;
        if (visible)
        {
            _phaseHighlight.color = state == RaidAbilityHudSlotState.Preparing ? _preparingColor : _executingColor;
        }

        if (_phaseHighlight.enabled != visible)
        {
            _phaseHighlight.enabled = visible;
        }
    }

    private void StartFlash()
    {
        _flashUntil = Time.unscaledTime + _flashDuration;
        CaptureShakeRest();
        if (_flash != null)
        {
            _flash.color = _flashColor;
            _flash.enabled = true;
        }
    }

    private void StopFlash()
    {
        _flashUntil = 0f;
        if (_flash != null)
        {
            _flash.enabled = false;
        }

        if (_shakeTarget != null && _hasShakeRest)
        {
            _shakeTarget.anchoredPosition = _shakeRest;
        }
    }

    private void CaptureShakeRest()
    {
        if (_hasShakeRest || _shakeTarget == null)
        {
            return;
        }

        _shakeRest = _shakeTarget.anchoredPosition;
        _hasShakeRest = true;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
        {
            target.text = value;
        }
    }

    private static void SetRadialFill(Image target, float value)
    {
        if (target == null)
        {
            return;
        }

        float safe = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        if (!Mathf.Approximately(target.fillAmount, safe))
        {
            target.fillAmount = safe;
        }
    }
}
