#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit, idempotent Editor authoring for TASK-282's quantity selector dialog prefab.</summary>
public static class Task282Composition
{
    public const string DialogPrefabPath = "Assets/Prefabs/UI/QuantitySelectorDialog.prefab";
    private const string RaidInventoryPrefabPath = "Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab";

    [MenuItem("Grimhold/Author TASK-282 quantity selector")]
    public static void Apply()
    {
        if (AssetDatabase.LoadAssetAtPath<QuantitySelectorDialogView>(DialogPrefabPath) == null)
            AuthorDialogPrefab();
        var dialog = AssetDatabase.LoadAssetAtPath<QuantitySelectorDialogView>(DialogPrefabPath)
            ?? throw new InvalidOperationException($"{DialogPrefabPath} has no {nameof(QuantitySelectorDialogView)}.");
        var inventoryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RaidInventoryPrefabPath);
        var inventoryView = inventoryPrefab.GetComponentInChildren<RaidInventoryView>(true);
        var inventorySettings = new SerializedObject(inventoryView);
        if (inventorySettings.FindProperty("_quantityDialogPrefab").objectReferenceValue != dialog ||
            inventorySettings.FindProperty("_quantityDialogParent").objectReferenceValue == null)
        {
            Edit(RaidInventoryPrefabPath, root =>
            {
                var view = root.GetComponentInChildren<RaidInventoryView>(true)
                    ?? throw new InvalidOperationException("Raid inventory view missing.");
                AssignDialog(view, view.ContextMenu, dialog);
            });
        }
        const string playerPath = "Assets/Prefabs/NetworkPlayer.prefab";
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (player.GetComponent<PlayerLootSplitNetworkController>() == null)
        {
            Edit(playerPath, root =>
            {
                var receiver = root.GetComponent<PlayerLootReceiver>()
                    ?? throw new InvalidOperationException("Network player receiver missing.");
                if (receiver.GetComponent<PlayerLootSplitNetworkController>() == null)
                    receiver.gameObject.AddComponent<PlayerLootSplitNetworkController>();
            });
        }
        const string stashPath = "Assets/Prefabs/UI/StashInventory.prefab";
        var stash = AssetDatabase.LoadAssetAtPath<GameObject>(stashPath).GetComponent<LobbyStashUI>();
        var stashSettings = new SerializedObject(stash);
        var menu = stashSettings.FindProperty("_contextMenu").objectReferenceValue as RaidLootContextMenuView;
        var parent = new SerializedObject(menu).FindProperty("_canvasRoot").objectReferenceValue;
        if (stashSettings.FindProperty("_quantityDialogPrefab").objectReferenceValue != dialog ||
            stashSettings.FindProperty("_quantityDialogParent").objectReferenceValue != parent)
        {
            Edit(stashPath, root =>
            {
                var view = root.GetComponent<LobbyStashUI>();
                var contextMenu = new SerializedObject(view).FindProperty("_contextMenu").objectReferenceValue as RaidLootContextMenuView;
                AssignDialog(view, contextMenu, dialog);
            });
        }
        // SaveAsPrefabAsset persists only the targeted prefabs; never flush unrelated dirty assets.
        Debug.Log("TASK282_COMPOSITION_COMPLETE");
    }

    private static void AuthorDialogPrefab()
    {
        var root = new GameObject("QuantitySelectorDialog", typeof(RectTransform));
        try
        {
            Stretch((RectTransform)root.transform);
            var view = root.AddComponent<QuantitySelectorDialogView>();

            RectTransform modal = Rect(root.transform, "Modal");
            Stretch(modal);
            var blocker = modal.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.6f);
            blocker.raycastTarget = true;

            RectTransform panel = Rect(modal, "Panel");
            panel.sizeDelta = new Vector2(380f, 220f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.1f, 0.09f, 0.98f);

            TMP_Text title = Label(panel, "Title", new Vector2(0f, 76f), new Vector2(340f, 36f));
            title.fontSize = 22f;

            var resources = new TMP_DefaultControls.Resources();
            GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            Place(sliderObject, panel, "QuantitySlider", new Vector2(0f, 24f), new Vector2(320f, 20f));
            var slider = sliderObject.GetComponent<Slider>();
            slider.wholeNumbers = true;

            GameObject inputObject = TMP_DefaultControls.CreateInputField(resources);
            Place(inputObject, panel, "QuantityInput", new Vector2(0f, -24f), new Vector2(120f, 36f));
            var input = inputObject.GetComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.textComponent.alignment = TextAlignmentOptions.Center;

            Button confirm = ButtonAt(panel, resources, "ConfirmButton", "Confirmar", new Vector2(-80f, -78f));
            Button cancel = ButtonAt(panel, resources, "CancelButton", "Cancelar", new Vector2(80f, -78f));

            Set(view, "_root", modal.gameObject);
            Set(view, "_title", title);
            Set(view, "_slider", slider);
            Set(view, "_input", input);
            Set(view, "_confirmButton", confirm);
            Set(view, "_cancelButton", cancel);
            modal.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, DialogPrefabPath, out bool saved);
            if (!saved) throw new InvalidOperationException($"Could not save {DialogPrefabPath}.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    /// <summary>Places the dialog on the same authored canvas root the context menu already positions within.</summary>
    private static void AssignDialog(Component view, RaidLootContextMenuView contextMenu, QuantitySelectorDialogView dialog)
    {
        if (contextMenu == null) throw new InvalidOperationException($"{view.GetType().Name} has no context menu.");
        var canvasRoot = new SerializedObject(contextMenu).FindProperty("_canvasRoot").objectReferenceValue as RectTransform;
        if (canvasRoot == null) throw new InvalidOperationException($"{view.GetType().Name} context menu has no canvas root.");
        Set(view, "_quantityDialogPrefab", dialog);
        Set(view, "_quantityDialogParent", canvasRoot);
    }

    private static void Edit(string path, Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Button ButtonAt(RectTransform parent, TMP_DefaultControls.Resources resources,
        string name, string label, Vector2 position)
    {
        GameObject buttonObject = TMP_DefaultControls.CreateButton(resources);
        Place(buttonObject, parent, name, position, new Vector2(140f, 40f));
        buttonObject.GetComponentInChildren<TMP_Text>(true).text = label;
        return buttonObject.GetComponent<Button>();
    }

    private static void Place(GameObject child, RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        child.name = name;
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static TMP_Text Label(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(parent, name);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field)
            ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} missing.");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
