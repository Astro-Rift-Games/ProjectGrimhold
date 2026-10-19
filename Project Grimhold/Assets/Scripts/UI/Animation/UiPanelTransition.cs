using UnityEngine;

/// <summary>
/// Fade and scale transition for a panel root. Activating the GameObject plays the show
/// transition; <see cref="Hide"/> plays the hide transition and deactivates the GameObject when it
/// finishes. Logical visibility (<see cref="IsShown"/>) and input blocking change immediately, so
/// open/close decisions never wait for the animation. Use <see cref="UiPanelVisibility"/> from views.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class UiPanelTransition : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float _showDuration = 0.18f;

    [SerializeField, Min(0f)]
    private float _hideDuration = 0.12f;

    [SerializeField, Range(0.5f, 1f), Tooltip("Scale at zero visibility; 1 disables the scale effect and never writes the scale " +
        "(use 1 for content whose host owns its scale, such as TownPlayerMenu tabs).")]
    private float _hiddenScale = 0.96f;

    [SerializeField, Tooltip("Scaled transform. Defaults to this transform.")]
    private RectTransform _scaleTarget;

    private readonly UiPanelTransitionState _state = new();
    private CanvasGroup _canvasGroup;
    private Vector3 _baseScale = Vector3.one;
    private bool _baseInteractable = true;
    private bool _baseBlocksRaycasts = true;
    private bool _initialized;
    private bool _hasBeenEnabled;
    private bool _deactivatePending;

    /// <summary>
    /// True while the panel is logically open, including during its show transition. Before its first
    /// enable (an active panel under an inactive parent) it reports <c>activeSelf</c>, as views did before.
    /// </summary>
    public bool IsShown => gameObject.activeSelf &&
        (!_hasBeenEnabled || (!_deactivatePending && _state.IsLogicallyVisible));

    private void Awake() => Initialize();

    private void OnEnable()
    {
        Initialize();
        _hasBeenEnabled = true;
        if (_deactivatePending)
        {
            // A parent was disabled mid-hide; finish the hide on the next frame without flashing.
            ApplyVisual(0f);
            return;
        }

        // Recaptured on every activation: hosts may reset the scale after Awake (TownPlayerMenu tabs).
        CaptureBaseScale();
        _state.SnapHidden();
        _state.BeginShow();
        SetInputBlocking(true);
        ApplyVisual(_state.Visibility);
    }

    private void OnDisable()
    {
        // activeSelf is still true only when a parent was disabled; a direct SetActive(false)
        // already completed the hide.
        _deactivatePending = _state.Phase == UiPanelTransitionPhase.Hiding && gameObject.activeSelf;

        _state.SnapShown();
        ApplyVisual(1f);
    }

    private void Update()
    {
        if (_deactivatePending)
        {
            _deactivatePending = false;
            _state.SnapHidden();
            gameObject.SetActive(false);
            return;
        }

        if (!_state.IsAnimating)
        {
            return;
        }

        bool hideCompleted = _state.Advance(Time.unscaledDeltaTime, _showDuration, _hideDuration);
        ApplyVisual(_state.Visibility);
        if (hideCompleted)
        {
            gameObject.SetActive(false);
        }
    }

    public void Show()
    {
        Initialize();
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            return;
        }

        if (_deactivatePending)
        {
            _deactivatePending = false;
            _state.SnapHidden();
        }

        _state.BeginShow();
        SetInputBlocking(true);
    }

    public void Hide()
    {
        Initialize();
        if (!gameObject.activeSelf)
        {
            return;
        }

        // Never enabled yet (e.g. hidden from a parent's Awake): nothing is on screen, so hide at once
        // exactly like the SetActive(false) it replaces; a later OnEnable would otherwise show it.
        if (!_hasBeenEnabled || !gameObject.activeInHierarchy || _hideDuration <= 0f)
        {
            HideImmediate();
            return;
        }

        _state.BeginHide();
        SetInputBlocking(false);
    }

    public void HideImmediate()
    {
        Initialize();
        _deactivatePending = false;
        _state.SnapHidden();
        SetInputBlocking(true);
        gameObject.SetActive(false);
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _canvasGroup = GetComponent<CanvasGroup>();
        _baseInteractable = _canvasGroup.interactable;
        _baseBlocksRaycasts = _canvasGroup.blocksRaycasts;
        if (_scaleTarget == null)
        {
            _scaleTarget = transform as RectTransform;
        }
    }

    private void CaptureBaseScale()
    {
        if (_scaleTarget != null && _hiddenScale < 1f)
        {
            _baseScale = _scaleTarget.localScale;
        }
    }

    private void SetInputBlocking(bool shown)
    {
        _canvasGroup.interactable = shown && _baseInteractable;
        _canvasGroup.blocksRaycasts = shown && _baseBlocksRaycasts;
    }

    private void ApplyVisual(float visibility)
    {
        _canvasGroup.alpha = visibility;
        if (_scaleTarget != null && _hiddenScale < 1f)
        {
            _scaleTarget.localScale = _baseScale * Mathf.LerpUnclamped(_hiddenScale, 1f, visibility);
        }
    }
}
