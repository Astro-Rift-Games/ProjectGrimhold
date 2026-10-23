#if UNITY_EDITOR
using System;
using System.Linq;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit, idempotent Editor authoring for TASK-220. Never generates gameplay UI at runtime.</summary>
public static class Task220Composition
{
    private const string Ui = "Assets/Prefabs/UI/PlayerUI/";

    [MenuItem("Grimhold/Author TASK-220 composition")]
    public static void Apply()
    {
        Edit(Ui + "LocalGameplayHud.prefab", ComposeRaidHud);
        Edit("Assets/Prefabs/NetworkPlayer.prefab", ComposePlayer);
        Edit("Assets/Prefabs/TownProgressionView.prefab", root =>
        {
            var view = root.GetComponentInChildren<TownProgressionView>(true);
            Set(view, "_pulseRoot", view.transform as RectTransform);
        });
        Edit("Assets/Prefabs/SocialPlayer.prefab", ComposeTown);
        Edit("Assets/Prefabs/Systems.prefab", ComposeRecovery);
        // SaveAsPrefabAsset persists only the targeted prefabs; never flush unrelated dirty assets.
        Debug.Log("TASK220_COMPOSITION_COMPLETE");
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

    private static void ComposeRaidHud(GameObject root)
    {
        var presenter = root.GetComponentInChildren<RaidHudPresenter>(true);
        if (presenter == null) throw new InvalidOperationException("Raid HUD presenter missing.");
        var vitals = Get<RaidHudView>(presenter, "_view");
        var icon = Get<Image>(vitals, "_cooldownIcon");
        if (icon == null) throw new InvalidOperationException("Authored weapon slot missing.");
        Transform slot = icon.transform.parent;
        var feedbackRoot = Rect(root.transform, "PlayerStatusFeedback", Vector2.zero, new Vector2(1, 1));
        var view = Require<RaidStatusFeedbackView>(feedbackRoot.gameObject);
        var previous = Button(slot, "PreviousWeaponSet", "^", new Vector2(0, 47), new Vector2(32, 18));
        var next = Button(slot, "NextWeaponSet", "v", new Vector2(0, -47), new Vector2(32, 18));
        var set = Label(slot, "ActiveWeaponSet", "A", new Vector2(26, 26), new Vector2(20, 20));
        Set(view, "_previousSetButton", previous);
        Set(view, "_nextSetButton", next);
        Set(view, "_activeSetText", set);
        Set(view, "_weaponPulseRoot", slot as RectTransform);
        var audio = Require<AudioSource>(feedbackRoot.gameObject);
        audio.playOnAwake = false;
        audio.spatialBlend = 0;
        Set(view, "_audioSource", audio);
        const string clipPath = "Assets/ImportedAssets/Free UI Click Sound Effects Pack/AUDIO/Button/SFX_UI_Button_Organic_Plastic_Generic_2.wav";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip == null) throw new InvalidOperationException("Confirmed UI switch clip missing: " + clipPath);
        Set(view, "_weaponChangedClip", clip);
        var notice = Rect(feedbackRoot, "Notice", new Vector2(0, 160), new Vector2(620, 44));
        var text = Label(notice, "Text", "", Vector2.zero, new Vector2(620, 44));
        Set(view, "_noticeRoot", notice.gameObject);
        Set(view, "_noticeText", text);
        notice.gameObject.SetActive(false);
        string[] fields = { "_empowerRoot", "_purificationRoot", "_immobilizedRoot" };
        string[] names = { "Empower", "Purification", "Immobilized" };
        string[] definitions = { "EmpowerDefinition", "PurificationDefinition", "TrapDefinition" };

        for (int i = 0; i < fields.Length; i++)
        {
            var effect = Rect(feedbackRoot, names[i], new Vector2((i - 1) * 38, 120), new Vector2(30,30));
            var image = Require<Image>(effect.gameObject);
            var definition = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(
                "Assets/Scriptable Objects/Abilities/Definitions/" + definitions[i] + ".asset");
            if (definition == null || definition.Icon == null)
                throw new InvalidOperationException("Required status icon missing: " + definitions[i]);
            image.sprite = definition.Icon;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;

            Set(view, fields[i], effect.gameObject);
            effect.gameObject.SetActive(false);
        }
        Set(presenter, "_statusFeedbackView", view);
        var quick = root.GetComponentInChildren<RaidQuickSlotHudPresenter>(true);
        if (quick != null) Set(quick, "_feedbackPresenter", presenter);
    }

