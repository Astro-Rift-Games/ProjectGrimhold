using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual-fidelity contract of the modular vitals: independent circular fills with one unscaled value each.
/// </summary>
public sealed class RaidVitalsPrefabTests
{
    private const string SourcePrefabPath = "Assets/Prefabs/UI/PlayerUI/RaidMainHud.prefab";
    private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab";
    private static readonly string[] Rows = { "Health", "Mana", "Stamina" };

    [Test]
    public void SourcePrefabHasNoStaticNameLabels()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        Assert.That(source, Is.Not.Null);

        foreach (TMP_Text text in source.GetComponentsInChildren<TMP_Text>(true))
        {
            Assert.That(text.name, Does.Not.EndWith("Label"), "static label object " + text.name);
        }

        Assert.That(
            source.GetComponentsInChildren<TMP_Text>(true).Length,
            Is.EqualTo(Rows.Length + 1),
            "one value text per vital plus the defeated indicator");
    }

    [Test]
    public void EveryRowHasOneValueTextInsideItsBarTrackAndNotInsideTheFill()
    {
        GameObject hud = Instantiate();
        try
        {
            Transform frame = hud.transform.Find("RaidMainHud");
            foreach (string row in Rows)
            {
                Transform track = frame.Find(row + "/" + row + "Track");
                Assert.That(track, Is.Not.Null, row + "Track");
                Transform fill = track.Find(row + "Fill");
                Transform value = track.Find(row + "TextValue");
                Assert.That(fill, Is.Not.Null, row + "Fill under its track");
                Assert.That(value, Is.Not.Null, row + "TextValue under its track");
                Assert.That(fill.GetComponentsInChildren<TMP_Text>(true), Is.Empty, row + " fill must not scale the text");
                Assert.That(value.GetSiblingIndex(), Is.GreaterThan(fill.GetSiblingIndex()), row + " text draws over the fill");

                TMP_Text text = value.GetComponent<TMP_Text>();
                Assert.That(text.alignment, Is.EqualTo(TextAlignmentOptions.Center), row + " value centered");
                Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(row == "Stamina" ? 10f : 16f), row + " native value size");
                Assert.That(text.enableAutoSizing, Is.False);
                Assert.That(text.font.name, Is.EqualTo("alagard SDF"), row + " pixel font");
            }
        }
        finally
        {
            Object.DestroyImmediate(hud);
        }
    }

    [Test]
    public void CircularFillsStayCenteredAndUseTheirAuthoredSpriteColors()
    {
        GameObject hud = Instantiate();
        try
        {
            RaidHudView view = hud.GetComponentInChildren<RaidHudView>(true);
            Image[] fills = { view.HealthFill, view.ManaFill, view.StaminaFill };
            Assert.That(fills, Has.None.Null);

            for (int i = 0; i < fills.Length; i++)
            {
                Image fill = fills[i];
                RectTransform rect = fill.rectTransform;
                RectTransform track = (RectTransform)rect.parent;
                Assert.That(track.name, Is.EqualTo(Rows[i] + "Track"), Rows[i] + " fill parent");
                Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), Rows[i] + " centred pivot");
                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(fill.color, Is.EqualTo(Color.white), "do not tint pre-coloured pixel art");
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
                Assert.That(rect.sizeDelta.x, Is.EqualTo(rect.sizeDelta.y), "circular canvas");
            }

            Assert.That(view.HealthFill.sprite.name, Is.EqualTo("orb_health_fill"));
            Assert.That(view.ManaFill.sprite.name, Is.EqualTo("orb_mana_fill"));
            Assert.That(view.StaminaFill.sprite.name, Is.EqualTo("ring_stamina_fill"));
        }
        finally
        {
            Object.DestroyImmediate(hud);
        }
    }

    [Test]
    public void ViewExposesTheManaWidgetsWiredFromTheHudPrefab()
    {
        GameObject hud = Instantiate();
        try
        {
            RaidHudView view = hud.GetComponentInChildren<RaidHudView>(true);
            Assert.That(view.ManaText, Is.Not.Null);
            Assert.That(view.ManaFill, Is.Not.Null);
            Assert.That(view.ManaText.transform.parent.name, Is.EqualTo("ManaTrack"));
            Assert.That(view.HealthText.transform.parent.name, Is.EqualTo("HealthTrack"));
            Assert.That(view.StaminaText.transform.parent.name, Is.EqualTo("StaminaTrack"));
        }
        finally
        {
            Object.DestroyImmediate(hud);
        }
    }

    private static GameObject Instantiate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        return Object.Instantiate(prefab);
    }
}
