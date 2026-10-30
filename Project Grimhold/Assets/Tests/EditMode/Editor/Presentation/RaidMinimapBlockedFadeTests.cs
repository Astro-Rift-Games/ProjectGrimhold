using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class RaidMinimapBlockedFadeTests
{
    private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab";

    private GameObject _hud;
    private RaidMinimapView _view;
    private RaidMinimapGraphic _map;
    private CanvasGroup _visibility;
    private Image _localMarker;

    [SetUp]
    public void SetUp()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        _hud = Object.Instantiate(prefab);
        _view = _hud.GetComponentInChildren<RaidMinimapView>(true);
        Assert.That(_view, Is.Not.Null);
        _map = _view.transform.Find("Viewport/Map").GetComponent<RaidMinimapGraphic>();
        _visibility = _view.GetComponent<CanvasGroup>();
        _localMarker = _view.transform.Find("Viewport/LocalMarker").GetComponent<Image>();

        MinimapLayout layout = AssetDatabase.LoadAssetAtPath<MinimapLayout>(MinimapLayoutGenerator.LayoutAssetPath);
        Assert.That(_view.TryConfigureMap(layout, 4f, 1f, out string error), Is.True, error);
        _view.ShowMinimap();
        _view.PresentLocalMarker();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_hud);
    }

    [Test]
    public void BlockedMap_KeepsThePanelVisibleAndFadesTheMapToBlack()
    {
        float fade = _view.BlockedFadeSeconds;
        Assert.That(fade, Is.GreaterThan(0f));

        _view.PresentBlockedMap(fade * 0.5f);

        Assert.That(_visibility.alpha, Is.EqualTo(1f), "The frame stays visible instead of disappearing.");
        Assert.That(_localMarker.enabled, Is.False, "Blocked map shows no position.");
        Assert.That(_map.canvasRenderer.GetColor().r, Is.EqualTo(0.5f).Within(0.001f), "Halfway through the fade.");
        Assert.That(_map.canvasRenderer.GetColor().a, Is.EqualTo(1f), "Darkening never changes alpha.");

        _view.PresentBlockedMap(fade);

        Assert.That(_map.canvasRenderer.GetColor(), Is.EqualTo(Color.black));
        AssertEveryMapGraphic(Color.black);
    }

    [Test]
    public void UnblockedMap_FadesBackToItsNormalColor()
    {
        float fade = _view.BlockedFadeSeconds;
        _view.PresentBlockedMap(fade);

        _view.UpdateMapDarkening(false, fade * 0.25f);
        Assert.That(_map.canvasRenderer.GetColor().g, Is.EqualTo(0.25f).Within(0.001f));

        _view.UpdateMapDarkening(false, fade);
        Assert.That(_map.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
        AssertEveryMapGraphic(Color.white);
    }

    [Test]
    public void NormalMap_IsNeverDarkenedWithoutABlock()
    {
        _view.UpdateMapDarkening(false, 10f);

        Assert.That(_map.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
        Assert.That(_visibility.alpha, Is.EqualTo(1f));
    }

    private void AssertEveryMapGraphic(Color expected)
    {
        // Continuation batches are child graphics of the map and must darken with it.
        foreach (Graphic graphic in _map.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.canvasRenderer.GetColor(), Is.EqualTo(expected), graphic.name);
    }
}