    private static void ComposePlayer(GameObject root)
    {
        var immobilized = Require<PlayerImmobilizationNetworkEffect>(root);
        var purification = root.GetComponent<PurificationNetworkEffect>();
        Set(immobilized, "_character", root.GetComponent<PlayerCharacter>());
        Set(immobilized, "_purificationEffect", purification);
        Set(purification, "_immobilizationEffect", immobilized);
        var binder = root.GetComponent<LocalPlayerHudBinder>();
        Set(binder, "_movementController", root.GetComponent<PlayerMovementNetworkController>());
        Set(binder, "_empowerEffect", root.GetComponent<EmpowerNetworkEffect>());
        Set(binder, "_purificationEffect", purification);
        Set(binder, "_immobilizationEffect", immobilized);
        // Quick Slot controller lives on the avatar, not on the standalone HUD prefab.
        var presenter = Get<RaidHudPresenter>(binder, "_raidHudPresenter");
        var quick = Get<RaidQuickSlotHudPresenter>(binder, "_quickSlotHudPresenter");
        var status = root.GetComponentInChildren<RaidStatusFeedbackView>(true);
        Set(presenter, "_statusFeedbackView", status);
        Set(quick, "_feedbackPresenter", presenter);
        // Fusion's prefab importer owns the baked NetworkBehaviour list on reimport.
    }

    private static void ComposeTown(GameObject root)
    {
        var menu = root.GetComponentInChildren<TownPlayerMenuPresenter>(true);
        if (menu == null) throw new InvalidOperationException("Town player menu missing.");
        // The inventory canvas is inactive while its panel is closed. The access must live on an owned HUD surface.
        var statusRoot = Rect(root.transform, "TownStatusFeedbackCanvas", Vector2.zero, new Vector2(1920,1080));
        var canvas = Require<Canvas>(statusRoot.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = Require<CanvasScaler>(statusRoot.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        if (statusRoot.GetComponent<GraphicRaycaster>() == null) statusRoot.gameObject.AddComponent<GraphicRaycaster>();
        var access = Button(canvas.transform, "AttributesAccess", "Attributes", new Vector2(-110, 35), new Vector2(160,32));
        var rect = access.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1,0);
        var badge = Label(access.transform, "AvailablePointsBadge", "0", new Vector2(65,18), new Vector2(32,24));
        badge.color = new Color(1,.8f,.2f);
        Set(menu, "_attributeBadgeRoot", badge.gameObject);
        Set(menu, "_attributeBadgeText", badge);
        Set(menu, "_attributeAccessButton", access);
        access.gameObject.SetActive(false);
        badge.gameObject.SetActive(false);
        var serialized = new SerializedObject(menu);
        var hudRoots = serialized.FindProperty("_hudRoots");
        if (!Enumerable.Range(0, hudRoots.arraySize).Any(i => hudRoots.GetArrayElementAtIndex(i).objectReferenceValue == statusRoot.gameObject))
        {
            int index = hudRoots.arraySize++;
            hudRoots.GetArrayElementAtIndex(index).objectReferenceValue = statusRoot.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ComposeRecovery(GameObject root)
    {
        var coordinator = root.GetComponentInChildren<SessionConnectionCoordinator>(true);
        if (coordinator == null) throw new InvalidOperationException("Application coordinator missing.");
        var canvasRoot = Rect(coordinator.transform, "RecoveryWeaponConsentCanvas", Vector2.zero, new Vector2(1920,1080));
        var canvas = Require<Canvas>(canvasRoot.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = Require<CanvasScaler>(canvasRoot.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        if (canvasRoot.GetComponent<GraphicRaycaster>() == null) canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
        var view = Require<RecoveryWeaponConsentView>(canvasRoot.gameObject);
        var shade = Rect(canvasRoot, "Modal", Vector2.zero, new Vector2(1920,1080));
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        var image = Require<Image>(shade.gameObject);
        image.color = new Color(0,0,0,.85f);
        var message = Label(shade, "Message", "Free recovery weapon", new Vector2(0,55), new Vector2(650,100));
        var accept = Button(shade, "AcceptAndEnter", "Accept and enter", new Vector2(-135,-45), new Vector2(250,44));
        var cancel = Button(shade, "Cancel", "Cancel", new Vector2(135,-45), new Vector2(250,44));
        Set(view, "_coordinator", coordinator);
        Set(view, "_root", shade.gameObject);
        Set(view, "_acceptButton", accept);
        Set(view, "_cancelButton", cancel);
        Set(view, "_message", message);
        shade.gameObject.SetActive(false);
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var child = parent.Find(name);
        var rect = child != null ? child.GetComponent<RectTransform>() :
            new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, position, size);
        var label = Require<TextMeshProUGUI>(rect.gameObject);
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 20;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, position, size);
        var image = Require<Image>(rect.gameObject);
        image.color = new Color(.18f,.14f,.12f,.95f);
        var button = Require<Button>(rect.gameObject);
        button.targetGraphic = image;
        Label(rect, "Label", text, Vector2.zero, size);
        return button;
    }

    private static T Require<T>(GameObject owner) where T : Component
    {
        // Unity's missing-component wrappers compare equal to null but are not CLR null.
        var component = owner.GetComponent<T>();
        return component != null ? component : owner.AddComponent<T>();
    }
    private static T Get<T>(UnityEngine.Object target, string name) where T : UnityEngine.Object =>
        new SerializedObject(target).FindProperty(name).objectReferenceValue as T;

    private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        if (target == null || value == null) throw new InvalidOperationException("Missing " + name);
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException(target.name + " missing " + name);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
