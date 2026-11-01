using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// uGUI surface of the Downed and revive feedback. It owns only visibility, fill, text and a short
/// pulse; it never reads or modifies gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidDownedHudView : MonoBehaviour
{
    [Header("Downed (local player)")]
    [SerializeField]
    private GameObject _downedRoot;

    [SerializeField]
    private Image _reserveFill;

    [SerializeField]
    private TMP_Text _reserveText;

    [SerializeField]
    private TMP_Text _stateText;

    [Header("Revive progress (Downed and reviver)")]
    [SerializeField]
    private GameObject _reviveRoot;

    [SerializeField]
    private Image _reviveFill;

    [Tooltip("Optional linear bar inside the revive panel; follows the same progress as the ring.")]
    [SerializeField]
    private Image _reviveBarFill;

    [Header("Optional modular status ring")]
    [SerializeField]
    private GameObject _reserveRingRoot;

    [SerializeField]
    private GameObject _reviveRingRoot;

    [SerializeField]
    private TMP_Text _reviveText;

    [Header("Reviver")]
    [SerializeField]
    private GameObject _allyDownedRoot;

    [SerializeField]
    private TMP_Text _allyDownedText;

    [Header("Notice")]
    [SerializeField]
    private TMP_Text _messageText;

    [SerializeField]
    private Color _interruptedColor = new Color(1f, 0.45f, 0.4f, 1f);

    [SerializeField]
    private Color _completedColor = new Color(0.55f, 1f, 0.6f, 1f);

    [SerializeField, Min(1f)]
    private float _pulseScale = 1.08f;

    [SerializeField, Min(0.01f)]
    private float _pulseDuration = 0.25f;

    private float _pulseRemaining;
    private Vector3 _pulseBaseScale = Vector3.one;
    private bool _hasPulseBase;
    private bool _showReserveRing;
    private bool _showReviveRing;

    public void PresentDowned(bool active, float reserveFraction, string reserveText, bool isPaused)
    {
        PresentDowned(active, reserveFraction, reserveText, isPaused ? "ABATIDO · Reanimando" : "ABATIDO");
    }

    /// <summary>Downed panel with a prepared status line (recovery form or Self-reviver availability).</summary>
    public void PresentDowned(bool active, float reserveFraction, string reserveText, string stateText)
    {
        _showReserveRing = active;
        RefreshStatusRing();
        SetActive(_downedRoot, active);
        if (!active)
        {
            return;
        }

        SetFill(_reserveFill, reserveFraction);
        SetText(_reserveText, reserveText);
        SetText(_stateText, stateText);
    }

    public void PresentReviveProgress(bool visible, float fraction, bool asReviver)
    {
        PresentReviveProgress(visible, fraction, asReviver ? "Reanimando…" : "Te están reanimando…");
    }

    /// <summary>Recovery progress with a prepared label (revive, Self-revive or handoff).</summary>
    public void PresentReviveProgress(bool visible, float fraction, string label)
    {
        _showReviveRing = visible;
        RefreshStatusRing();
        SetActive(_reviveRoot, visible);
        if (!visible)
        {
            return;
        }

        SetFill(_reviveFill, fraction);
        SetFill(_reviveBarFill, fraction);
        SetText(_reviveText, label);
    }

    public void PresentAllyDowned(bool visible, bool isInRange)
    {
        PresentAllyDowned(visible, isInRange, false);
    }

    /// <summary>Ally Downed hint; offers tap (Self-reviver handoff) and hold (revive) when both apply.</summary>
    public void PresentAllyDowned(bool visible, bool isInRange, bool canHandoff)
    {
        SetActive(_allyDownedRoot, visible);
        if (visible)
        {
            SetText(_allyDownedText, DownedRecoveryFeedbackRules.AllyDownedText(isInRange, canHandoff));
        }
    }

    public void ShowMessage(string message, bool isPositive)
    {
        if (_messageText == null)
        {
            return;
        }

        _messageText.text = message;
        _messageText.color = isPositive ? _completedColor : _interruptedColor;
        SetActive(_messageText.gameObject, true);
        StartPulse();
    }

    public void HideMessage()
    {
        if (_messageText != null)
        {
            SetActive(_messageText.gameObject, false);
        }
    }

    /// <summary>Basic visual cue when the local player enters Downed.</summary>
    public void PulseDowned()
    {
        StartPulse();
    }

    public void Clear()
    {
        _showReserveRing = false;
        _showReviveRing = false;
        RefreshStatusRing();
        SetActive(_downedRoot, false);
        SetActive(_reviveRoot, false);
        SetActive(_allyDownedRoot, false);
        HideMessage();
        _pulseRemaining = 0f;
        ApplyPulseScale(1f);
    }

    private void Update()
    {
        if (_pulseRemaining <= 0f)
        {
            return;
        }

        _pulseRemaining -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_pulseRemaining / _pulseDuration);
        ApplyPulseScale(1f + (_pulseScale - 1f) * t);
    }

    private void StartPulse()
    {
        if (!_hasPulseBase && _downedRoot != null)
        {
            _pulseBaseScale = _downedRoot.transform.localScale;
            _hasPulseBase = true;
        }

        _pulseRemaining = _pulseDuration;
    }

    private void ApplyPulseScale(float factor)
    {
        if (_downedRoot != null && _hasPulseBase)
        {
            _downedRoot.transform.localScale = _pulseBaseScale * factor;
        }
    }

    private void RefreshStatusRing()
    {
        // A confirmed revive temporarily owns the shared ring; reserve text stays visible.
        SetActive(_reserveRingRoot, _showReserveRing && !_showReviveRing);
        SetActive(_reviveRingRoot, _showReviveRing);
    }

    /// <summary>Clips authored Filled images without deforming them; legacy bars keep their scale contract.</summary>
    private static void SetFill(Image fill, float fraction)
    {
        if (fill == null)
        {
            return;
        }

        if (fill.type == Image.Type.Filled)
        {
            fill.fillAmount = float.IsNaN(fraction) || float.IsInfinity(fraction)
                ? 0f
                : Mathf.Clamp01(fraction);
            fill.rectTransform.localScale = Vector3.one;
            return;
        }

        Vector3 scale = fill.rectTransform.localScale;
        if (!Mathf.Approximately(scale.x, fraction))
        {
            fill.rectTransform.localScale = new Vector3(fraction, scale.y, scale.z);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null && !string.Equals(text.text, value))
        {
            text.text = value;
        }
    }
}
