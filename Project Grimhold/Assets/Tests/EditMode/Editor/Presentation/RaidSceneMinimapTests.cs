using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The minimap must show the Raid map that is actually loaded: each Raid scene declares its own
/// generated layout and the HUD never falls back to another scene's map.
/// </summary>
public sealed class RaidSceneMinimapTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (MinimapLayout layout in _layouts)
            if (layout != null) Object.DestroyImmediate(layout);
        _layouts.Clear();
    }

    private RaidSceneMinimap CreateDeclaration(MinimapLayout layout)
    {
        var go = new GameObject("RaidSceneMinimap", typeof(RaidSceneMinimap));
        _spawned.Add(go);
        var declaration = go.GetComponent<RaidSceneMinimap>();
        declaration.EditorConfigure(layout, null);
        return declaration;
    }

    private MinimapLayout CreateOtherLayout()
    {
        MinimapLayout layout = ScriptableObject.CreateInstance<MinimapLayout>();
        _layouts.Add(layout);
        return layout;
    }

    private readonly List<MinimapLayout> _layouts = new List<MinimapLayout>();

    private static MinimapLayout Load(string path)
    {
        var layout = AssetDatabase.LoadAssetAtPath<MinimapLayout>(path);
        Assert.That(layout, Is.Not.Null, path);
        return layout;
    }

    [Test]
    public void ResolveLoaded_ReturnsTheLayoutDeclaredByTheLoadedScene()
    {
        MinimapLayout declared = CreateOtherLayout();
        CreateDeclaration(declared);

        Assert.That(RaidSceneMinimap.TryResolveLoaded(out MinimapLayout resolved, out string error), Is.True, error);
        Assert.That(resolved, Is.SameAs(declared));
    }

    [Test]
    public void ResolveLoaded_FailsWithoutFallbackWhenNoSceneDeclaresAMinimap()
    {
        Assert.That(RaidSceneMinimap.TryResolveLoaded(out MinimapLayout resolved, out string error), Is.False);
        Assert.That(resolved, Is.Null);
        Assert.That(error, Does.Contain("found 0"));
    }

    [Test]
    public void ResolveLoaded_RejectsAmbiguousOrEmptyDeclarations()
    {
        RaidSceneMinimap first = CreateDeclaration(Load(MinimapLayoutGenerator.LayoutAssetPath));
        CreateDeclaration(CreateOtherLayout());
        Assert.That(RaidSceneMinimap.TryResolveLoaded(out _, out string error), Is.False);
        Assert.That(error, Does.Contain("found 2"));

        Object.DestroyImmediate(_spawned[1]);
        first.EditorConfigure(null, null);
        Assert.That(RaidSceneMinimap.TryResolveLoaded(out _, out error), Is.False);
        Assert.That(error, Does.Contain("has no MinimapLayout"));
    }

    [Test]
    public void Graphic_RendersTheLayoutItIsConfiguredWith()
    {
        var go = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RaidMinimapGraphic));
        _spawned.Add(go);
        var graphic = go.GetComponent<RaidMinimapGraphic>();
        MinimapLayout gameplay = Load(MinimapLayoutGenerator.LayoutAssetPath);

        Assert.That(graphic.TryConfigure(gameplay, 4f, out Vector2 size, out string error), Is.True, error);
        Assert.That(graphic.Layout, Is.SameAs(gameplay));
        Assert.That(size, Is.EqualTo(gameplay.RenderSize * 4f));
        Assert.That(graphic.WorldPivotPosition, Is.EqualTo(gameplay.WorldPivotPosition));
    }

    [Test]
    public void Graphic_BindingNeverRewritesTheSerializedPreview_AndNullDoesNotFallBack()
    {
        var go = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RaidMinimapGraphic));
        _spawned.Add(go);
        var graphic = go.GetComponent<RaidMinimapGraphic>();
        MinimapLayout gameplay = Load(MinimapLayoutGenerator.LayoutAssetPath);
        var serialized = new SerializedObject(graphic);
        Assert.That(serialized.FindProperty("_layout").objectReferenceValue, Is.Null);

        Assert.That(graphic.TryConfigure(gameplay, 4f, out _, out string error), Is.True, error);
        serialized.Update();
        Assert.That(serialized.FindProperty("_layout").objectReferenceValue, Is.Null);
        Assert.That(graphic.Layout, Is.SameAs(gameplay));

        Assert.That(graphic.TryConfigure(null, 4f, out _, out error), Is.False);
        Assert.That(error, Does.Contain("no MinimapLayout"));
    }

    [Test]
    public void Generator_FindsEveryRaidSceneAndKeepsOneLayoutPerScene()
    {
        List<string> scenes = MinimapLayoutGenerator.FindMinimapScenes();
        Assert.That(scenes, Does.Contain(MinimapLayoutGenerator.SourceScenePath));

        Assert.That(MinimapLayoutGenerator.TryBuildGeneratedData(MinimapLayoutGenerator.SourceScenePath,
            out MinimapLayoutGeneratedData data, out string layoutPath, out string error), Is.True, error);
        Assert.That(layoutPath, Is.EqualTo(MinimapLayoutGenerator.LayoutAssetPath));
        Assert.That(Load(layoutPath).SourceHash, Is.EqualTo(data.SourceHash), "Gameplay layout is stale.");
    }

    [Test]
    public void Generator_UnchangedScenesDoNotRewriteTheirLayouts()
    {
        byte[] gameplay = File.ReadAllBytes(MinimapLayoutGenerator.LayoutAssetPath);

        Assert.That(MinimapLayoutGenerator.EnsureCurrentLayout(out string error), Is.True, error);

        Assert.That(File.ReadAllBytes(MinimapLayoutGenerator.LayoutAssetPath), Is.EqualTo(gameplay));
    }

    [Test]
    public void EveryRaidSceneDeclaresItsOwnLayoutAndTilemapRoot()
    {
        var layouts = new HashSet<MinimapLayout>();
        foreach (string scenePath in MinimapLayoutGenerator.FindMinimapScenes())
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                RaidSceneMinimap found = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    RaidSceneMinimap candidate = root.GetComponentInChildren<RaidSceneMinimap>(true);
                    if (candidate != null) found = candidate;
                }

                Assert.That(found, Is.Not.Null, scenePath);
                Assert.That(found.Layout, Is.Not.Null, scenePath);
                Assert.That(found.SourceTilemapRoot, Is.Not.Null, scenePath);
                Assert.That(layouts.Add(found.Layout), Is.True, $"{scenePath} shares another scene's layout.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
