using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public sealed class MinimapSceneSourceTests
{
    private const string GameplayPath = "Assets/Scenes/Gameplay.unity";

    [Test]
    public void GeneratedCellsAndWorldPivotMatchTheActualSavedGameplayDungeon()
    {
        Scene original = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenPreviewScene(GameplayPath);
        try
        {
            GameObject dungeon = FindDungeon(scene);
            Assert.That(MinimapLayoutGenerator.TryBuildGeneratedData(out MinimapLayoutGeneratedData data,
                out string error), Is.True, error);
            Tilemap[] maps = dungeon.GetComponentsInChildren<Tilemap>(true);
            foreach (Tilemap map in maps)
            {
                MinimapLayoutCellFlags flag = FlagFor(map.name);
                if (flag == MinimapLayoutCellFlags.None) continue;
                int expected = 0;
                foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                {
                    if (!map.HasTile(cell)) continue;
                    expected++;
                    int x = cell.x - data.MinimumCell.x;
                    int y = cell.y - data.MinimumCell.y;
                    Assert.That(x, Is.InRange(0, data.SizeInCells.x - 1), $"{map.name} {cell} outside generated X bounds");
                    Assert.That(y, Is.InRange(0, data.SizeInCells.y - 1), $"{map.name} {cell} outside generated Y bounds");
                    Assert.That((data.CellFlags[y * data.SizeInCells.x + x] & (byte)flag) != 0,
                        Is.True, $"Gameplay {map.name} {cell} must not be missing from minimap.");
                }

                Assert.That(data.CellFlags.Count(value => (value & (byte)flag) != 0), Is.EqualTo(expected), map.name);
            }

            Tilemap floor = maps.Single(map => map.name == "Floor");
            Vector3 min = floor.layoutGrid.CellToWorld(new Vector3Int(data.MinimumCell.x, data.MinimumCell.y));
            Vector3 max = floor.layoutGrid.CellToWorld(new Vector3Int(data.MinimumCell.x + data.SizeInCells.x,
                data.MinimumCell.y + data.SizeInCells.y));
            Assert.That(Vector2.Distance(data.WorldPivotPosition, ((Vector2)min + (Vector2)max) * 0.5f),
                Is.LessThan(0.00001f), "Scene placement is already included in the generated world pivot.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(original));
    }

    [Test]
    public void SavingGameplayInvalidatesTheLayoutWithoutMakingTheOutputItsOwnDependency()
    {
        Assert.That(MinimapLayoutGenerator.IsSourceDependency(GameplayPath), Is.True);
        Assert.That(MinimapLayoutGenerator.IsSourceDependency(MinimapLayoutGenerator.LayoutAssetPath), Is.False);
    }

    [Test]
    public void RepeatedSceneGenerationDoesNotRewriteSourceDesignOrCycleThroughTheGeneratedAsset()
    {
        byte[] scene = File.ReadAllBytes(GameplayPath);
        byte[] prefab = File.ReadAllBytes(MinimapLayoutGenerator.SourcePrefabPath);
        Assert.That(MinimapLayoutGenerator.EnsureCurrentLayout(out string error), Is.True, error);
        byte[] generated = File.ReadAllBytes(MinimapLayoutGenerator.LayoutAssetPath);
        Assert.That(MinimapLayoutGenerator.EnsureCurrentLayout(out error), Is.True, error);
        Assert.That(File.ReadAllBytes(MinimapLayoutGenerator.LayoutAssetPath), Is.EqualTo(generated));
        Assert.That(File.ReadAllBytes(GameplayPath), Is.EqualTo(scene));
        Assert.That(File.ReadAllBytes(MinimapLayoutGenerator.SourcePrefabPath), Is.EqualTo(prefab));
    }

    private static GameObject FindDungeon(Scene scene)
    {
        GameObject[] matches = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(owner => PrefabUtility.GetNearestPrefabInstanceRoot(owner) == owner &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(owner) == MinimapLayoutGenerator.SourcePrefabPath)
            .ToArray();
        Assert.That(matches, Has.Length.EqualTo(1));
        return matches[0];
    }

    private static MinimapLayoutCellFlags FlagFor(string name)
    {
        return name == "Floor" ? MinimapLayoutCellFlags.Floor : name == "Walls" ? MinimapLayoutCellFlags.Wall :
            name == "Obstacles" ? MinimapLayoutCellFlags.Obstacle : MinimapLayoutCellFlags.None;
    }
}
