using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class RaidMinimapPrefabTests
{
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    [Test]
    public void NetworkPlayerPrefabContainsOneConfiguredLocalMinimap()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        RaidMinimapPresenter[] presenters = prefab.GetComponentsInChildren<RaidMinimapPresenter>(true);
        RaidMinimapView[] views = prefab.GetComponentsInChildren<RaidMinimapView>(true);
        Assert.That(presenters, Has.Length.EqualTo(1));
        Assert.That(views, Has.Length.EqualTo(1));
        Assert.That(views[0].GetComponent<CanvasGroup>(), Is.Not.Null);
        Assert.That(views[0].GetComponentInChildren<RectMask2D>(true), Is.Not.Null);
        Assert.That(views[0].GetComponentsInChildren<Camera>(true), Is.Empty);

        Image[] images = views[0].GetComponentsInChildren<Image>(true);
        Assert.That(images, Is.Not.Empty);
        Assert.That(images.All(image => !image.raycastTarget), Is.True);
        Image arrow = views[0].transform.Find("Viewport/SanctuaryArrow").GetComponent<Image>();
        Assert.That(arrow.sprite, Is.Not.Null);
        RaidMinimapGraphic map = views[0].transform.Find("Viewport/Map")
            .GetComponent<RaidMinimapGraphic>();
        Assert.That(map, Is.Not.Null);
        Assert.That(map.Layout, Is.Not.Null);
        Assert.That(map.raycastTarget, Is.False);

        SerializedObject binder = new SerializedObject(prefab.GetComponent<LocalPlayerHudBinder>());
        Assert.That(binder.FindProperty("_raidMinimapPresenter").objectReferenceValue,
            Is.SameAs(presenters[0]));
    }

    [Test]
    public void GeneratedLayoutMatchesTheThreeAuthorizedGrayboxTilemaps()
    {
        MinimapLayout layout = AssetDatabase.LoadAssetAtPath<MinimapLayout>(
            MinimapLayoutGenerator.LayoutAssetPath);
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout.TryValidate(out string validationError), Is.True, validationError);
        Assert.That(MinimapLayoutGenerator.TryBuildGeneratedData(out MinimapLayoutGeneratedData data,
            out string generationError), Is.True, generationError);
        Assert.That(layout.SourceHash, Is.EqualTo(data.SourceHash));
        Assert.That(layout.MinimumCell, Is.EqualTo(data.MinimumCell));
        Assert.That(layout.SizeInCells, Is.EqualTo(data.SizeInCells));
        Assert.That(layout.CellSize, Is.EqualTo(data.CellSize));
        Assert.That(layout.WorldPivotPosition, Is.EqualTo(data.WorldPivotPosition));

        for (int y = 0; y < data.SizeInCells.y; y++)
        {
            for (int x = 0; x < data.SizeInCells.x; x++)
            {
                Vector2Int cell = data.MinimumCell + new Vector2Int(x, y);
                Assert.That(
                    layout.GetCellFlags(cell),
                    Is.EqualTo((MinimapLayoutCellFlags)data.CellFlags[y * data.SizeInCells.x + x]));
            }
        }

        Assert.That(data.CellFlags.Any(flags => (flags & (byte)MinimapLayoutCellFlags.Floor) != 0), Is.True);
        Assert.That(data.CellFlags.Any(flags => (flags & (byte)MinimapLayoutCellFlags.Wall) != 0), Is.True);
        Assert.That(data.CellFlags.Any(flags => (flags & (byte)MinimapLayoutCellFlags.Obstacle) != 0), Is.True);
    }

    [Test]
    public void GraphicEmitsOneQuadForEachLayoutLayerWithoutTextureAssets()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        RaidMinimapGraphic graphic = prefab.transform.Find("LocalGameplayHud/RaidMinimap/Viewport/Map")
            .GetComponent<RaidMinimapGraphic>();
        Assert.That(graphic.TryConfigure(4f, out Vector2 uiSize, out string error), Is.True, error);
        Assert.That(uiSize, Is.EqualTo(graphic.Layout.WorldSize * 4f));

        using VertexHelper vertices = new VertexHelper();
        MethodInfo populate = typeof(RaidMinimapGraphic).GetMethod(
            "OnPopulateMesh",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            null,
            new[] { typeof(VertexHelper) },
            null);
        Assert.That(populate, Is.Not.Null);
        populate.Invoke(graphic, new object[] { vertices });

        int layerCount = 0;
        foreach (byte flags in EnumerateFlags(graphic.Layout))
        {
            layerCount += (flags & (byte)MinimapLayoutCellFlags.Floor) != 0 ? 1 : 0;
            layerCount += (flags & (byte)MinimapLayoutCellFlags.Wall) != 0 ? 1 : 0;
            layerCount += (flags & (byte)MinimapLayoutCellFlags.Obstacle) != 0 ? 1 : 0;
        }

        Assert.That(vertices.currentVertCount, Is.EqualTo(layerCount * 4));
        Assert.That(vertices.currentIndexCount, Is.EqualTo(layerCount * 6));
        Assert.That(AssetDatabase.FindAssets("GrayboxMinimap t:Texture2D"), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty);
    }

    [Test]
    public void ViewSeparatesInteriorIconAndExteriorArrowWithoutResidualRotation()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            RaidMinimapView view = instance.GetComponentInChildren<RaidMinimapView>(true);
            Image icon = instance.transform.Find("LocalGameplayHud/RaidMinimap/Viewport/SanctuaryIcon")
                .GetComponent<Image>();
            Image arrow = instance.transform.Find("LocalGameplayHud/RaidMinimap/Viewport/SanctuaryArrow")
                .GetComponent<Image>();

            view.PresentSanctuaryArrow(Vector2.right, 90f, 15f, Color.cyan, 1.2f);
            Assert.That(arrow.enabled, Is.True);
            Assert.That(icon.enabled, Is.False);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(arrow.rectTransform.localEulerAngles.z, 105f)),
                Is.LessThan(0.001f));

            view.PresentSanctuaryIcon(Vector2.up, Color.green, 0.8f);
            Assert.That(icon.enabled, Is.True);
            Assert.That(arrow.enabled, Is.False);
            Assert.That(icon.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(arrow.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
            // The active icon receives the current ritual scale; only the inactive
            // arrow must be restored to its neutral authored scale.
            Assert.That(icon.rectTransform.localScale, Is.EqualTo(Vector3.one * 0.8f));
            Assert.That(arrow.rectTransform.localScale, Is.EqualTo(Vector3.one));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void AssetsContainNoTemporaryTask68Tooling()
    {
        string[] temporaryFiles = System.IO.Directory.GetFiles(
            Application.dataPath,
            "*Task68*",
            System.IO.SearchOption.AllDirectories);
        Assert.That(temporaryFiles, Is.Empty);
    }

    private static byte[] EnumerateFlags(MinimapLayout layout)
    {
        Vector2Int size = layout.SizeInCells;
        byte[] flags = new byte[size.x * size.y];
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                flags[y * size.x + x] = (byte)layout.GetCellFlags(
                    layout.MinimumCell + new Vector2Int(x, y));
            }
        }

        return flags;
    }
}
