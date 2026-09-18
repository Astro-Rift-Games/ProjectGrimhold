using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DragPreviewView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private CanvasGroup _canvasGroup;
    
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;

    public bool IsActive => gameObject.activeSelf;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_icon == null) _icon = GetComponent<Image>();
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        
        if (_icon != null)
        {
            _icon.raycastTarget = false;
        }
        
        if (_rectTransform != null)
        {
            _rectTransform.sizeDelta = new Vector2(80, 80);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
        
        _parentCanvas = GetComponentInParent<Canvas>();
        
        gameObject.SetActive(false);
    }

    public void Show(Sprite icon, Vector2 screenPosition)
    {
        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }
        
        gameObject.SetActive(true);
        UpdatePosition(screenPosition);
    }

    public void UpdatePosition(Vector2 screenPosition)
    {
        if (_rectTransform == null) return;
        
        RectTransform parentRect = _rectTransform.parent as RectTransform;
        if (parentRect != null)
        {
            Camera cam = null;
            if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = _parentCanvas.worldCamera;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, cam, out Vector2 localPoint))
            {
                _rectTransform.localPosition = localPoint;
            }
        }
        else
        {
            _rectTransform.position = screenPosition;
        }
    }

    public void Hide()
    {
        if (_icon != null)
        {
            _icon.sprite = null;
        }
        gameObject.SetActive(false);
    }
}
