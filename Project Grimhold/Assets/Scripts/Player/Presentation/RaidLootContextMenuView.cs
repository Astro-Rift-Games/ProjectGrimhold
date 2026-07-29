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
    private bool _pointerInside;

    public event Action<LootContextActionId> ActionRequested;
    public event Action DismissRequested;

    public bool IsOpen => _menuRoot != null && _menuRoot.gameObject.activeSelf;

    private void Update()
    {
        if (IsOpen && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            !_pointerInside)
        {
            DismissRequested?.Invoke();
        }
    }

    public bool Show(IReadOnlyList<LootContextActionDescriptor> actions, Vector2 screenPosition)
    {
        if (actions == null || actions.Count == 0 || _menuRoot == null ||
            _buttonContainer == null || _buttonPrefab == null || _canvasRoot == null)
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
        PositionWithinCanvas(screenPosition);
        return true;
    }

    public void Hide()
    {
        _pointerInside = false;
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

    private void PositionWithinCanvas(Vector2 screenPosition)
    {
        Canvas canvas = _canvasRoot.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRoot,
                screenPosition,
                camera,
                out Vector2 localPoint))
        {
            return;
        }

        Vector2 halfMenu = _menuRoot.rect.size * 0.5f;
        Rect bounds = _canvasRoot.rect;
        localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + halfMenu.x, bounds.xMax - halfMenu.x);
        localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + halfMenu.y, bounds.yMax - halfMenu.y);
        _menuRoot.anchoredPosition = localPoint;
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
