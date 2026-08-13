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

        GameObject instance = Instantiate(stashInventoryPrefab, owner, false);
        instance.name = "StashInventory";

        TownStashView view = instance.AddComponent<TownStashView>();
        view._stashInstance = instance;

        Canvas canvas = instance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Optionally enforce sorting order to ensure it's on top
            canvas.sortingOrder = 110;
        }

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
