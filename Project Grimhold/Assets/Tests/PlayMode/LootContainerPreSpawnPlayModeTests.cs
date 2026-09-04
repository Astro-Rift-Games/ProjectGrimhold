#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Loot
{
    public sealed class LootContainerPreSpawnPlayModeTests
    {
        private const string LootContainerPrefabGuid = "2c19a78647c64b84a765ff0280706b7d";
        private const string EnemyMeleePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private NetworkRunner _runner;
        private NetworkObject _prefab;
        private EnemyFatalDamageSimulationDriver _damageDriver;
        private BreakableDamageSimulationDriver _interactionDriver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ShutdownRunner(_runner);
            _runner = null;
            _prefab = null;
            _damageDriver = null;
            _interactionDriver = null;
        }

        [UnityTest]
        public IEnumerator AuthoritativeCallback_AppliesEmptyOverrideBeforeSpawned()
        {
            yield return StartRunnerAndLoadPrefab(LootContainerPrefabGuid);

            bool callbackApplied = false;
            NetworkObject spawned = _runner.Spawn(
                _prefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (callbackRunner, instance) =>
                {
                    NetworkLootContainer container = instance.GetComponent<NetworkLootContainer>();
                    callbackApplied = container != null &&
                        container.TrySetInitialContentOverride(
                            callbackRunner,
                            instance,
                            Array.Empty<LootEntry>());
                });

            Assert.That(callbackApplied, Is.True);
            Assert.That(spawned, Is.Not.Null);
            NetworkLootContainer spawnedContainer = spawned.GetComponent<NetworkLootContainer>();
            Assert.That((bool)spawnedContainer.IsInitialized, Is.True);
            Assert.That((bool)spawnedContainer.IsAvailable, Is.True);
            Assert.That(spawnedContainer.IsEmpty, Is.True);
            Assert.That(spawnedContainer.LootChangeSequence, Is.Zero);

            NetworkId spawnedId = spawned.Id;
            _runner.Despawn(spawned);
            Assert.That(_runner.TryFindObject(spawnedId, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator RejectedCallback_FailsClosedAndCanBeCompensated()
        {
            yield return StartRunnerAndLoadPrefab(LootContainerPrefabGuid);

            bool callbackApplied = true;
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "NetworkLootContainer: The requested initial-content override was rejected for 'LootContainer(Clone)'. Manual content will not be used as a fallback.");
            NetworkObject spawned = _runner.Spawn(
                _prefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (callbackRunner, instance) =>
                {
                    NetworkLootContainer container = instance.GetComponent<NetworkLootContainer>();
                    callbackApplied = container != null &&
                        container.TrySetInitialContentOverride(
                            callbackRunner,
                            instance,
                            new[] { default(LootEntry) });
                });

            Assert.That(callbackApplied, Is.False);
            Assert.That(spawned, Is.Not.Null);
            NetworkLootContainer spawnedContainer = spawned.GetComponent<NetworkLootContainer>();
            Assert.That((bool)spawnedContainer.IsInitialized, Is.False);
            Assert.That((bool)spawnedContainer.IsAvailable, Is.False);

            NetworkId spawnedId = spawned.Id;
            _runner.Despawn(spawned);
            Assert.That(_runner.TryFindObject(spawnedId, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator RandomContent_ResolvesOnlyOnFirstInteraction()
        {
            yield return StartRunnerAndLoadPrefab(LootContainerPrefabGuid);

            const ulong seed = 144;
            NetworkLootContainer container = SpawnRandomContainer(seed, 10_000);
            NetworkLootContainerInteractable interactable =
                container.GetComponent<NetworkLootContainerInteractable>();
            Assert.That(container.GenerationState, Is.EqualTo(LootSourceGenerationState.Pending));
            Assert.That(container.IsEmpty, Is.True);
            Assert.That(container.LootChangeSequence, Is.Zero);

            LootContainerRandomContentConfig config =
                container.GetComponent<LootContainerRandomContentConfig>();
            Assert.That(LootContainerContentTableValidation.TryCreateSnapshot(
                config.Table,
                container.LootCatalog,
                container.SlotCapacity,
                NetworkLootContainer.MaxDistinctLootTypes,
                out ValidatedLootContainerContentSnapshot snapshot,
                out string error), Is.True, error);
            Assert.That(LootContainerContentRoller.TryRoll(
                snapshot,
                seed,
                10_000,
                out IReadOnlyList<LootEntry> expected,
                out error), Is.True, error);

            yield return Interact(interactable);

            Assert.That(_interactionDriver.LastContainerInteractionResult.Success, Is.True);
            Assert.That(container.GenerationState, Is.EqualTo(LootSourceGenerationState.Resolved));
            Assert.That(container.TryGetLootContent(out IReadOnlyList<LootEntry> actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(container.LootChangeSequence, Is.EqualTo(1));

            yield return Interact(interactable);
            Assert.That(container.GenerationState, Is.EqualTo(LootSourceGenerationState.Resolved));
            Assert.That(container.TryGetLootContent(out actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(container.LootChangeSequence, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RandomContent_FailedIsTerminalAndStillOpensEmpty()
        {
            yield return StartRunnerAndLoadPrefab(LootContainerPrefabGuid);

            NetworkLootContainer container = SpawnRandomContainer(144, 0);
            NetworkLootContainerInteractable interactable =
                container.GetComponent<NetworkLootContainerInteractable>();
            var serializedConfig = new SerializedObject(
                container.GetComponent<LootContainerRandomContentConfig>());
            serializedConfig.FindProperty("_table").objectReferenceValue = null;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(UnityEngine.LogType.Error,
                "NetworkLootContainerInteractable consumed Loot generation for 'LootContainer(Clone)' as Failed. Loot content table is missing.");
            yield return Interact(interactable);

            Assert.That(_interactionDriver.LastContainerInteractionResult.Success, Is.True);
            Assert.That(container.GenerationState, Is.EqualTo(LootSourceGenerationState.Failed));
            Assert.That(container.IsEmpty, Is.True);
            Assert.That(container.LootChangeSequence, Is.Zero);

            yield return Interact(interactable);
            Assert.That(_interactionDriver.LastContainerInteractionResult.Success, Is.True);
            Assert.That(container.GenerationState, Is.EqualTo(LootSourceGenerationState.Failed));
            Assert.That(container.IsEmpty, Is.True);
            Assert.That(container.LootChangeSequence, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FatalEnemy_PersistsAndExposesItsOwnContainer()
        {
            yield return StartRunnerAndLoadPrefab(EnemyMeleePrefabGuid);

            NetworkObject spawned = _runner.Spawn(
                _prefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null);

            Assert.That(spawned, Is.Not.Null);
            EnemyCharacter enemy = spawned.GetComponent<EnemyCharacter>();
            EnemyMovementAIController movement = spawned.GetComponent<EnemyMovementAIController>();
            EnemyCombatAIController combat = spawned.GetComponent<EnemyCombatAIController>();
            NetworkLootContainer container = spawned.GetComponent<NetworkLootContainer>();
            NetworkLootContainerInteractable interactable = spawned.GetComponent<NetworkLootContainerInteractable>();
            Assert.That(enemy, Is.Not.Null);
            Assert.That(container, Is.Not.Null);
            Assert.That(interactable, Is.Not.Null);
            Assert.That((bool)container.IsInitialized, Is.True);
            Assert.That((bool)container.IsAvailable, Is.False);
            Assert.That(interactable.CanInteract(new InteractionRequest(
                new EntityId(int.MaxValue), enemy.Id, _runner.Tick)), Is.False);

            _damageDriver.Target = enemy;
            _damageDriver.IsRequested = true;
            int framesRemaining = 120;
            while (enemy.IsAlive && framesRemaining-- > 0)
            {
                yield return null;
            }

            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(_damageDriver.LastResult.IsFatal, Is.True);
            Assert.That((bool)movement.IsControlEnabled, Is.False);
            Assert.That((bool)combat.IsAttackEnabled, Is.False);
            Assert.That((bool)container.IsAvailable, Is.True);
            Assert.That(interactable.CanInteract(new InteractionRequest(
                new EntityId(int.MaxValue), enemy.Id, _runner.Tick)), Is.True);
            Assert.That(spawned.gameObject.activeInHierarchy, Is.True);

            yield return new WaitForSeconds(2.1f);
            Transform body = spawned.transform.Find("Body");
            Assert.That(body, Is.Not.Null);
            Assert.That(body.gameObject.activeSelf, Is.True);
        }

        private IEnumerator StartRunnerAndLoadPrefab(string prefabGuidValue)
        {
            var runnerObject = new GameObject("LootContainerPreSpawnTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _damageDriver = runnerObject.AddComponent<EnemyFatalDamageSimulationDriver>();
            _interactionDriver = runnerObject.AddComponent<BreakableDamageSimulationDriver>();
            var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
            var objectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>();

            var startTask = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task35-{Guid.NewGuid():N}",
                SceneManager = sceneManager,
                ObjectProvider = objectProvider
            });
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            Assert.That(startTask.IsFaulted, Is.False, startTask.Exception?.ToString());
            Assert.That(startTask.Result.Ok, Is.True, startTask.Result.ShutdownReason.ToString());

            NetworkObjectGuid prefabGuid = NetworkObjectGuid.Parse(prefabGuidValue);
            NetworkPrefabId prefabId = _runner.Config.PrefabTable.GetId(prefabGuid);
            Assert.That(prefabId.IsValid, Is.True);
            _prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(_prefab, Is.Not.Null);
        }

        private NetworkLootContainer SpawnRandomContainer(
            ulong seed,
            int additionalLootChanceBasisPoints)
        {
            bool callbackApplied = false;
            NetworkObject spawned = _runner.Spawn(
                _prefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (callbackRunner, instance) =>
                {
                    NetworkLootContainer container =
                        instance.GetComponent<NetworkLootContainer>();
                    callbackApplied = container != null &&
                        container.TrySetRandomGenerationOverride(
                            callbackRunner,
                            instance,
                            seed,
                            additionalLootChanceBasisPoints);
                });

            Assert.That(callbackApplied, Is.True);
            Assert.That(spawned, Is.Not.Null);
            return spawned.GetComponent<NetworkLootContainer>();
        }

        private IEnumerator Interact(NetworkLootContainerInteractable interactable)
        {
            _interactionDriver.ContainerTarget = interactable;
            _interactionDriver.ContainerInteractorId = new EntityId(int.MaxValue);
            _interactionDriver.IsContainerInteractionRequested = true;
            int framesRemaining = 120;
            while (_interactionDriver.IsContainerInteractionRequested && framesRemaining-- > 0)
            {
                yield return null;
            }

            Assert.That(_interactionDriver.IsContainerInteractionRequested, Is.False);
            yield return null;
        }

        private static IEnumerator ShutdownRunner(NetworkRunner runner)
        {
            if (runner != null && runner.IsRunning)
            {
                runner.Shutdown();
                while (runner != null && runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (runner != null)
            {
                UnityEngine.Object.DestroyImmediate(runner.gameObject);
            }
        }
    }
}
#endif
