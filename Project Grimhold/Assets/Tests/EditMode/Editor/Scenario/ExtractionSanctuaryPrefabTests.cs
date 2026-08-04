using System.Linq;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Scenario
{
    public sealed class ExtractionSanctuaryPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/ExtractionSanctuary.prefab";
        private const string ScenePath = "Assets/Scenes/Gameplay.unity";

        [Test]
        public void ExtractionFlowHasNoStandaloneZonePrefab()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExtractionZone.prefab"),
                Is.Null);
        }

        [Test]
        public void Prefab_ComposesSanctuaryAndInteractionAreaUnderOneRoot()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ExtractionSanctuary>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ExtractionZone>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ExtractionSanctuaryPresenter>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<BoxCollider2D>(), Is.Not.Null);
            SpriteRenderer zoneRenderer = prefab.GetComponent<SpriteRenderer>();
            Assert.That(zoneRenderer, Is.Not.Null);
            Assert.That(zoneRenderer.enabled, Is.True);
            Assert.That(zoneRenderer.sortingLayerName, Is.EqualTo("Characters"));
            Assert.That(zoneRenderer.color.a, Is.GreaterThan(0f));
            Assert.That(prefab.GetComponent<IInteractable>(), Is.SameAs(prefab.GetComponent<ExtractionSanctuary>()));
            Assert.That(prefab.GetComponent<InteractionPromptMetadata>().PromptText, Is.EqualTo("Usar santuario"));
            Assert.That(prefab.layer, Is.EqualTo(LayerMask.NameToLayer("Interactable")));
            Assert.That(prefab.GetComponent<ExtractionZone>().IsAvailable, Is.False);
            Assert.That(
                typeof(ExtractionZone).GetField("_spriteRenderer", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        public void Gameplay_ContainsExactlyFourDistinctSanctuaryInstances()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                ExtractionSanctuary[] sanctuaries = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ExtractionSanctuary>(true))
                    .ToArray();

                Assert.That(sanctuaries, Has.Length.EqualTo(4));
                Assert.That(sanctuaries.Select(item => item.gameObject.name).Distinct().Count(), Is.EqualTo(4));
                Assert.That(sanctuaries.All(item => item.GetComponent<NetworkObject>() != null), Is.True);
                Assert.That(sanctuaries.All(item => item.GetComponent<ExtractionZone>() != null), Is.True);

                ExtractionZone[] zones = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ExtractionZone>(true))
                    .ToArray();
                Assert.That(zones, Has.Length.EqualTo(4));
                Assert.That(zones.All(zone => zone.GetComponent<ExtractionSanctuary>() != null), Is.True);

                Tilemap floor = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Tilemap>(true))
                    .Single(tilemap => tilemap.name == "Floor");
                foreach (ExtractionZone zone in zones)
                {
                    AssertZoneFootprintIsOnFloor(zone, floor);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }

        [Test]
        public void ExtractionPresentationAddsNoAudioOrVfxResources()
        {
            GameObject sanctuary = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(sanctuary.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(sanctuary.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            Assert.That(
                AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Scripts/Scenario/Extraction" }),
                Is.Empty);
        }

        private static void AssertZoneFootprintIsOnFloor(ExtractionZone zone, Tilemap floor)
        {
            Collider2D collider = zone.GetComponent<Collider2D>();
            Assert.That(collider, Is.Not.Null, zone.name);

            const float boundsInset = 0.01f;
            Bounds bounds = collider.bounds;
            Vector3 min = bounds.min + new Vector3(boundsInset, boundsInset, 0f);
            Vector3 max = bounds.max - new Vector3(boundsInset, boundsInset, 0f);
            Vector3Int minCell = floor.WorldToCell(min);
            Vector3Int maxCell = floor.WorldToCell(max);

            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    Assert.That(
                        floor.HasTile(cell),
                        Is.True,
                        $"{zone.name} footprint leaves Floor at cell {cell}.");
                }
            }
        }
    }
}
