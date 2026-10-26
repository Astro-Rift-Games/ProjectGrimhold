using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the local minimap using explicit uGUI references. It owns no network or gameplay
/// state and resets both Sanctuary representations whenever their mode changes.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMinimapView : MonoBehaviour
{
    [SerializeField]
    private GameObject _minimapRoot;

    [SerializeField]
    private CanvasGroup _visibilityGroup;

    [SerializeField]
    private RectTransform _viewport;

    [SerializeField]
    private RaidMinimapGraphic _mapGraphic;

    [SerializeField]
    private RectTransform _mapRect;

    [SerializeField]
    private Image _localMarker;

    [SerializeField]
    private RectTransform _sanctuaryIconRect;

    [SerializeField]
    private Image _sanctuaryIcon;

    [SerializeField]
    private RectTransform _sanctuaryArrowRect;

    [SerializeField]
    private Image _sanctuaryArrow;

    [SerializeField]
    private Image _border;

    private RectTransform _canvasMapRect;
    private Canvas _mapCanvas;
    private bool _mapCanvasResolved;

    /// <summary>Gets the useful minimap viewport size in local UI units.</summary>
    public Vector2 ViewportSize => _viewport != null ? _viewport.rect.size : Vector2.zero;

    /// <summary>Gets the largest Sanctuary representation size used by projection bounds.</summary>
    public Vector2 SanctuaryMarkerSize => _sanctuaryIconRect != null
        ? Vector2.Max(
            _sanctuaryIconRect.rect.size,
            _sanctuaryArrowRect != null ? _sanctuaryArrowRect.rect.size : Vector2.zero)
        : Vector2.zero;

    /// <summary>Validates all mandatory visual references and positive viewport dimensions.</summary>
    public bool TryValidateConfiguration(out string error)
    {
        if (_minimapRoot == null || _visibilityGroup == null || _viewport == null || _mapGraphic == null ||
            _mapRect == null || _localMarker == null || _sanctuaryIconRect == null ||
            _sanctuaryIcon == null || _sanctuaryArrowRect == null ||
            _sanctuaryArrow == null || _border == null)
        {
            error = "RaidMinimapView has missing serialized references.";
            return false;
        }

        if (ViewportSize.x <= 0f || ViewportSize.y <= 0f ||
            SanctuaryMarkerSize.x <= 0f || SanctuaryMarkerSize.y <= 0f)
        {
            error = "RaidMinimapView requires positive viewport and marker dimensions.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Configures the generated layout in local RectTransform units. The supplied scale is
    /// expressed at the Canvas reference resolution and is never interpreted as monitor pixels.
    /// </summary>
    public bool TryConfigureMap(float uiUnitsPerWorldUnit, float zoom, out string error)
    {
        if (_mapGraphic == null || _mapRect == null || !IsPositiveFinite(uiUnitsPerWorldUnit) ||
            !IsPositiveFinite(zoom))
        {
            error = "RaidMinimapView has invalid generated-map configuration.";
            return false;
        }

        if (!_mapGraphic.TryConfigure(uiUnitsPerWorldUnit * zoom, out Vector2 uiSize, out error))
        {
            return false;
        }

        _mapRect.sizeDelta = uiSize;
        _mapRect.localRotation = Quaternion.identity;
        error = null;
        return true;
    }

    /// <summary>Gets the authored world position aligned with the generated layout center.</summary>
    public Vector2 MapPivotWorldPosition => _mapGraphic != null
        ? _mapGraphic.WorldPivotPosition
        : Vector2.zero;

    /// <summary>Shows the minimap graphics without disabling the presenter component.</summary>
    public void ShowMinimap()
    {
        if (_visibilityGroup != null)
        {
            _visibilityGroup.alpha = 1f;
            _visibilityGroup.interactable = false;
            _visibilityGroup.blocksRaycasts = false;
        }
    }

    /// <summary>Hides the minimap graphics without changing gameplay state.</summary>
    public void HideMinimap()
    {
        if (_visibilityGroup != null)
        {
            _visibilityGroup.alpha = 0f;
            _visibilityGroup.interactable = false;
            _visibilityGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Positions the map in local UI units, then rounds its screen pivot to physical pixels.
    /// This keeps Point-filtered tile art at a stable sampling phase while the player moves;
    /// the nearest-pixel translation error is at most half a screen pixel per axis.
    /// </summary>
    public void PresentMapPosition(Vector2 anchoredPosition)
    {
        if (_mapRect == null)
        {
            return;
        }

        _mapRect.anchoredPosition = anchoredPosition;
        _mapRect.localRotation = Quaternion.identity;
        SnapMapToScreenPixels();
    }

    /// <summary>Presents the local player at the fixed center marker.</summary>
    public void PresentLocalMarker()
    {
        if (_localMarker == null)
        {
            return;
        }

        _localMarker.enabled = true;
        _localMarker.raycastTarget = false;
        _localMarker.rectTransform.anchoredPosition = Vector2.zero;
        _localMarker.rectTransform.localRotation = Quaternion.identity;
        _localMarker.rectTransform.localScale = Vector3.one;
    }

    /// <summary>Presents an interior Sanctuary icon without rotating it.</summary>
    public void PresentSanctuaryIcon(Vector2 position, Color color, float scale)
    {
        ResetArrowVisual();
        if (_sanctuaryIcon == null || _sanctuaryIconRect == null)
        {
            return;
        }

        _sanctuaryIcon.enabled = true;
        _sanctuaryIcon.raycastTarget = false;
        _sanctuaryIcon.color = color;
        _sanctuaryIconRect.anchoredPosition = position;
        _sanctuaryIconRect.localRotation = Quaternion.identity;
        _sanctuaryIconRect.localScale = Vector3.one * SanitizeScale(scale);
    }

    /// <summary>Presents a border Sanctuary arrow with the supplied mathematical angle.</summary>
    public void PresentSanctuaryArrow(
        Vector2 position,
        float angleDegrees,
        float baseAngleCorrection,
        Color color,
        float scale)
    {
        ResetIconVisual();
        if (_sanctuaryArrow == null || _sanctuaryArrowRect == null)
        {
            return;
        }

        _sanctuaryArrow.enabled = true;
        _sanctuaryArrow.raycastTarget = false;
        _sanctuaryArrow.color = color;
        _sanctuaryArrowRect.anchoredPosition = position;
        _sanctuaryArrowRect.localRotation = Quaternion.Euler(
            0f,
            0f,
            angleDegrees + SanitizeAngle(baseAngleCorrection));
        _sanctuaryArrowRect.localScale = Vector3.one * SanitizeScale(scale);
    }

    /// <summary>Hides both Sanctuary representations and restores their authored transforms.</summary>
    public void HideSanctuary()
    {
        ResetIconVisual();
        ResetArrowVisual();
    }

    /// <summary>Clears all minimap visuals and hides the minimap root.</summary>
    public void Clear()
    {
        HideSanctuary();
        if (_mapRect != null)
        {
            _mapRect.anchoredPosition = Vector2.zero;
            _mapRect.localRotation = Quaternion.identity;
            _mapRect.localScale = Vector3.one;
        }

        if (_localMarker != null)
        {
            _localMarker.enabled = false;
        }

        HideMinimap();
    }

    private void Awake()
    {
        DisableRaycasts();
        Clear();
    }

    private void SnapMapToScreenPixels()
    {
        if (!_mapCanvasResolved || _canvasMapRect != _mapRect)
        {
            _canvasMapRect = _mapRect;
            Canvas canvas = _mapRect.GetComponentInParent<Canvas>();
            _mapCanvas = canvas != null ? canvas.rootCanvas : null;
            _mapCanvasResolved = true;
        }

        // Standalone views have no physical screen grid. World-space canvases also need
        // an explicit camera; never discover Camera.main or change the rest of the HUD.
        if (_mapCanvas == null)
            return;

        if (_mapCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            SnapMapToOverlayCanvasPixels();
            return;
        }

        Camera camera = _mapCanvas.worldCamera;
        if (_mapCanvas.renderMode == RenderMode.WorldSpace && camera == null)
            return;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, _mapRect.position);
        screenPoint = new Vector2(Mathf.Round(screenPoint.x), Mathf.Round(screenPoint.y));
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _mapRect, screenPoint, camera, out Vector3 worldPoint))
        {
            _mapRect.position = worldPoint;
        }
    }

    private void SnapMapToOverlayCanvasPixels()
    {
        // Overlay canvases render relative to their own RectTransform and ignore where it sits
        // in world space. The HUD is parented to the moving player, so measure the pixel grid
        // in root-canvas space; world coordinates drift by the player's fractional position.
        float scale = _mapCanvas.scaleFactor;
        if (!IsPositiveFinite(scale))
            return;

        var canvasRect = (RectTransform)_mapCanvas.transform;
        Vector3 local = canvasRect.InverseTransformPoint(_mapRect.position);
        Vector2 origin = canvasRect.rect.min;
        Vector2 pixel = ((Vector2)local - origin) * scale;
        Vector2 snapped = new Vector2(Mathf.Round(pixel.x), Mathf.Round(pixel.y)) / scale + origin;
        _mapRect.position = canvasRect.TransformPoint(new Vector3(snapped.x, snapped.y, local.z));
    }

    private void OnCanvasHierarchyChanged()
    {
        _mapCanvasResolved = false;
    }

    private void OnTransformParentChanged()
    {
        _mapCanvasResolved = false;
    }

    private void ResetIconVisual()
    {
        if (_sanctuaryIcon == null || _sanctuaryIconRect == null)
        {
            return;
        }

        _sanctuaryIcon.enabled = false;
        _sanctuaryIconRect.anchoredPosition = Vector2.zero;
        _sanctuaryIconRect.localRotation = Quaternion.identity;
        _sanctuaryIconRect.localScale = Vector3.one;
        _sanctuaryIcon.color = Color.white;
    }

    private void ResetArrowVisual()
    {
        if (_sanctuaryArrow == null || _sanctuaryArrowRect == null)
        {
            return;
        }

        _sanctuaryArrow.enabled = false;
        _sanctuaryArrowRect.anchoredPosition = Vector2.zero;
        _sanctuaryArrowRect.localRotation = Quaternion.identity;
        _sanctuaryArrowRect.localScale = Vector3.one;
        _sanctuaryArrow.color = Color.white;
    }

    private void DisableRaycasts()
    {
        if (_mapGraphic != null) _mapGraphic.raycastTarget = false;
        if (_localMarker != null) _localMarker.raycastTarget = false;
        if (_sanctuaryIcon != null) _sanctuaryIcon.raycastTarget = false;
        if (_sanctuaryArrow != null) _sanctuaryArrow.raycastTarget = false;
        if (_border != null) _border.raycastTarget = false;
    }

    private static float SanitizeScale(float value)
    {
        return IsFinite(value) && value > 0f ? value : 1f;
    }

    private static float SanitizeAngle(float value)
    {
        return IsFinite(value) ? value : 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsPositiveFinite(float value)
    {
        return IsFinite(value) && value > 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        DisableRaycasts();
    }
#endif
}
