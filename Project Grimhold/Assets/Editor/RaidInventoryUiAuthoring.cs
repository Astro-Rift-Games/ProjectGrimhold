#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the whole Raid inventory UI into <c>NetworkPlayer.prefab</c> so nothing is created at
/// runtime. Re-running the command rebuilds the slot pools in place and keeps every serialized
/// reference wired; the resulting hierarchy stays freely editable in the Inspector afterwards.
/// </summary>
public static class RaidInventoryUiAuthoring
{
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string SlotPrefabPath = "Assets/Prefabs/UI/RaidInventorySlot.prefab";
    private const string EquipmentPanelName = "EquipmentPanel";
    private const string EquipmentGridName = "EquipmentGrid";

    private const int InventorySlotCount = 16;
    private const int EquipmentColumns = 2;

    private static readonly string[] EquipmentSlotNames =
    {
        "WeaponSlot1", "WeaponSlot2", "Helmet", "Armor", "Gloves", "Boots"
    };

    [MenuItem("Grimhold/UI/Rebuild Raid Inventory UI")]
    public static void Rebuild()
    {
        if (!TryRebuild(out string error))
        {
            Debug.LogError($"Raid inventory UI authoring failed: {error}");
            EditorUtility.DisplayDialog("Raid Inventory UI", error, "OK");
            return;
        }

        Debug.Log("Raid inventory UI authoring completed.");
    }

