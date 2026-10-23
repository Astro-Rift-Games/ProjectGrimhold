using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Generates the immutable minimap layout from the only authorized graybox Tilemaps.
/// It runs in the editor only and never participates in gameplay or networking.
/// </summary>
public static class MinimapLayoutGenerator
{
    public const string SourcePrefabPath = "Assets/Prefabs/Dungeon_Graybox.prefab";
    public const string SourceScenePath = "Assets/Scenes/Gameplay.unity";
    public const string LayoutAssetPath = "Assets/Data/Minimap/DungeonGrayboxMinimapLayout.asset";

    private static bool _isGenerating;
    private static readonly HashSet<string> SourceDependencies = new HashSet<string>();

    /// <summary>Regenerates the layout asset from Floor, Walls and Obstacles.</summary>
    [MenuItem("Tools/Project Grimhold/Regenerate Dungeon Minimap Layout")]
    public static void RegenerateFromMenu()
    {
        if (!EnsureCurrentLayout(out string error))
        {
            Debug.LogError(error);
        }
    }

    /// <summary>
    /// Regenerates the asset only when the serialized graybox data changed. This method is also
    /// used by the import hook and tests to keep the generated layout verifiable.
    /// </summary>
    public static bool EnsureCurrentLayout(out string error)
    {
        if (_isGenerating)
        {
            error = null;
            return true;
        }

        if (!TryBuildGeneratedData(out MinimapLayoutGeneratedData data, out error))
        {
            return false;
        }

        _isGenerating = true;
        try
        {
            EnsureAssetFolders();
            MinimapLayout layout = AssetDatabase.LoadAssetAtPath<MinimapLayout>(LayoutAssetPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<MinimapLayout>();
                AssetDatabase.CreateAsset(layout, LayoutAssetPath);
            }

            if (layout.SourceHash != data.SourceHash || !layout.TryValidate(out _))
            {
                layout.ApplyGeneratedData(
                    data.MinimumCell,
                    data.SizeInCells,
                    data.CellSize,
                    data.WorldPivotPosition,
                    data.CellFlags,
                    data.SourceHash,
                    data.RenderSize,
                    data.SourceSprites,
                    data.ArtBatches);
                EditorUtility.SetDirty(layout);
                // Do not save unrelated dirty prefabs, fonts or scenes.
                AssetDatabase.SaveAssetIfDirty(layout);
            }

            error = null;
            return true;
        }
        finally
        {
            _isGenerating = false;
        }
    }

    /// <summary>Builds deterministic layout data without modifying assets.</summary>
    public static bool TryBuildGeneratedData(
        out MinimapLayoutGeneratedData data,
        out string error)
    {
        data = default;
        Scene scene = default;
        try
        {
            // The saved scene contains authored Tilemap overrides absent from the prefab asset.
            // A preview scene reads those bytes without switching/saving the user's active scene.
            scene = EditorSceneManager.OpenPreviewScene(SourceScenePath);
            if (!TryFindSceneDungeon(scene, out GameObject root, out error) ||
                !TryFindSourceTilemaps(root, out Tilemap floor, out Tilemap walls,
                    out Tilemap obstacles, out error)) return false;
            CollectSourceDependencies(floor, walls, obstacles);
            return TryBuildGeneratedData(root, ComputeDependencyHash(),
                out data, out error);
        }
        catch (Exception exception)
        {
            error = $"{nameof(MinimapLayoutGenerator)} could not read {SourceScenePath}: {exception.Message}";
            return false;
        }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static bool TryFindSceneDungeon(Scene scene, out GameObject dungeon, out string error)
    {
        dungeon = null;
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                GameObject owner = candidate.gameObject;
                if (PrefabUtility.GetNearestPrefabInstanceRoot(owner) != owner ||
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(owner) != SourcePrefabPath) continue;
                if (dungeon != null)
                {
                    error = "Gameplay minimap requires exactly one Dungeon_Graybox prefab instance.";
                    return false;
                }
                dungeon = owner;
            }
        }

