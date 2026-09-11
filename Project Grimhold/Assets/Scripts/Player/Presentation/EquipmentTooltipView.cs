using TMPro;
using UnityEngine;

/// <summary>Owns one non-interactive tooltip authored inside an inventory screen prefab.</summary>
[DisallowMultipleComponent]
public sealed class EquipmentTooltipView : MonoBehaviour
{
    [SerializeField] private RectTransform _tooltipRoot;
    [SerializeField] private RectTransform _canvasRoot;
    [SerializeField] private TMP_Text _contentText;

    private readonly Vector3[] _anchorWorldCorners = new Vector3[4];

    public bool IsOpen => _tooltipRoot != null && _tooltipRoot.gameObject.activeSelf;
    public RectTransform CurrentAnchor { get; private set; }
    public TMP_Text ContentText => _contentText;

    private void OnDisable() => Hide();

    public bool Show(in EquipmentTooltipPresentation presentation, RectTransform anchor)
    {
        if (!presentation.CanShow || anchor == null || _tooltipRoot == null ||
            _canvasRoot == null || _contentText == null)
        {
            Hide();
            return false;
        }

        _contentText.text = string.IsNullOrEmpty(presentation.Body)
            ? $"<b>{presentation.Title}</b>"
            : $"<b>{presentation.Title}</b>\n{presentation.Body}";
        _tooltipRoot.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        CurrentAnchor = anchor;
        PositionWithinCanvas(anchor);
        return true;
    }

    public void Hide()
    {
        CurrentAnchor = null;
        if (_tooltipRoot != null)
        {
            _tooltipRoot.gameObject.SetActive(false);
        }
    }

    private void PositionWithinCanvas(RectTransform anchor)
    {
        const float spacing = 8f;
        Bounds anchorBounds = CalculateAnchorBounds(anchor);
        Bounds tooltipBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_canvasRoot, _tooltipRoot);
        Vector2 tooltipSize = tooltipBounds.size;
        Vector2 pivot = _tooltipRoot.pivot;
        Rect canvasBounds = _canvasRoot.rect;

        Vector2 localPivot = new(
            anchorBounds.max.x + spacing + tooltipSize.x * pivot.x,
            anchorBounds.center.y + tooltipSize.y * (pivot.y - 0.5f));
        if (localPivot.x + tooltipSize.x * (1f - pivot.x) > canvasBounds.xMax)
        {
            localPivot.x = anchorBounds.min.x - spacing - tooltipSize.x * (1f - pivot.x);
        }

        localPivot.x = Mathf.Clamp(
            localPivot.x,
            canvasBounds.xMin + tooltipSize.x * pivot.x,
            canvasBounds.xMax - tooltipSize.x * (1f - pivot.x));
        localPivot.y = Mathf.Clamp(
            localPivot.y,
            canvasBounds.yMin + tooltipSize.y * pivot.y,
            canvasBounds.yMax - tooltipSize.y * (1f - pivot.y));
        _tooltipRoot.position = _canvasRoot.TransformPoint(localPivot);

        Canvas.ForceUpdateCanvases();
        ClampRenderedBoundsToCanvas();
    }

    private void ClampRenderedBoundsToCanvas()
    {
        Bounds rendered = RectTransformUtility.CalculateRelativeRectTransformBounds(_canvasRoot, _tooltipRoot);
        Rect canvasBounds = _canvasRoot.rect;
        Vector2 correction = Vector2.zero;
        if (rendered.min.x < canvasBounds.xMin) correction.x = canvasBounds.xMin - rendered.min.x;
        else if (rendered.max.x > canvasBounds.xMax) correction.x = canvasBounds.xMax - rendered.max.x;
        if (rendered.min.y < canvasBounds.yMin) correction.y = canvasBounds.yMin - rendered.min.y;
        else if (rendered.max.y > canvasBounds.yMax) correction.y = canvasBounds.yMax - rendered.max.y;

        _tooltipRoot.position += _canvasRoot.TransformVector(correction);
    }

    private Bounds CalculateAnchorBounds(RectTransform anchor)
    {
        anchor.GetWorldCorners(_anchorWorldCorners);
        Vector3 first = _canvasRoot.InverseTransformPoint(_anchorWorldCorners[0]);
        var bounds = new Bounds(first, Vector3.zero);
        for (int index = 1; index < _anchorWorldCorners.Length; index++)
        {
            bounds.Encapsulate(_canvasRoot.InverseTransformPoint(_anchorWorldCorners[index]));
        }

        return bounds;
    }
}
