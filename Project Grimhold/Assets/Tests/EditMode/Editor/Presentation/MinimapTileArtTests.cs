using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public sealed class MinimapTileArtTests
{
    private GameObject _root;
    private Tilemap _floor;
    private Tilemap _walls;
    private Tilemap _obstacles;
    private Texture2D _texture;
    private Texture2D _otherTexture;
    private Sprite _first;
    private Sprite _second;
    private Sprite _other;
    private Tile _tile;
    private Tile _otherTile;
    private MinimapLayout _layout;
    private GameObject _graphicOwner;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("IsolatedDungeon", typeof(Grid));
        _floor = CreateMap("Floor", 0);
        _walls = CreateMap("Walls", 1);
        _obstacles = CreateMap("Obstacles", 3);
        _texture = new Texture2D(16, 8) { filterMode = FilterMode.Point };
        _otherTexture = new Texture2D(8, 8) { filterMode = FilterMode.Point };
        _first = Sprite.Create(_texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 8, 0, SpriteMeshType.FullRect);
        _second = Sprite.Create(_texture, new Rect(8, 0, 8, 8), Vector2.one * 0.5f, 8, 0, SpriteMeshType.FullRect);
        _other = Sprite.Create(_otherTexture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 8, 0, SpriteMeshType.FullRect);
        _tile = ScriptableObject.CreateInstance<Tile>();
        _tile.sprite = _first;
        _tile.flags = TileFlags.None;
        _otherTile = ScriptableObject.CreateInstance<Tile>();
        _otherTile.sprite = _other;
        _layout = ScriptableObject.CreateInstance<MinimapLayout>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_graphicOwner);
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_layout);
        Object.DestroyImmediate(_tile);
        Object.DestroyImmediate(_otherTile);
        Object.DestroyImmediate(_first);
        Object.DestroyImmediate(_second);
        Object.DestroyImmediate(_other);
        Object.DestroyImmediate(_texture);
        Object.DestroyImmediate(_otherTexture);
    }

    [Test]
    public void SparseNegativeCellsAndHolesMatchOnlyTheThreePermittedLayers()
    {
        _floor.SetTile(new Vector3Int(-2, -3), _tile);
        _floor.SetTile(new Vector3Int(1, 2), _tile);
        _walls.SetTile(Vector3Int.zero, _tile);
        CreateMap("LootAndCharacters", 100).SetTile(new Vector3Int(100, 100), _otherTile);
        MinimapLayoutGeneratedData data = Build();
        Assert.That(data.MinimumCell, Is.EqualTo(new Vector2Int(-2, -3)));
        Assert.That(data.SizeInCells, Is.EqualTo(new Vector2Int(4, 6)));
        Assert.That(data.ArtBatches.Sum(batch => batch.VertexCount), Is.EqualTo(12));
        Apply(data);
        Assert.That(_layout.GetCellFlags(new Vector2Int(-2, -3)), Is.EqualTo(MinimapLayoutCellFlags.Floor));
        Assert.That(_layout.GetCellFlags(new Vector2Int(-1, -2)), Is.EqualTo(MinimapLayoutCellFlags.None));
        Assert.That(_layout.GetCellFlags(new Vector2Int(100, 100)), Is.EqualTo(MinimapLayoutCellFlags.None));
        Assert.That(data.SourceSprites, Has.No.Member(_other));
        _floor.SetTile(new Vector3Int(-2, -3), null);
        Assert.That(Build().SourceHash, Is.Not.EqualTo(data.SourceHash));
    }

    [Test]
    public void SpriteOnlySwapChangesUvsAndHashWithoutChangingOccupancy()
    {
        _floor.SetTile(Vector3Int.zero, _tile);
        MinimapLayoutGeneratedData before = Build();
        _tile.sprite = _second;
        _floor.RefreshAllTiles();
        MinimapLayoutGeneratedData after = Build();
        Assert.That(after.CellFlags, Is.EqualTo(before.CellFlags));
        Assert.That(after.SourceHash, Is.Not.EqualTo(before.SourceHash));
        Assert.That(after.ArtBatches[0].GetUv(0), Is.EqualTo(_second.uv[0]));
        Assert.That(after.ArtBatches[0].GetUv(0), Is.Not.EqualTo(before.ArtBatches[0].GetUv(0)));
        Assert.That(Build().SourceHash, Is.EqualTo(after.SourceHash));
        Assert.That(Build("texture-reimport-v2").SourceHash, Is.Not.EqualTo(after.SourceHash));
    }

    [Test]
    public void TransformedTilePreservesSpriteGeometryUvsTintAndWorldAlignment()
    {
        Vector3Int cell = new Vector3Int(-2, 1);
        _floor.transform.localPosition = new Vector3(3, -4);
        _floor.tileAnchor = new Vector3(0.25f, 0.75f);
        _floor.color = new Color(0.5f, 1, 1, 0.75f);
        _floor.SetTile(cell, _tile);
        Color cellColor = new Color(1, 0.25f, 0.5f, 0.5f);
        _floor.SetColor(cell, cellColor);
        Matrix4x4 transform = Matrix4x4.TRS(new Vector3(1, 0.5f),
            Quaternion.Euler(0, 0, 90), new Vector3(-2, 1, 1));
        _floor.SetTransformMatrix(cell, transform);
        MinimapLayoutGeneratedData data = Build();
        MinimapMeshBatch batch = data.ArtBatches[0];
        Vector3 anchor = new Vector3(cell.x + 0.25f, cell.y + 0.75f);
        for (int i = 0; i < _first.vertices.Length; i++)
        {
            Vector2 world = _floor.transform.TransformPoint(anchor + transform.MultiplyPoint3x4(_first.vertices[i]));
            Assert.That(Vector2.Distance(batch.GetPosition(i) + data.WorldPivotPosition, world), Is.LessThan(0.00001f));
            Assert.That(batch.GetUv(i), Is.EqualTo(_first.uv[i]));
            Assert.That(batch.GetColor(i), Is.EqualTo((Color32)(_floor.color * cellColor)));
        }
        Assert.That(data.RenderSize.x, Is.GreaterThan(data.CellSize.x));
        Assert.That(data.RenderSize.y, Is.GreaterThan(data.CellSize.y));
    }

    [Test]
    public void LayerOrderAndInterleavedTexturesRemainInPainterOrder()
    {
        _floor.SetTile(Vector3Int.zero, _tile);
        _walls.SetTile(Vector3Int.zero, _otherTile);
        _obstacles.SetTile(Vector3Int.zero, _tile);
        _floor.color = Color.red;
        _walls.color = Color.green;
        _obstacles.color = Color.blue;
        MinimapLayoutGeneratedData data = Build();
        Assert.That(data.ArtBatches, Has.Length.EqualTo(3));
        Assert.That(data.ArtBatches.Select(batch => batch.Texture), Is.EqualTo(new[] { _texture, _otherTexture, _texture }));
        Assert.That(data.ArtBatches.Select(batch => batch.GetColor(0)),
            Is.EqualTo(new Color32[] { Color.red, Color.green, Color.blue }));
        _floor.GetComponent<TilemapRenderer>().sortingOrder = 10;
        MinimapMeshBatch last = Build().ArtBatches.Last();
        Assert.That(last.GetColor(last.VertexCount - 1), Is.EqualTo((Color32)Color.red));
    }

    [TestCase(TilemapRenderer.SortOrder.BottomLeft, 0f, 0f)]
    [TestCase(TilemapRenderer.SortOrder.TopRight, 1f, 1f)]
    public void TileOrderingStartsAtTheAuthoredRendererCorner(TilemapRenderer.SortOrder order, float x, float y)
    {
        _floor.SetTile(Vector3Int.zero, _tile);
        _floor.SetTile(new Vector3Int(1, 1), _tile);
        _floor.GetComponent<TilemapRenderer>().sortOrder = order;
        MinimapLayoutGeneratedData data = Build();
        Vector2 firstWorld = data.ArtBatches[0].GetPosition(0) + data.WorldPivotPosition;
        Assert.That(firstWorld, Is.EqualTo(new Vector2(x, y) + Vector2.one * 0.5f + _first.vertices[0]));
    }

    [Test]
    public void LargeGeometrySplitsBeforeTheUguiVertexLimitWithoutDroppingTiles()
    {
        int count = MinimapMeshBatch.MaxVertexCount / 4 + 1;
        var tiles = Enumerable.Repeat<TileBase>(_tile, count).ToArray();
        _floor.SetTilesBlock(new BoundsInt(0, 0, 0, count, 1, 1), tiles);
        MinimapLayoutGeneratedData data = Build();
        Assert.That(data.ArtBatches, Has.Length.EqualTo(2));
        Assert.That(data.ArtBatches.Sum(batch => batch.VertexCount), Is.EqualTo(count * 4));
        foreach (MinimapMeshBatch batch in data.ArtBatches)
        {
            Assert.That(batch.VertexCount, Is.LessThanOrEqualTo(MinimapMeshBatch.MaxVertexCount));
            Assert.That(batch.TryValidate(out string error), Is.True, error);
        }
    }

    [Test]
    public void GraphicUsesOneMaskedChildPerContinuationBatchAndClearsOnInvalidRebind()
    {
        _floor.SetTile(Vector3Int.zero, _tile);
        _walls.SetTile(Vector3Int.zero, _otherTile);
        _obstacles.SetTile(Vector3Int.zero, _tile);
        Apply(Build());
        _graphicOwner = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RaidMinimapGraphic));
        var graphic = _graphicOwner.GetComponent<RaidMinimapGraphic>();
        var serialized = new SerializedObject(graphic);
        serialized.FindProperty("_layout").objectReferenceValue = _layout;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(graphic.TryConfigure(4, out Vector2 size, out string error), Is.True, error);
        Assert.That(size, Is.EqualTo(_layout.RenderSize * 4));
        Assert.That(graphic.mainTexture, Is.SameAs(_texture));
        var children = _graphicOwner.GetComponentsInChildren<RaidMinimapBatchGraphic>(true);
        Assert.That(children, Has.Length.EqualTo(2));
        Assert.That(children.All(child => !child.raycastTarget && child.maskable), Is.True);
        using var vertices = new VertexHelper();
        typeof(RaidMinimapGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Invoke(graphic, new object[] { vertices });
        Assert.That(vertices.currentVertCount, Is.EqualTo(_layout.GetArtBatch(0).VertexCount));
        UIVertex first = default;
        vertices.PopulateUIVertex(ref first, 0);
        Assert.That((Vector2)first.position, Is.EqualTo(_layout.GetArtBatch(0).GetPosition(0) * 4));
        Assert.That((Vector2)first.uv0, Is.EqualTo(_layout.GetArtBatch(0).GetUv(0)));
        graphic.enabled = false;
        Assert.That(children.All(child => !child.gameObject.activeSelf), Is.True);
        graphic.enabled = true;
        Assert.That(children.All(child => child.gameObject.activeSelf), Is.True);
        Assert.That(graphic.TryConfigure(float.NaN, out _, out _), Is.False);
        Assert.That(children.All(child => !child.gameObject.activeSelf), Is.True);
    }

    [Test]
    public void MissingSpriteAndInvalidMeshFailClearlyInsteadOfUsingSchematicFallback()
    {
        _floor.SetTile(Vector3Int.zero, _tile);
        _tile.sprite = null;
        _floor.RefreshAllTiles();
        Assert.That(MinimapLayoutGenerator.TryBuildGeneratedData(_root, "fixture", out _, out string error), Is.False);
        Assert.That(error, Does.Contain("no tile sprite"));
        var invalid = new MinimapMeshBatch(_texture, new Vector2[3], new Vector2[3], new Color32[3], new[] { 0, 1, 3 });
        Assert.That(invalid.TryValidate(out error), Is.False);
        Assert.That(error, Does.Contain("out-of-range"));
    }

    [Test]
    public void SourceDependencyIncludesImportedArtButExcludesTheGeneratedAsset()
    {
        Assert.That(MinimapLayoutGenerator.IsSourceDependency(MinimapLayoutGenerator.SourcePrefabPath), Is.True);
        Assert.That(MinimapLayoutGenerator.IsSourceDependency("Assets/Resources/ProvisionalTileset 48x56 (Tile 8x8).png"), Is.True);
        Assert.That(MinimapLayoutGenerator.IsSourceDependency(MinimapLayoutGenerator.LayoutAssetPath), Is.False);
        Assert.That(MinimapLayoutGenerator.IsSourceDependency("Assets/Unrelated.png"), Is.False);
    }

    [Test]
    public void PrebuildRegenerationIsFreshAndAnUnchangedSecondPassDoesNotRewriteTheAsset()
    {
        new MinimapLayoutBuildPreprocessor().OnPreprocessBuild(null);
        byte[] before = File.ReadAllBytes(MinimapLayoutGenerator.LayoutAssetPath);
        var generated = AssetDatabase.LoadAssetAtPath<MinimapLayout>(MinimapLayoutGenerator.LayoutAssetPath);
        Assert.That(generated.TryValidate(out string error), Is.True, error);
        Assert.That(EditorUtility.IsDirty(generated), Is.False);
        new MinimapLayoutBuildPreprocessor().OnPreprocessBuild(null);
        Assert.That(File.ReadAllBytes(MinimapLayoutGenerator.LayoutAssetPath), Is.EqualTo(before));
        Assert.That(EditorUtility.IsDirty(generated), Is.False);
    }

    private Tilemap CreateMap(string name, int order)
    {
        var owner = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        owner.transform.SetParent(_root.transform, false);
        owner.GetComponent<TilemapRenderer>().sortingOrder = order;
        return owner.GetComponent<Tilemap>();
    }

    private MinimapLayoutGeneratedData Build(string dependencyHash = "fixture-art-v1")
    {
        Assert.That(MinimapLayoutGenerator.TryBuildGeneratedData(_root, dependencyHash,
            out MinimapLayoutGeneratedData data, out string error), Is.True, error);
        return data;
    }

    private void Apply(MinimapLayoutGeneratedData data)
    {
        _layout.ApplyGeneratedData(data.MinimumCell, data.SizeInCells, data.CellSize, data.WorldPivotPosition,
            data.CellFlags, data.SourceHash, data.RenderSize, data.SourceSprites, data.ArtBatches);
        Assert.That(_layout.TryValidate(out string error), Is.True, error);
    }
}
