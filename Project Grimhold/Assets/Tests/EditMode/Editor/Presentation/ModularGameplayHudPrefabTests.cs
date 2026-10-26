using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ModularGameplayHudPrefabTests
{
    private const string Sprites = "Assets/Art/UI/Hud/Modular/";

    [Test]
    public void NativeOrbFramesAreConcentricWithoutLegacyRectangularBacking()
    {
        foreach (string path in new[] { "Assets/Prefabs/UI/PlayerUI/RaidMainHud.prefab", "Assets/Prefabs/NetworkPlayer.prefab" })
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Transform hud = path.EndsWith("NetworkPlayer.prefab")
                ? asset.transform.Find("LocalGameplayHud/RaidMainHud") : asset.transform;
            Assert.That(hud.GetComponent<Image>().enabled, Is.False, path + " legacy outer frame");
            foreach (string resource in new[] { "Health", "Mana" })
            {
                Transform holder = hud.Find(resource);
                RectTransform frame = (RectTransform)holder.Find("ModularHolder");
                Assert.That(frame.anchorMin, Is.EqualTo(Vector2.one * .5f), resource);
                Assert.That(frame.anchorMax, Is.EqualTo(Vector2.one * .5f), resource);
                Assert.That(frame.pivot, Is.EqualTo(Vector2.one * .5f), resource);
                Assert.That(frame.anchoredPosition, Is.EqualTo(Vector2.zero), resource);
                Assert.That(frame.sizeDelta, Is.EqualTo(new Vector2(80f, 80f)), resource);
                Assert.That(holder.Find("ModularSupport").GetComponent<Image>().enabled, Is.False, resource);
            }
        }
    }

    [Test]
    public void GameplayAvatarUsesTheModularOrbAndRingComposition()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        RaidHudView view = player.GetComponentInChildren<RaidHudView>(true);
        RectTransform frame = (RectTransform)view.MainHudRoot.transform;
        Assert.That(frame.sizeDelta, Is.EqualTo(new Vector2(512f, 128f)));
        Assert.That(frame.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
        Assert.That(frame.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.name == "StaminaTextValue").gameObject.activeSelf, Is.False);
        Assert.That(frame.localScale, Is.EqualTo(Vector3.one));
        Assert.That(frame.GetComponent<Image>().sprite.name, Is.EqualTo("outer_frame"));
        Filled(view.HealthFill, "orb_health_fill", Image.FillMethod.Vertical, 0);
        Filled(view.ManaFill, "orb_mana_fill", Image.FillMethod.Vertical, 0);
        Filled(view.StaminaFill, "ring_stamina_fill", Image.FillMethod.Radial360, 2);
        Assert.That(view.HealthFill.rectTransform.sizeDelta, Is.EqualTo(new Vector2(72f, 72f)));
        Assert.That(view.ManaFill.rectTransform.sizeDelta, Is.EqualTo(new Vector2(72f, 72f)));
        Assert.That(view.StaminaFill.rectTransform.sizeDelta, Is.EqualTo(new Vector2(96f, 96f)));
    }

    [Test]
    public void StatusRingUsesDistinctExplicitlyWiredReserveAndReviveRoots()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        RaidDownedHudView view = player.GetComponentInChildren<RaidDownedHudView>(true);
        Image reserve = Field<Image>(view, "_reserveFill");
        Image revive = Field<Image>(view, "_reviveFill");
        Filled(reserve, "ring_revive_fill", Image.FillMethod.Radial360, 2);
        Filled(revive, "ring_revive_fill", Image.FillMethod.Radial360, 2);
        GameObject reserveRoot = Field<GameObject>(view, "_reserveRingRoot");
        GameObject reviveRoot = Field<GameObject>(view, "_reviveRingRoot");
        Assert.That(((RectTransform)reserveRoot.transform).anchoredPosition, Is.EqualTo(new Vector2(16f, -16f)));
        Assert.That(reserveRoot, Is.Not.SameAs(reviveRoot));
        Assert.That(reserveRoot.activeSelf, Is.False);
        Assert.That(reviveRoot.activeSelf, Is.False);
        Assert.That(reserveRoot.transform.parent.name, Is.EqualTo("RaidMainHud"));
        Assert.That(reviveRoot.transform.parent, Is.SameAs(reserveRoot.transform.parent));
        Assert.That(((RectTransform)reserveRoot.transform).anchoredPosition,
            Is.EqualTo(((RectTransform)reviveRoot.transform).anchoredPosition));
    }

    [TestCase("outer_frame", 12f)]
    [TestCase("support_frame", 8f)]
    [TestCase("connector", 8f)]
    public void ImportedRectangularSpritesKeepTheirRealNineSliceBorders(string name, float border)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + name + ".png");
        Assert.That(sprite, Is.Not.Null);
        Assert.That(sprite.border, Is.EqualTo(Vector4.one * border));
        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));
        Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
    }

    private static void Filled(Image fill, string spriteName, Image.FillMethod method, int origin)
    {
        Assert.That(fill, Is.Not.Null);
        Assert.That(fill.sprite, Is.Not.Null);
        Assert.That(fill.sprite.name, Is.EqualTo(spriteName));
        Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
        Assert.That(fill.fillMethod, Is.EqualTo(method));
        Assert.That(fill.fillOrigin, Is.EqualTo(origin));
        Assert.That(fill.color, Is.EqualTo(Color.white));
        Assert.That(fill.raycastTarget, Is.False);
        Assert.That(fill.rectTransform.localScale, Is.EqualTo(Vector3.one));
    }

    private static T Field<T>(Component view, string name) where T : Object
    {
        FieldInfo field = view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        T value = field.GetValue(view) as T;
        Assert.That(value, Is.Not.Null, name);
        return value;
    }
}