    /// <summary>Batch entry point: <c>-executeMethod RaidInventoryUiAuthoring.RebuildBatch</c>.</summary>
    public static void RebuildBatch()
    {
        bool ok = TryRebuild(out string error);
        if (!ok)
        {
            Debug.LogError($"Raid inventory UI authoring failed: {error}");
        }
        else
        {
            Debug.Log("Raid inventory UI authoring completed.");
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool TryRebuild(out string error)
    {
        error = null;
        var slotPrefab = AssetDatabase.LoadAssetAtPath<RaidInventorySlotView>(SlotPrefabPath);
        if (slotPrefab == null)
        {
            error = $"Slot prefab not found at {SlotPrefabPath}.";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
        {
            error = $"Player prefab not found at {PlayerPrefabPath}.";
            return false;
        }

        try
        {
            var view = root.GetComponentInChildren<RaidInventoryView>(true);
            if (view == null)
            {
                error = "NetworkPlayer.prefab has no RaidInventoryView.";
                return false;
            }

            Transform panelsRow = FindChild(root.transform, "PanelsRow");
            Transform playerPanel = FindChild(root.transform, "PlayerInventoryPanel");
            Transform containerPanel = FindChild(root.transform, "ContainerLootPanel");
            if (panelsRow == null || playerPanel == null || containerPanel == null)
            {
                error = "Expected PanelsRow, PlayerInventoryPanel and ContainerLootPanel in the prefab.";
                return false;
            }

            // Panels keep their authored width instead of splitting the row evenly, so the
            // equipment panel can sit beside the inventory without squeezing it.
            var row = panelsRow.GetComponent<HorizontalLayoutGroup>();
            if (row != null)
            {
                row.childForceExpandWidth = false;
            }

            SetPreferredSize(playerPanel.gameObject, 700f, 580f);
            SetPreferredSize(containerPanel.gameObject, 700f, 580f);

            Transform equipmentGrid = BuildEquipmentPanel(panelsRow);
            RaidInventorySlotView[] equipmentSlots =
                BuildSlotPool(equipmentGrid, slotPrefab, EquipmentSlotNames);

            if (!TryFillPanel(root, playerPanel, slotPrefab, out error) ||
                !TryFillPanel(root, containerPanel, slotPrefab, out error))
            {
                return false;
            }

            WireEquipmentViews(view, equipmentSlots);
            EditorUtility.SetDirty(view);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool saved);
            if (!saved)
            {
                error = "Unity refused to save the player prefab.";
                return false;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return true;
    }

    private static bool TryFillPanel(
        GameObject root,
        Transform panel,
        RaidInventorySlotView slotPrefab,
        out string error)
    {
        error = null;
        var panelView = panel.GetComponent<RaidLootPanelView>();
        if (panelView == null)
        {
            error = $"{panel.name} has no RaidLootPanelView.";
            return false;
        }

        var serialized = new SerializedObject(panelView);
        SerializedProperty container = serialized.FindProperty("_slotContainer");
        if (container?.objectReferenceValue is not RectTransform slotContainer)
        {
            error = $"{panel.name} has no _slotContainer assigned.";
            return false;
        }

        var names = new string[InventorySlotCount];
        for (int index = 0; index < names.Length; index++)
        {
            names[index] = $"Slot{index:00}";
        }

        RaidInventorySlotView[] slots = BuildSlotPool(slotContainer, slotPrefab, names);

        SerializedProperty authored = serialized.FindProperty("_authoredSlots");
        authored.arraySize = slots.Length;
        for (int index = 0; index < slots.Length; index++)
        {
            authored.GetArrayElementAtIndex(index).objectReferenceValue = slots[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panelView);
        return true;
    }

    /// <summary>
    /// Rebuilds the slot children of <paramref name="parent"/> as nested prefab instances so the
    /// source slot prefab keeps driving their look.
    /// </summary>
    private static RaidInventorySlotView[] BuildSlotPool(
        Transform parent,
        RaidInventorySlotView slotPrefab,
        IReadOnlyList<string> names)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Transform child = parent.GetChild(index);
            if (child.GetComponent<RaidInventorySlotView>() != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        var created = new RaidInventorySlotView[names.Count];
        for (int index = 0; index < names.Count; index++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab.gameObject, parent);
            instance.name = names[index];
            instance.SetActive(true);
            created[index] = instance.GetComponent<RaidInventorySlotView>();
        }

        return created;
    }

    private static Transform BuildEquipmentPanel(Transform panelsRow)
    {
        Transform existing = FindChild(panelsRow, EquipmentPanelName);
        GameObject panel;
        if (existing != null)
        {
            panel = existing.gameObject;
        }
        else
        {
            panel = new GameObject(EquipmentPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.layer = panelsRow.gameObject.layer;
            panel.transform.SetParent(panelsRow, false);
            panel.transform.SetSiblingIndex(0);
        }

        var background = panel.GetComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);
        SetPreferredSize(panel, 372f, 580f);

        Transform grid = FindChild(panel.transform, EquipmentGridName);
        GameObject gridObject;
        if (grid != null)
        {
            gridObject = grid.gameObject;
        }
        else
        {
            gridObject = new GameObject(EquipmentGridName, typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.layer = panel.layer;
            gridObject.transform.SetParent(panel.transform, false);
        }

        var gridRect = (RectTransform)gridObject.transform;
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(0f, -8f);
        gridRect.sizeDelta = new Vector2(340f, 384f);

        var layout = gridObject.GetComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.cellSize = new Vector2(150f, 112f);
        layout.spacing = new Vector2(12f, 12f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = EquipmentColumns;
        layout.childAlignment = TextAnchor.MiddleCenter;

        return gridRect;
    }

    private static void WireEquipmentViews(RaidInventoryView view, RaidInventorySlotView[] slots)
    {
        string[] fields =
        {
            "_weaponSlot1View", "_weaponSlot2View", "_helmetView",
            "_armorView", "_glovesView", "_bootsView"
        };

        var serialized = new SerializedObject(view);
        for (int index = 0; index < fields.Length; index++)
        {
            SerializedProperty property = serialized.FindProperty(fields[index]);
            if (property == null)
            {
                Debug.LogError($"{nameof(RaidInventoryView)} has no field named {fields[index]}.");
                continue;
            }

            property.objectReferenceValue = slots[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPreferredSize(GameObject target, float width, float height)
    {
        var element = target.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = target.AddComponent<LayoutElement>();
        }

        element.ignoreLayout = false;
        element.minWidth = width;
        element.minHeight = height;
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindChild(root.GetChild(index), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
