using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Local hover/press scale feedback for a uGUI control. It complements the Selectable color or
/// sprite transition and never changes interactability, layout or click handling.
/// </summary>
[DisallowMultipleComponent]
public sealed class UiButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField, Tooltip("Scaled transform. Defaults to this transform.")]
    private RectTransform _target;

    [SerializeField, Tooltip("Optional; when set, feedback is suppressed while it is not interactable.")]
    private Selectable _selectable;

    [SerializeField, Min(1f)]
    private float _hoverScale = 1.05f;

    [SerializeField, Range(0.5f, 1f)]
    private float _pressedScale = 0.95f;

    [SerializeField, Min(1f), Tooltip("Exponential approach speed; higher is snappier.")]
    private float _sharpness = 22f;

    private Vector3 _baseScale = Vector3.one;
    private float _currentScale = 1f;
    private bool _hovered;
    private bool _selected;
    private bool _pressed;

    private void Awake()
    {
        if (_target == null)
        {
            _target = transform as RectTransform;
        }

        if (_selectable == null)
        {
            _selectable = GetComponent<Selectable>();
        }
    }

    private void OnEnable()
    {
        // Recaptured on activation so a host that rescales the control after Awake is respected.
        if (_target != null)
        {
            _baseScale = _target.localScale;
        }
    }

    private void OnDisable()
    {
        _hovered = false;
        _selected = false;
        _pressed = false;
        _currentScale = 1f;
        Apply();
    }

    private void Update()
    {
        bool interactable = _selectable == null || _selectable.IsInteractable();
        float target = UiPointerResponse.TargetScale(
            interactable, _hovered || _selected, _pressed, _hoverScale, _pressedScale);

        if (Mathf.Approximately(_currentScale, target))
        {
            return;
        }

        _currentScale = UiPointerResponse.Approach(_currentScale, target, _sharpness, Time.unscaledDeltaTime);
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _pressed = true;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _pressed = false;
        }
    }

    public void OnSelect(BaseEventData eventData) => _selected = eventData is not PointerEventData;

    public void OnDeselect(BaseEventData eventData) => _selected = false;

    private void Apply()
    {
        if (_target != null)
        {
            _target.localScale = _baseScale * _currentScale;
        }
    }
}
