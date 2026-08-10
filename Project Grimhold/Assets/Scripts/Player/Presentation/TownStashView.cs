using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local Canvas that hosts the existing persistent stash prefab in Town.
/// It owns presentation lifetime only and does not contain stash state.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownStashView : MonoBehaviour
{
    private GameObject _stashInstance;

    public bool IsOpen => _stashInstance != null && _stashInstance.activeSelf;

    public static TownStashView Create(Transform owner, GameObject stashInventoryPrefab)
    {
        if (owner == null || stashInventoryPrefab == null)
        {
            return null;
        }

        var root = new GameObject(
            "TownStashHud",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(TownStashView));
        root.transform.SetParent(owner, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TownStashView view = root.GetComponent<TownStashView>();
        view._stashInstance = Instantiate(stashInventoryPrefab, root.transform, false);
        view._stashInstance.name = "StashInventory";
        view._stashInstance.SetActive(false);
        return view;
    }

    public void Open()
    {
        _stashInstance?.SetActive(true);
    }

    public void Close()
    {
        _stashInstance?.SetActive(false);
    }

    private void OnDestroy()
    {
        _stashInstance = null;
    }
}
