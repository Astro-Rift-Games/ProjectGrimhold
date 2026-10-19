using UnityEngine;

/// <summary>
/// One-shot feedback for confirmed UI results: a scale punch on success and a short shake on
/// rejection. Presentation only; it reacts to results the caller already received.
/// </summary>
[DisallowMultipleComponent]
public sealed class UiFeedbackPulse : MonoBehaviour
{
    [SerializeField, Tooltip("Animated transform. Defaults to this transform.")]
    private RectTransform _target;

    [SerializeField, Min(0.01f)]
    private float _duration = 0.3f;

    [SerializeField, Min(0f)]
    private float _successScaleAmplitude = 0.12f;

    [SerializeField, Min(0f), Tooltip("Rejection shake amplitude in canvas units.")]
    private float _rejectionShakeAmplitude = 8f;

    private readonly UiPulseState _state = new();
    private Vector3 _baseScale = Vector3.one;
    private Vector2 _basePosition;
    private bool _captured;

    /// <summary>Plays the pulse on <paramref name="target"/> when it has this component.</summary>
    public static void Play(GameObject target, UiPulseKind kind)
    {
        if (target != null && target.TryGetComponent(out UiFeedbackPulse pulse))
        {
            pulse.Play(kind);
        }
    }

    public void Play(UiPulseKind kind)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        CaptureBase();
        _state.Play(kind, _successScaleAmplitude, _rejectionShakeAmplitude);
    }

    private void Awake()
    {
        if (_target == null)
        {
            _target = transform as RectTransform;
        }
    }

    private void OnDisable()
    {
        if (_state.IsPlaying)
        {
            _state.Advance(_duration, _duration);
            Apply();
        }

        _captured = false;
    }

    private void Update()
    {
        if (!_state.IsPlaying)
        {
            return;
        }

        _state.Advance(Time.unscaledDeltaTime, _duration);
        Apply();
        if (!_state.IsPlaying)
        {
            _captured = false;
        }
    }

    private void CaptureBase()
    {
        if (_captured || _target == null)
        {
            return;
        }

        // Captured per pulse, not in Awake, so layout groups that reposition the target keep working.
        _captured = true;
        _baseScale = _target.localScale;
        _basePosition = _target.anchoredPosition;
    }

    private void Apply()
    {
        if (_target == null || !_captured)
        {
            return;
        }

        _target.localScale = _baseScale * _state.Scale;
        _target.anchoredPosition = _basePosition + new Vector2(_state.OffsetX, 0f);
    }
}
