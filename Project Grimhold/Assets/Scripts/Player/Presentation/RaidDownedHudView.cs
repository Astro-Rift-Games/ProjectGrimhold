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

    public void PresentDowned(bool active, float reserveFraction, string reserveText, bool isPaused)
    {
        SetActive(_downedRoot, active);
        if (!active)
        {
            return;
        }

        SetFill(_reserveFill, reserveFraction);
        SetText(_reserveText, reserveText);
        SetText(_stateText, isPaused ? "ABATIDO · Reanimando" : "ABATIDO");
    }

    public void PresentReviveProgress(bool visible, float fraction, bool asReviver)
    {
        SetActive(_reviveRoot, visible);
        if (!visible)
        {
            return;
        }

        SetFill(_reviveFill, fraction);
        SetText(_reviveText, asReviver ? "Reanimando…" : "Te están reanimando…");
    }

    public void PresentAllyDowned(bool visible, bool isInRange)
    {
        SetActive(_allyDownedRoot, visible);
        if (visible)
        {
            SetText(_allyDownedText, isInRange ? $"Aliado abatido · mantén [{InteractionPromptText.KeyLabel}] para reanimar" : "Aliado abatido");
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

    /// <summary>Fills a left-anchored bar by scaling it on X, like the vitals bars of the main HUD.</summary>
    private static void SetFill(Image fill, float fraction)
    {
        if (fill == null)
        {
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