        error = dungeon != null ? null : "Gameplay minimap is missing its Dungeon_Graybox prefab instance.";
        return dungeon != null;
    }

    private static void CollectSourceDependencies(params Tilemap[] maps)
    {
        SourceDependencies.Clear();
        SourceDependencies.Add(SourceScenePath);
        SourceDependencies.Add(SourcePrefabPath);
        var tiles = new HashSet<TileBase>();
        foreach (Tilemap map in maps)
        {
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                TileBase tile = map.GetTile(cell);
                if (tile == null || !tiles.Add(tile)) continue;
                string path = AssetDatabase.GetAssetPath(tile);
                foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                    if (dependency != LayoutAssetPath) SourceDependencies.Add(dependency);
            }
        }
    }

    private static string ComputeDependencyHash()
    {
        var paths = new List<string>(SourceDependencies);
        paths.Sort(StringComparer.Ordinal);
        var signature = new StringBuilder();
        foreach (string path in paths)
        {
            signature.Append(path).Append(':');
            // Hash the source scene's own bytes, NOT its transitive dependency hash: the HUD
            // can reference the generated layout, which would make that hash self-invalidating.
            signature.Append(File.Exists(path) ? Hash128.Compute(File.ReadAllBytes(path)) :
                AssetDatabase.GetAssetDependencyHash(path));
            if (File.Exists(path + ".meta")) signature.Append(Hash128.Compute(File.ReadAllBytes(path + ".meta")));
        }
        return Hash128.Compute(signature.ToString()).ToString();
    }

    /// <summary>The same generation path, accepting an isolated source for deterministic Editor tests.</summary>
    public static bool TryBuildGeneratedData(GameObject root, string dependencyHash,
        out MinimapLayoutGeneratedData data, out string error)
    {
        data = default;
        if (root == null)
        {
            error = "Dungeon minimap source prefab is missing.";
            return false;
        }

        try
        {
            if (!TryFindSourceTilemaps(root, out Tilemap floor, out Tilemap walls,
                    out Tilemap obstacles, out error))
            {
                return false;
            }

            if (!TryGetCombinedBounds(floor, walls, obstacles, out BoundsInt bounds, out error) ||
                !TryGetGridMetrics(floor.layoutGrid, bounds, out Vector2 cellSize,
                    out Vector2 worldPivotPosition, out error))
            {
                return false;
            }

            int length = checked(bounds.size.x * bounds.size.y);
            byte[] cellFlags = new byte[length];
            AddTilemapFlags(floor, bounds, MinimapLayoutCellFlags.Floor, cellFlags);
            AddTilemapFlags(walls, bounds, MinimapLayoutCellFlags.Wall, cellFlags);
            AddTilemapFlags(obstacles, bounds, MinimapLayoutCellFlags.Obstacle, cellFlags);

            if (!MinimapTileArtBuilder.TryBuild(new[] { floor, walls, obstacles }, worldPivotPosition,
                    Vector2.Scale(new Vector2(bounds.size.x, bounds.size.y), cellSize),
                    out MinimapMeshBatch[] batches, out Sprite[] sprites, out Vector2 renderSize, out error))
                return false;

            data = new MinimapLayoutGeneratedData(
                new Vector2Int(bounds.xMin, bounds.yMin),
                new Vector2Int(bounds.size.x, bounds.size.y),
                cellSize,
                worldPivotPosition,
                cellFlags,
                ComputeSourceHash(bounds, cellSize, worldPivotPosition, cellFlags, dependencyHash, batches),
                renderSize, sprites, batches);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = $"{nameof(MinimapLayoutGenerator)} could not read {SourcePrefabPath}: {exception.Message}";
            return false;
        }
    }

    /// <summary>Checks import/delete/move paths without making the generated asset its own dependency.</summary>
    public static bool IsSourceDependency(string path)
    {
        if (path == SourceScenePath || path == SourcePrefabPath) return true;
        if (path == LayoutAssetPath) return false;
        if (SourceDependencies.Count == 0)
            foreach (string dependency in AssetDatabase.GetDependencies(SourceScenePath, true))
                if (dependency != LayoutAssetPath) SourceDependencies.Add(dependency);
        return SourceDependencies.Contains(path);
    }

    internal static void ScheduleRefresh()
    {
        EditorApplication.delayCall -= RefreshAfterImport;
        EditorApplication.delayCall += RefreshAfterImport;
    }

    private static void RefreshAfterImport()
    {
        if (!EnsureCurrentLayout(out string error))
        {
            Debug.LogError(error);
        }
    }

    private static bool TryFindSourceTilemaps(
        GameObject root,
        out Tilemap floor,
        out Tilemap walls,
        out Tilemap obstacles,
        out string error)
    {
        floor = null;
        walls = null;
        obstacles = null;
        bool hasDuplicate = false;
        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            switch (tilemaps[i].name)
            {
                case "Floor":
                    hasDuplicate |= floor != null;
                    floor = floor ?? tilemaps[i];
                    break;
                case "Walls":
                    hasDuplicate |= walls != null;
                    walls = walls ?? tilemaps[i];
                    break;
                case "Obstacles":
                    hasDuplicate |= obstacles != null;
                    obstacles = obstacles ?? tilemaps[i];
                    break;
            }
        }

        if (hasDuplicate || floor == null || walls == null || obstacles == null ||
            floor.layoutGrid == null || floor.layoutGrid != walls.layoutGrid ||
            floor.layoutGrid != obstacles.layoutGrid)
        {
            error = "Dungeon_Graybox requires exactly one Floor, Walls and Obstacles Tilemap on the same Grid.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryGetCombinedBounds(
        Tilemap floor,
        Tilemap walls,
        Tilemap obstacles,
        out BoundsInt bounds,
        out string error)
    {
        bool hasCells = false;
        Vector3Int minimum = default;
        Vector3Int maximumExclusive = default;
        IncludeOccupiedBounds(floor, ref hasCells, ref minimum, ref maximumExclusive);
        IncludeOccupiedBounds(walls, ref hasCells, ref minimum, ref maximumExclusive);
        IncludeOccupiedBounds(obstacles, ref hasCells, ref minimum, ref maximumExclusive);
        if (!hasCells)
        {
            bounds = default;
            error = "Dungeon_Graybox has no Floor, Walls or Obstacles cells to render.";
            return false;
        }

        bounds = new BoundsInt(minimum, maximumExclusive - minimum);
        error = null;
        return true;
    }

    private static void IncludeOccupiedBounds(
        Tilemap tilemap,
        ref bool hasCells,
        ref Vector3Int minimum,
        ref Vector3Int maximumExclusive)
    {
        BoundsInt tilemapBounds = tilemap.cellBounds;
        foreach (Vector3Int cell in tilemapBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell))
            {
                continue;
            }

            if (!hasCells)
            {
                hasCells = true;
                minimum = cell;
                maximumExclusive = cell + Vector3Int.one;
                continue;
            }

            minimum = Vector3Int.Min(minimum, cell);
            maximumExclusive = Vector3Int.Max(maximumExclusive, cell + Vector3Int.one);
        }
    }

    private static bool TryGetGridMetrics(
        GridLayout grid,
        BoundsInt bounds,
        out Vector2 cellSize,
        out Vector2 worldPivotPosition,
        out string error)
    {
        Vector3 minimumWorld = grid.CellToWorld(bounds.min);
        Vector3 maximumWorld = grid.CellToWorld(bounds.max);
        Vector3 xWorld = grid.CellToWorld(bounds.min + Vector3Int.right) - minimumWorld;
        Vector3 yWorld = grid.CellToWorld(bounds.min + Vector3Int.up) - minimumWorld;
        if (!IsAxisAligned(xWorld, yWorld) || xWorld.x <= 0f || yWorld.y <= 0f)
        {
            cellSize = default;
            worldPivotPosition = default;
            error = "Dungeon_Graybox minimap generation requires an axis-aligned positive XY Grid.";
            return false;
        }

        cellSize = new Vector2(xWorld.x, yWorld.y);
        worldPivotPosition = ((Vector2)minimumWorld + (Vector2)maximumWorld) * 0.5f;
        if (!IsPositiveFinite(cellSize.x) || !IsPositiveFinite(cellSize.y) ||
            !IsFinite(worldPivotPosition.x) || !IsFinite(worldPivotPosition.y))
        {
            error = "Dungeon_Graybox generated invalid minimap grid metrics.";
            return false;
        }

        error = null;
        return true;
    }

    private static void AddTilemapFlags(
        Tilemap tilemap,
        BoundsInt layoutBounds,
        MinimapLayoutCellFlags flag,
        byte[] cellFlags)
    {
        BoundsInt tilemapBounds = tilemap.cellBounds;
        foreach (Vector3Int cell in tilemapBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell))
            {
                continue;
            }

            int x = cell.x - layoutBounds.xMin;
            int y = cell.y - layoutBounds.yMin;
            cellFlags[y * layoutBounds.size.x + x] |= (byte)flag;
        }
    }

    private static string ComputeSourceHash(
        BoundsInt bounds,
        Vector2 cellSize,
        Vector2 worldPivotPosition,
        byte[] cellFlags,
        string dependencyHash,
        MinimapMeshBatch[] batches)
    {
        ulong hash = 14695981039346656037UL;
        HashInt(ref hash, bounds.xMin);
        HashInt(ref hash, bounds.yMin);
        HashInt(ref hash, bounds.size.x);
        HashInt(ref hash, bounds.size.y);
        HashInt(ref hash, cellSize.x.GetHashCode());
        HashInt(ref hash, cellSize.y.GetHashCode());
        HashInt(ref hash, worldPivotPosition.x.GetHashCode());
        HashInt(ref hash, worldPivotPosition.y.GetHashCode());
        for (int i = 0; i < cellFlags.Length; i++)
        {
            hash ^= cellFlags[i];
            hash *= 1099511628211UL;
        }

        // Version the generated representation as well as imported art and occupancy.
        foreach (char character in "scene-tile-art-v2:" + dependencyHash) HashInt(ref hash, character);
        foreach (MinimapMeshBatch batch in batches)
        {
            HashInt(ref hash, batch.VertexCount);
            HashInt(ref hash, batch.IndexCount);
            for (int i = 0; i < batch.VertexCount; i++)
            {
                HashInt(ref hash, batch.GetPosition(i).x.GetHashCode());
                HashInt(ref hash, batch.GetPosition(i).y.GetHashCode());
                HashInt(ref hash, batch.GetUv(i).x.GetHashCode());
                HashInt(ref hash, batch.GetUv(i).y.GetHashCode());
                Color32 color = batch.GetColor(i);
                HashInt(ref hash, color.r | color.g << 8 | color.b << 16 | color.a << 24);
            }
            for (int i = 0; i < batch.IndexCount; i++) HashInt(ref hash, batch.GetIndex(i));
        }

        return hash.ToString("X16");
    }

    private static void HashInt(ref ulong hash, int value)
    {
        unchecked
        {
            uint unsignedValue = (uint)value;
            for (int i = 0; i < 4; i++)
            {
                hash ^= (byte)(unsignedValue >> (i * 8));
                hash *= 1099511628211UL;
            }
        }
    }

    private static void EnsureAssetFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
        {
            AssetDatabase.CreateFolder("Assets", "Data");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Data/Minimap"))
        {
            AssetDatabase.CreateFolder("Assets/Data", "Minimap");
        }
    }

    private static bool IsAxisAligned(Vector3 xWorld, Vector3 yWorld)
    {
        const float Epsilon = 0.0001f;
        return Mathf.Abs(xWorld.y) < Epsilon && Mathf.Abs(yWorld.x) < Epsilon &&
            Mathf.Abs(xWorld.z) < Epsilon && Mathf.Abs(yWorld.z) < Epsilon;
    }

    private static bool IsPositiveFinite(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

/// <summary>Deterministic editor result used to validate generated minimap assets.</summary>
public readonly struct MinimapLayoutGeneratedData
{
    public Vector2Int MinimumCell { get; }
    public Vector2Int SizeInCells { get; }
    public Vector2 CellSize { get; }
    public Vector2 WorldPivotPosition { get; }
    public byte[] CellFlags { get; }
    public string SourceHash { get; }
    public Vector2 RenderSize { get; }
    public Sprite[] SourceSprites { get; }
    public MinimapMeshBatch[] ArtBatches { get; }

    public MinimapLayoutGeneratedData(
        Vector2Int minimumCell,
        Vector2Int sizeInCells,
        Vector2 cellSize,
        Vector2 worldPivotPosition,
        byte[] cellFlags,
        string sourceHash,
        Vector2 renderSize = default,
        Sprite[] sourceSprites = null,
        MinimapMeshBatch[] artBatches = null)
    {
        MinimumCell = minimumCell;
        SizeInCells = sizeInCells;
        CellSize = cellSize;
        WorldPivotPosition = worldPivotPosition;
        CellFlags = cellFlags;
        SourceHash = sourceHash;
        RenderSize = renderSize;
        SourceSprites = sourceSprites;
        ArtBatches = artBatches;
    }
}

internal sealed class DungeonMinimapLayoutPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string[] changes in new[] { importedAssets, deletedAssets, movedAssets, movedFromAssetPaths })
        {
            foreach (string path in changes)
            {
                if (!MinimapLayoutGenerator.IsSourceDependency(path)) continue;
                MinimapLayoutGenerator.ScheduleRefresh();
                return;
            }
        }
    }
}

[InitializeOnLoad]
internal static class DungeonMinimapLayoutBootstrap
{
    static DungeonMinimapLayoutBootstrap()
    {
        MinimapLayoutGenerator.ScheduleRefresh();
    }
}
