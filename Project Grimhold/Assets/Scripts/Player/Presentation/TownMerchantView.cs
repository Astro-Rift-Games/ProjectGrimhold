using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local Canvas that hosts the existing persistent merchant shop prefab in Town.
/// It owns presentation lifetime only and does not contain shop state.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownMerchantView : MonoBehaviour
{
    private GameObject _shopInstance;
    private MerchantShopUI _shopUI;

    public bool IsOpen => _shopInstance != null && _shopInstance.activeSelf;
    public MerchantShopUI ShopUI => _shopUI;

    public static TownMerchantView Create(Transform owner, GameObject merchantShopPrefab)
    {
        if (owner == null || merchantShopPrefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(merchantShopPrefab, owner, false);
        instance.name = "MerchantShopUI";

        TownMerchantView view = instance.AddComponent<TownMerchantView>();
        view._shopInstance = instance;
        view._shopUI = instance.GetComponent<MerchantShopUI>();
        
        if (view._shopUI == null)
        {
            Debug.LogWarning("MerchantShopPrefab is missing a MerchantShopUI component.", owner);
        }

        Canvas canvas = instance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Optionally enforce sorting order to ensure it's on top
            canvas.sortingOrder = 110;
        }
        
        view._shopInstance.SetActive(false);
        return view;
    }

    public void Open()
    {
        _shopInstance?.SetActive(true);
    }

    public void Close()
    {
        _shopInstance?.SetActive(false);
    }

    private void OnDestroy()
    {
        _shopInstance = null;
        _shopUI = null;
    }
}
