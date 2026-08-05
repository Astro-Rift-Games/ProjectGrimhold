using System.Collections.Generic;
using Fusion.Editor;
using NUnit.Framework;
using Spawning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using NetworkObject = Fusion.NetworkObject;

namespace Tests.EditMode.Loot
{
    public sealed class NetworkSpawnManagerLootTests
    {
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const string LootContainerPath = "Assets/Prefabs/LootContainer.prefab";
        private const string EnemyPrefabPath = "Assets/Prefabs/NetworkEnemy.prefab";
        private const string BreakablePrefabPath = "Assets/Prefabs/BreakableObject.prefab";
        private const string PickupPrefabPath = "Assets/Prefabs/LootPickup.prefab";

        [Test]
        public void InitialPolicy_UsesExplicitIntegrationsAndRejectsEnemyFallbacks()
        {
            Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.Players),
                Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.Players));
            Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.Enemies),
                Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.Enemies));
            Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.Loot),
                Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.LootContainers));
            Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.Breakables),
                Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.Breakables));
            Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.NPCs),
                Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.Unsupported));
            Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.Bosses),
                Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.Unsupported));
            Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.Misc),
                Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.Unsupported));
        }

        [Test]
        public void BreakablePrefab_IsDamageableWorldContentAndNotInspectableLoot()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BreakablePrefabPath);
            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(pickupPrefab, Is.Not.Null);
            BreakableObject breakable = prefab.GetComponent<BreakableObject>();
            Assert.That(breakable, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Fusion.NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<IInteractable>(), Is.Null);
            Assert.That(prefab.GetComponent<ILootExtractor>(), Is.Null);
            Assert.That(prefab.GetComponent<ILootQuantityReader>(), Is.Null);
            Assert.That(prefab.GetComponent<NetworkLootContainer>(), Is.Null);
            Assert.That(breakable.LootTable, Is.Not.Null);
            Assert.That(breakable.LootCatalog, Is.Not.Null);
            Assert.That(breakable.DropCapacity,
                Is.GreaterThanOrEqualTo(breakable.LootTable.MaximumDistinctStacks));

            Collider2D[] colliders = prefab.GetComponentsInChildren<Collider2D>(true);
            Assert.That(colliders, Has.Length.EqualTo(2));
            Assert.That(colliders, Has.Some.Matches<Collider2D>(
                collider => collider.gameObject.layer == LayerMask.NameToLayer("Character")));
            Assert.That(colliders, Has.Some.Matches<Collider2D>(
                collider => collider.gameObject.layer == LayerMask.NameToLayer("WorldCollision") && !collider.isTrigger));

            NetworkLootPickup pickup = pickupPrefab.GetComponent<NetworkLootPickup>();
            Assert.That(pickup, Is.Not.Null);
            Assert.That(pickup.LootCatalog, Is.SameAs(breakable.LootCatalog));
            Assert.That(pickup.SortingLayerName, Is.EqualTo("Default"));
            Assert.That(pickup.SortingOrder, Is.EqualTo(2));
            Assert.That(pickupPrefab.GetComponentInChildren<SpriteRenderer>(true).sortingOrder,
                Is.EqualTo(pickup.SortingOrder));
            Assert.That(breakable.PickupPrefab.ToString(),
                Is.EqualTo(NetworkObjectEditor.GetPrefabGuid(pickupPrefab.GetComponent<NetworkObject>()).ToString()));
        }

        [Test]
        public void BreakableSpawnState_IsIdempotentAndPointBounded()
        {
            var points = new Transform[2];
            var definition = new SpawnGroupDefinition
            {
                Group = SpawnGroupType.Breakables,
                SpawnPoints = points,
                Amount = 3
            };
            Assert.That(
                InitialSpawnGroupPolicy.GetPointBoundedSpawnCount(definition, out bool wasClamped),
                Is.EqualTo(2));
            Assert.That(wasClamped, Is.True);

            var holder = new GameObject("Spawned breakable");
            NetworkObject spawned = holder.AddComponent<NetworkObject>();
            var state = new InitialBreakableSpawnState();
            try
            {
                Assert.That(state.TryRecordSuccessfulSpawn(0, spawned), Is.True);
                Assert.That(state.TryRecordSuccessfulSpawn(0, spawned), Is.False);
                Assert.That(state.Count, Is.EqualTo(1));
                state.Clear();
                Assert.That(state.ContainsPoint(0), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void LootCount_IsLimitedToUniqueConfiguredPoints()
        {
            var points = new Transform[2];
            var definition = new SpawnGroupDefinition
            {
                Group = SpawnGroupType.Loot,
                SpawnPoints = points,
                Amount = 5
            };

            Assert.That(InitialSpawnGroupPolicy.GetLootSpawnCount(definition, out bool wasClamped), Is.EqualTo(2));
            Assert.That(wasClamped, Is.True);

            definition.Amount = 2;
            Assert.That(InitialSpawnGroupPolicy.GetLootSpawnCount(definition, out wasClamped), Is.EqualTo(2));
            Assert.That(wasClamped, Is.False);
        }

        [Test]
        public void SuccessfulSpawnState_IsIdempotentAndResettableForANewSession()
        {
            var holder = new GameObject("Spawned loot");
            NetworkObject spawned = holder.AddComponent<NetworkObject>();
            var state = new InitialLootSpawnState();
            try
            {
                Assert.That(state.TryRecordSuccessfulSpawn(0, null), Is.False);
                Assert.That(state.ContainsPoint(0), Is.False);
                Assert.That(state.TryRecordSuccessfulSpawn(0, spawned), Is.True);
                Assert.That(state.TryRecordSuccessfulSpawn(0, spawned), Is.False);
                Assert.That(state.Count, Is.EqualTo(1));

                state.Clear();
                Assert.That(state.Count, Is.Zero);
                Assert.That(state.ContainsPoint(0), Is.False);
                Assert.That(state.TryRecordSuccessfulSpawn(0, spawned), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void GameplayScene_CopiesLootContainerReferenceAndConfiguresUniqueLootPoints()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(GameplayScenePath);
            var targetObject = new GameObject("Persistent manager test");
            NetworkSpawnManager target = targetObject.AddComponent<NetworkSpawnManager>();
            try
            {
                FindSceneConfiguration(
                    scene,
                    out NetworkSpawnManager configuredManager,
                    out NetworkSpawnSceneConfiguration configuration);

                Assert.That(configuredManager, Is.Not.Null);
                Assert.That(configuration, Is.Not.Null);
                Assert.That(configuredManager.LootContainerPrefab.IsValid, Is.True);
                Assert.That(target.CopyReferencesFrom(configuredManager), Is.True);
                Assert.That(target.LootContainerPrefab, Is.EqualTo(configuredManager.LootContainerPrefab));

                NetworkObject lootContainer = AssetDatabase.LoadAssetAtPath<GameObject>(LootContainerPath)
                    .GetComponent<NetworkObject>();
                NetworkObject enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath)
                    .GetComponent<NetworkObject>();
                LootContainerRandomContentConfig containerRandomConfig = lootContainer.GetComponent<LootContainerRandomContentConfig>();
                Assert.That(configuredManager.LootContainerPrefab.ToString(),
                    Is.EqualTo(NetworkObjectEditor.GetPrefabGuid(lootContainer).ToString()));
                Assert.That(configuredManager.LootContainerPrefab.ToString(),
                    Is.Not.EqualTo(NetworkObjectEditor.GetPrefabGuid(enemy).ToString()));
                Assert.That(containerRandomConfig, Is.Not.Null);
                Assert.That(containerRandomConfig.enabled, Is.True);
                Assert.That(containerRandomConfig.Table, Is.Not.Null);
                Assert.That(enemy.GetComponent<NetworkLootContainer>().StartsAvailable, Is.False);

                SpawnGroupDefinition lootGroup = FindGroup(configuration, SpawnGroupType.Loot);
                Assert.That(lootGroup, Is.Not.Null);
                Assert.That(lootGroup.Amount, Is.GreaterThan(0));
                Assert.That(lootGroup.Amount, Is.LessThanOrEqualTo(lootGroup.SpawnPoints.Length));
                Assert.That(new HashSet<Transform>(lootGroup.SpawnPoints).Count, Is.EqualTo(lootGroup.SpawnPoints.Length));

                int placedContainers = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    placedContainers += root.GetComponentsInChildren<NetworkLootContainer>(true).Length;
                }
                Assert.That(placedContainers, Is.Zero);

                SpawnGroupDefinition breakableGroup = FindGroup(configuration, SpawnGroupType.Breakables);
                Assert.That(breakableGroup, Is.Not.Null);
                Assert.That(breakableGroup.Amount, Is.GreaterThan(0));
                Assert.That(breakableGroup.Amount, Is.LessThanOrEqualTo(breakableGroup.SpawnPoints.Length));
                Assert.That(new HashSet<Transform>(breakableGroup.SpawnPoints).Count,
                    Is.EqualTo(breakableGroup.SpawnPoints.Length));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void MissingLootPrefab_IsRejectedWithoutChangingGroupDispatchPolicy()
        {
            var sourceObject = new GameObject("Invalid scene manager");
            var targetObject = new GameObject("Persistent manager");
            NetworkSpawnManager source = sourceObject.AddComponent<NetworkSpawnManager>();
            NetworkSpawnManager target = targetObject.AddComponent<NetworkSpawnManager>();
            try
            {
                LogAssert.Expect(
                    UnityEngine.LogType.Error,
                    "[NetworkSpawnManager] Scene manager 'Invalid scene manager' has no valid loot-container prefab. Loot groups will be skipped.");
                Assert.That(target.CopyReferencesFrom(source), Is.False);
                Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.Enemies),
                    Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.Enemies));
                Assert.That(InitialSpawnGroupPolicy.Resolve(SpawnGroupType.Loot),
                    Is.EqualTo(InitialSpawnGroupPolicy.SpawnKind.LootContainers));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(sourceObject);
            }
        }

        private static void FindSceneConfiguration(
            Scene scene,
            out NetworkSpawnManager manager,
            out NetworkSpawnSceneConfiguration configuration)
        {
            manager = null;
            configuration = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                manager ??= root.GetComponentInChildren<NetworkSpawnManager>(true);
                configuration ??= root.GetComponentInChildren<NetworkSpawnSceneConfiguration>(true);
            }
        }

        private static SpawnGroupDefinition FindGroup(
            NetworkSpawnSceneConfiguration configuration,
            SpawnGroupType groupType)
        {
            foreach (SpawnGroupDefinition group in configuration.SpawnGroups)
            {
                if (group != null && group.Group == groupType)
                {
                    return group;
                }
            }

            return null;
        }

    }
}
