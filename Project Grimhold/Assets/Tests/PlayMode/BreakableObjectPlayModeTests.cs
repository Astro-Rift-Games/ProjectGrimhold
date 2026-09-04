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
    public sealed class BreakableObjectPlayModeTests
    {
        private const string BreakablePrefabGuid = "8b5a2dd483fd42b48fd1863cf21fc690";

        private NetworkRunner _runner;
        private NetworkObject _prefab;
        private BreakableDamageSimulationDriver _damageDriver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner != null && _runner.IsRunning)
                {
                    yield return null;
                }
            }

            if (_runner != null)
            {
                UnityEngine.Object.DestroyImmediate(_runner.gameObject);
            }

            _runner = null;
            _prefab = null;
            _damageDriver = null;
        }

        [UnityTest]
        public IEnumerator PartialFatalAndRepeatedDamage_GeneratesOnePickupBatch()
        {
            yield return StartRunner();

            BreakableObject partialTarget = SpawnBreakable(new LootEntry(new LootId("bone"), 2), Vector3.zero);
            float initialHealth = partialTarget.Health;
            _damageDriver.Target = partialTarget;
            _damageDriver.DamageAmount = 5f;
            _damageDriver.RequestedHits = 1;
            yield return WaitForRequests(1);

            Assert.That(_damageDriver.LastResult.IsApplied, Is.True);
            Assert.That(_damageDriver.LastResult.IsFatal, Is.False);
            Assert.That(partialTarget.Health, Is.EqualTo(initialHealth - 5f));
            Assert.That((bool)partialTarget.IsDestroyed, Is.False);
            Assert.That(FindSpawnedPickups(), Has.Length.Zero);

            _damageDriver.DamageAmount = 1000f;
            _damageDriver.RequestedHits = 2;
            yield return WaitForRequests(3);
            yield return null;

            Assert.That((bool)partialTarget.IsDestroyed, Is.True);
            Assert.That(partialTarget.Health, Is.Zero);
            Assert.That(_damageDriver.FatalResult.IsApplied, Is.True);
            Assert.That(_damageDriver.FatalResult.IsFatal, Is.True);
            Assert.That(_damageDriver.FirstResult.IsFatal, Is.False);
            Assert.That(_damageDriver.LastResult.IsApplied, Is.False);
            Assert.That(_damageDriver.LastResult.FailureReason, Is.EqualTo(DamageFailureReason.TargetDead));
            Assert.That(partialTarget.GetComponentsInChildren<Collider2D>(true),
                Has.All.Matches<Collider2D>(collider => !collider.enabled));
            Assert.That(partialTarget.GetComponentsInChildren<SpriteRenderer>(true),
                Has.All.Matches<SpriteRenderer>(renderer => !renderer.enabled));

            NetworkLootPickup[] pickups = FindSpawnedPickups();
            Assert.That(pickups, Has.Length.EqualTo(1));
            Assert.That((bool)pickups[0].IsInitialized, Is.True);
            Assert.That(pickups[0].LootDefinition.LootId, Is.EqualTo(new LootId("bone")));
            Assert.That(pickups[0].Amount, Is.EqualTo(2));
            Assert.That(pickups[0].IsAvailable, Is.True);

            var receiver = new StubLootReceiver(new EntityId(999));
            EntityRegistry registry = _runner.GetComponent<EntityRegistry>();
            Assert.That(registry.TryRegisterLootReceiver(receiver.Id, receiver), Is.True);
            _damageDriver.PickupTarget = pickups[0];
            _damageDriver.PickupInteractorId = receiver.Id;
            _damageDriver.IsPickupRequested = true;
            int framesRemaining = 120;
            while (_damageDriver.IsPickupRequested && framesRemaining-- > 0)
            {
                yield return null;
            }
            yield return null;

            Assert.That(_damageDriver.LastInteractionResult.Success, Is.True);
            Assert.That(_damageDriver.LastInteractionResult.IsConsumed, Is.True);
            Assert.That(receiver.CommittedAmount, Is.EqualTo(2));
            Assert.That(FindSpawnedPickups(), Has.Length.Zero);
        }

        [UnityTest]
        public IEnumerator RandomDrops_ResolveOnFatalDamageAndOnlyOnce()
        {
            yield return StartRunner();

            const ulong seed = 144;
            BreakableObject target = SpawnRandomBreakable(seed, 10_000, Vector3.zero);
            Assert.That(target.GenerationState, Is.EqualTo(LootSourceGenerationState.Pending));
            Assert.That(target.HasInitialDrops, Is.False);

            Assert.That(LootContainerContentTableValidation.TryCreateSnapshot(
                target.LootTable,
                target.LootCatalog,
                target.DropCapacity,
                NetworkLootContainer.MaxDistinctLootTypes,
                out ValidatedLootContainerContentSnapshot snapshot,
                out string error), Is.True, error);
            Assert.That(LootContainerContentRoller.TryRoll(
                snapshot,
                seed,
                10_000,
                out IReadOnlyList<LootEntry> expected,
                out error), Is.True, error);

            _damageDriver.Target = target;
            _damageDriver.DamageAmount = 5f;
            _damageDriver.RequestedHits = 1;
            yield return WaitForRequests(1);
            Assert.That(target.GenerationState, Is.EqualTo(LootSourceGenerationState.Pending));
            Assert.That(FindSpawnedPickups(), Is.Empty);

            _damageDriver.DamageAmount = 1000f;
            _damageDriver.RequestedHits = 2;
            yield return WaitForRequests(3);
            yield return null;

            Assert.That((bool)target.IsDestroyed, Is.True);
            Assert.That(target.GenerationState, Is.EqualTo(LootSourceGenerationState.Resolved));
            Assert.That(_damageDriver.FatalResult.IsFatal, Is.True);
            Assert.That(_damageDriver.LastResult.FailureReason,
                Is.EqualTo(DamageFailureReason.TargetDead));

            NetworkLootPickup[] pickups = FindSpawnedPickups();
            Assert.That(pickups, Has.Length.EqualTo(expected.Count));
            for (int index = 0; index < expected.Count; index++)
            {
                LootEntry expectedEntry = expected[index];
                Assert.That(Array.Exists(pickups, pickup =>
                    pickup.LootDefinition.LootId == expectedEntry.LootId &&
                    pickup.Amount == expectedEntry.Amount), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator RandomDrops_FailureStillDestroysAndNeverRetries()
        {
            yield return StartRunner();

            BreakableObject target = SpawnRandomBreakable(144, 0, Vector3.zero);
            var serialized = new SerializedObject(target);
            serialized.FindProperty("_lootTable").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(UnityEngine.LogType.Error,
                "BreakableObject consumed Loot generation for 'BreakableObject(Clone)' as Failed. Loot content table is missing.");
            _damageDriver.Target = target;
            _damageDriver.DamageAmount = 1000f;
            _damageDriver.RequestedHits = 2;
            yield return WaitForRequests(2);
            yield return null;

            Assert.That(_damageDriver.FirstResult.IsApplied, Is.True);
            Assert.That(_damageDriver.FirstResult.IsFatal, Is.True);
            Assert.That(_damageDriver.LastResult.IsApplied, Is.False);
            Assert.That(_damageDriver.LastResult.FailureReason,
                Is.EqualTo(DamageFailureReason.TargetDead));
            Assert.That((bool)target.IsDestroyed, Is.True);
            Assert.That(target.Health, Is.Zero);
            Assert.That(target.GenerationState, Is.EqualTo(LootSourceGenerationState.Failed));
            Assert.That(FindSpawnedPickups(), Is.Empty);
            Assert.That(target.GetComponentsInChildren<Collider2D>(true),
                Has.All.Matches<Collider2D>(collider => !collider.enabled));
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("BreakableTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _damageDriver = runnerObject.AddComponent<BreakableDamageSimulationDriver>();
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

            NetworkObjectGuid prefabGuid = NetworkObjectGuid.Parse(BreakablePrefabGuid);
            NetworkPrefabId prefabId = _runner.Config.PrefabTable.GetId(prefabGuid);
            Assert.That(prefabId.IsValid, Is.True);
            _prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(_prefab, Is.Not.Null);
        }

        private BreakableObject SpawnBreakable(LootEntry drop, Vector3 position)
        {
            bool callbackApplied = false;
            NetworkObject spawned = _runner.Spawn(
                _prefab,
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (callbackRunner, instance) =>
                {
                    BreakableObject breakable = instance.GetComponent<BreakableObject>();
                    callbackApplied = breakable.TrySetInitialDropsOverride(
                        callbackRunner,
                        instance,
                        new[] { drop });
                });

            Assert.That(callbackApplied, Is.True);
            Assert.That(spawned, Is.Not.Null);
            BreakableObject result = spawned.GetComponent<BreakableObject>();
            Assert.That(result.HasInitialDrops, Is.True);
            return result;
        }

        private BreakableObject SpawnRandomBreakable(
            ulong seed,
            int additionalLootChanceBasisPoints,
            Vector3 position)
        {
            bool callbackApplied = false;
            NetworkObject spawned = _runner.Spawn(
                _prefab,
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (callbackRunner, instance) =>
                {
                    BreakableObject breakable = instance.GetComponent<BreakableObject>();
                    callbackApplied = breakable.TrySetRandomGenerationOverride(
                        callbackRunner,
                        instance,
                        seed,
                        additionalLootChanceBasisPoints);
                });

            Assert.That(callbackApplied, Is.True);
            Assert.That(spawned, Is.Not.Null);
            BreakableObject result = spawned.GetComponent<BreakableObject>();
            Assert.That(result.GenerationState, Is.EqualTo(LootSourceGenerationState.Pending));
            return result;
        }

        private IEnumerator WaitForRequests(int expectedCount)
        {
            int framesRemaining = 120;
            while (_damageDriver.AppliedRequests < expectedCount && framesRemaining-- > 0)
            {
                yield return null;
            }

            Assert.That(_damageDriver.AppliedRequests, Is.EqualTo(expectedCount));
        }

        private static NetworkLootPickup[] FindSpawnedPickups()
        {
            return UnityEngine.Object.FindObjectsByType<NetworkLootPickup>(
                FindObjectsInactive.Exclude);
        }

        private sealed class StubLootReceiver : ILootReceiver
        {
            public EntityId Id { get; }
            public int CommittedAmount { get; private set; }

            public StubLootReceiver(EntityId id)
            {
                Id = id;
            }

            public LootTransferFailureReason ValidateReceive(in LootTransferRequest request)
            {
                return request.DestinationId == Id
                    ? LootTransferFailureReason.None
                    : LootTransferFailureReason.DestinationNotFound;
            }

            public void CommitReceive(in LootTransferRequest request)
            {
                CommittedAmount += request.RequestedAmount;
            }
        }
    }
}
#endif
