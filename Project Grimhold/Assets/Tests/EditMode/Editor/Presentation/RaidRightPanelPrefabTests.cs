using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class RaidRightPanelPrefabTests
{
    private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string PanelName = "RaidRightPanel";

    [Test]
    public void RightPanelStacksMinimapAndThreeBlocksInOneTopRightColumn()
    {
        Transform panel = LoadHud().transform.Find(PanelName);
        Assert.That(panel, Is.Not.Null);

        RectTransform rect = (RectTransform)panel;
        Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one));
        Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(rect.pivot, Is.EqualTo(Vector2.one));
        Assert.That(panel.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);

        Assert.That(panel.childCount, Is.EqualTo(4));
        Assert.That(panel.GetChild(0).name, Is.EqualTo("RaidMinimap"));
        Assert.That(panel.GetChild(1).name, Is.EqualTo("ObjectivesBlock"));
        Assert.That(panel.GetChild(2).name, Is.EqualTo("SanctuaryBlock"));
        Assert.That(panel.GetChild(3).name, Is.EqualTo("RitualBlock"));
        Assert.That(panel.GetComponentInChildren<RaidMinimapView>(true), Is.Not.Null);
    }

    [Test]
    public void ColumnStaysNarrowSoItNeverCoversTheCentralPlayArea()
    {
        RectTransform rect = (RectTransform)LoadHud().transform.Find(PanelName);
        Assert.That(rect, Is.Not.Null);
        // Reference canvas is 1920x1080 with match 0.5: a 300 unit column is under 16% of the width
        // even at the narrowest supported aspect ratio.
        Assert.That(rect.sizeDelta.x, Is.LessThanOrEqualTo(300f));
        Assert.That(rect.anchoredPosition.x, Is.LessThanOrEqualTo(0f));
    }

    [Test]
    public void EachBlockHasAStaticHeaderAndItsExistingLabelInsideTheColumn()
    {
        GameObject hud = LoadHud();
        RaidHudView view = hud.GetComponentInChildren<RaidHudView>(true);
        Assert.That(view, Is.Not.Null);
        Transform panel = hud.transform.Find(PanelName);
        Assert.That(panel, Is.Not.Null);

        AssertBlock(panel, "ObjectivesBlock", "Objetivos de expedición", view.QuotaText);
        AssertBlock(panel, "SanctuaryBlock", "Santuario", view.SanctuaryText);
        AssertBlock(panel, "RitualBlock", "Estado del ritual", view.ExtractionText);
    }

    [Test]
    public void PanelGraphicsDoNotBlockRaycasts()
    {
        Transform panel = LoadHud().transform.Find(PanelName);
        Assert.That(panel, Is.Not.Null);
        foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
        {
            Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }
    }

    [Test]
    public void NetworkPlayerKeepsOneCompleteViewResolvedInsideTheColumn()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(player, Is.Not.Null);
        RaidHudView[] views = player.GetComponentsInChildren<RaidHudView>(true);
        Assert.That(views, Has.Length.EqualTo(1));

        Transform panel = views[0].transform.Find(PanelName);
        Assert.That(panel, Is.Not.Null);
        Assert.That(views[0].QuotaText.transform.IsChildOf(panel), Is.True);
        Assert.That(views[0].SanctuaryText.transform.IsChildOf(panel), Is.True);
        Assert.That(views[0].ExtractionText.transform.IsChildOf(panel), Is.True);
        Assert.That(views[0].HealthText, Is.Not.Null);
        Assert.That(views[0].InventoryText, Is.Not.Null);
    }

    private static void AssertBlock(Transform panel, string blockName, string headerText, TMP_Text label)
    {
        Transform block = panel.Find(blockName);
        Assert.That(block, Is.Not.Null, blockName);
        // The frame is a child that ignores layout so the sprite size never drives the block height.
        Image frame = block.Find("Frame")?.GetComponent<Image>();
        Assert.That(frame, Is.Not.Null, blockName);
        Assert.That(frame.sprite, Is.Not.Null, blockName);
        Assert.That(frame.GetComponent<LayoutElement>().ignoreLayout, Is.True, blockName);
        Assert.That(frame.transform.GetSiblingIndex(), Is.EqualTo(0), blockName);

        TMP_Text header = block.Find("Header")?.GetComponent<TMP_Text>();
        Assert.That(header, Is.Not.Null, blockName);
        Assert.That(header.text, Is.EqualTo(headerText));

        Assert.That(label, Is.Not.Null, blockName);
        Assert.That(label.transform.parent, Is.SameAs(block), blockName);
        Assert.That(label.font, Is.SameAs(header.font), blockName);
    }

    private static GameObject LoadHud()
    {
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        Assert.That(hud, Is.Not.Null);
        return hud;
    }
}
