using System.Linq;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using ProjectGrimhold.Gameplay.Visibility;
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
            EntityVisibilityPresenter visibilityPresenter = prefab.GetComponent<EntityVisibilityPresenter>();
            Assert.That(visibilityPresenter, Is.Not.Null);
            SerializedObject serializedVisibility = new SerializedObject(visibilityPresenter);
            Assert.That(
                serializedVisibility.FindProperty("_visibilityCollider").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<BoxCollider2D>()));
            Assert.That(
                serializedVisibility.FindProperty("_useColliderBounds").boolValue,
                Is.True);
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
        public void ExtractionPresentationUsesAuthoredCuesWithoutAddingGameplayOrAudioComponents()
        {
            GameObject sanctuary = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            ExtractionSanctuaryPresenter presenter = sanctuary.GetComponent<ExtractionSanctuaryPresenter>();
            SerializedObject serializedPresenter = new SerializedObject(presenter);

            Assert.That(sanctuary.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(sanctuary.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            Assert.That(sanctuary.GetComponentsInChildren<NetworkBehaviour>(true),
                Has.Length.EqualTo(1));
            Assert.That(sanctuary.GetComponent<NetworkBehaviour>(), Is.SameAs(sanctuary.GetComponent<ExtractionSanctuary>()));

            string[] audioCueFields =
            {
                "_assignedCue",
                "_ritualStartedCue",
                "_ritualCancelledCue",
                "_ritualCompletedCue"
            };
            foreach (string fieldName in audioCueFields)
            {
                SerializedProperty cue = serializedPresenter.FindProperty(fieldName);
                SerializedProperty clips = cue.FindPropertyRelative("_clips");
                Assert.That(
                    clips.arraySize,
                    Is.GreaterThan(0),
                    $"Expected an authored clip for {fieldName}.");
                Assert.That(clips.GetArrayElementAtIndex(0).objectReferenceValue, Is.Not.Null);
                Assert.That(cue.FindPropertyRelative("_spatialBlend").floatValue, Is.EqualTo(1f));
            }

            ParticleSystem authoredParticleCue = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/VFX/HealingParticles.prefab").GetComponent<ParticleSystem>();
            Assert.That(serializedPresenter.FindProperty("_ritualCompletedParticles").objectReferenceValue,
                Is.SameAs(authoredParticleCue));
        }

        [Test]
        public void Presenter_OnlyEmitsCuesForObservedConfirmedTransitions()
        {
            AssertCue(
                "None",
                false,
                ExtractionRitualState.NotStarted,
                ExtractionRitualState.InProgress,
                false,
                true,
                true);
            AssertCue(
                "None",
                true,
                ExtractionRitualState.InProgress,
                ExtractionRitualState.InProgress,
                true,
                true,
                true);
            AssertCue("Started", true, ExtractionRitualState.NotStarted,
                ExtractionRitualState.InProgress, true, true, false);
            AssertCue("Assigned", true, ExtractionRitualState.NotStarted,
                ExtractionRitualState.NotStarted, false, true, true);
            AssertCue("Cancelled", true, ExtractionRitualState.InProgress,
                ExtractionRitualState.Cancelled, true, true, true);
            AssertCue("None", true, ExtractionRitualState.InProgress,
                ExtractionRitualState.Cancelled, true, true, false);
            AssertCue("Completed", true, ExtractionRitualState.InProgress,
                ExtractionRitualState.Completed, true, true, false);
        }

        private static void AssertCue(
            string expected,
            bool hasPreviousSnapshot,
            ExtractionRitualState previousState,
            ExtractionRitualState currentState,
            bool previouslyReserved,
            bool currentlyReserved,
            bool isLocalOwner)
        {
            MethodInfo resolver = typeof(ExtractionSanctuaryPresenter).GetMethod(
                "ResolveObservedCues",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolver, Is.Not.Null);
            object result = resolver.Invoke(null, new object[]
            {
                hasPreviousSnapshot,
                previousState,
                currentState,
                previouslyReserved,
                currentlyReserved,
                isLocalOwner
            });
            Assert.That(result.ToString(), Is.EqualTo(expected));
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
