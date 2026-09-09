using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the local contextual inventory popup and its reusable action-button pool.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidLootContextMenuView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    private RectTransform _menuRoot;

    [SerializeField]
    private RectTransform _buttonContainer;

    [SerializeField]
    private RaidLootContextActionButton _buttonPrefab;

    [SerializeField]
    private RectTransform _canvasRoot;

    private readonly List<RaidLootContextActionButton> _buttons = new();
    private readonly Vector3[] _anchorWorldCorners = new Vector3[4];
    private bool _pointerInside;

    public event Action<LootContextActionId> ActionRequested;
    public event Action DismissRequested;

    public bool IsOpen => _menuRoot != null && _menuRoot.gameObject.activeSelf;
    public RectTransform CurrentAnchor { get; private set; }

    private void Update()
    {
        if (IsOpen && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            !_pointerInside)
        {
            DismissRequested?.Invoke();
        }
    }

    public bool Show(IReadOnlyList<LootContextActionDescriptor> actions, RectTransform anchor)
    {
        if (actions == null || actions.Count == 0 || _menuRoot == null ||
            _buttonContainer == null || _buttonPrefab == null || _canvasRoot == null || anchor == null)
        {
            Hide();
            return false;
        }

        EnsureButtonCount(actions.Count);
        for (int i = 0; i < _buttons.Count; i++)
        {
            bool active = i < actions.Count;
            _buttons[i].gameObject.SetActive(active);
            if (active)
            {
                _buttons[i].Present(actions[i]);
            }
        }

        _menuRoot.gameObject.SetActive(true);
        _menuRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            16f + actions.Count * 40f + Mathf.Max(0, actions.Count - 1) * 4f);
        Canvas.ForceUpdateCanvases();
        CurrentAnchor = anchor;
        PositionWithinCanvas(anchor);
        return true;
    }

    public void Hide()
    {
        _pointerInside = false;
        CurrentAnchor = null;
        if (_menuRoot != null)
        {
            _menuRoot.gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
    }

    public bool ContainsPointer => _pointerInside;

    private void EnsureButtonCount(int count)
    {
        while (_buttons.Count < count)
        {
            RaidLootContextActionButton button = Instantiate(_buttonPrefab, _buttonContainer);
            button.Invoked += OnActionInvoked;
            _buttons.Add(button);
        }
    }

    private void PositionWithinCanvas(RectTransform anchor)
    {
        const float spacing = 8f;
        Bounds anchorBounds = CalculateAnchorBounds(anchor);
        Bounds menuBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_canvasRoot, _menuRoot);
        Vector2 menuSize = menuBounds.size;
        Vector2 pivot = _menuRoot.pivot;
        Rect canvasBounds = _canvasRoot.rect;

        Vector2 localPivot = new(
            anchorBounds.max.x + spacing + menuSize.x * pivot.x,
            anchorBounds.center.y + menuSize.y * (pivot.y - 0.5f));
        if (localPivot.x + menuSize.x * (1f - pivot.x) > canvasBounds.xMax)
        {
            localPivot.x = anchorBounds.min.x - spacing - menuSize.x * (1f - pivot.x);
        }

        localPivot.x = Mathf.Clamp(
            localPivot.x,
            canvasBounds.xMin + menuSize.x * pivot.x,
            canvasBounds.xMax - menuSize.x * (1f - pivot.x));
        localPivot.y = Mathf.Clamp(
            localPivot.y,
            canvasBounds.yMin + menuSize.y * pivot.y,
            canvasBounds.yMax - menuSize.y * (1f - pivot.y));
        _menuRoot.position = _canvasRoot.TransformPoint(localPivot);

        Canvas.ForceUpdateCanvases();
        ClampRenderedBoundsToCanvas();
    }

    private void ClampRenderedBoundsToCanvas()
    {
        Bounds rendered = RectTransformUtility.CalculateRelativeRectTransformBounds(_canvasRoot, _menuRoot);
        Rect canvasBounds = _canvasRoot.rect;
        Vector2 correction = Vector2.zero;
        if (rendered.min.x < canvasBounds.xMin) correction.x = canvasBounds.xMin - rendered.min.x;
        else if (rendered.max.x > canvasBounds.xMax) correction.x = canvasBounds.xMax - rendered.max.x;
        if (rendered.min.y < canvasBounds.yMin) correction.y = canvasBounds.yMin - rendered.min.y;
        else if (rendered.max.y > canvasBounds.yMax) correction.y = canvasBounds.yMax - rendered.max.y;

        _menuRoot.position += _canvasRoot.TransformVector(correction);
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

    private void OnActionInvoked(LootContextActionId actionId)
    {
        ActionRequested?.Invoke(actionId);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            if (_buttons[i] != null)
            {
                _buttons[i].Invoked -= OnActionInvoked;
            }
        }
    }
}
