#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Abilities
{
    /// <summary>Proof of the caster-owned networked trap entity on a real runner, real creatures and a real avatar.</summary>
    public sealed class NetworkTrapPlayModeTests
    {
        private const string TrapPrefabGuid = "f350a40a118648c4e8e6edf70efaa3b5";
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private const float TrapLifetimeSeconds = 30f;
        private const float TriggerRadius = 1.5f;
        private static readonly Vector3 TrapPosition = new Vector3(20f, 0f, 0f);
        private static readonly EntityId Caster = new EntityId(900001);
        private static readonly EntityId OtherCaster = new EntityId(900002);

        private NetworkRunner _runner;

        // Creature and player damage colliders both live on the Character layer (the same mask the avatar's
        // area finder and SpikesTrap use); the valid-enemy predicate, not the layer, tells them apart.
        private static int CharacterMask => LayerMask.GetMask("Character");

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
                Object.Destroy(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Enemy_InsideRadius_TriggersOnceWithItsIdAndDespawnsTheTrap()
        {
            yield return Begin();
            var trap = SpawnTrap(TrapPosition, Caster, TrapLifetimeSeconds, out var triggered);
            var trapId = trap.Object.Id;
            var enemy = SpawnEnemy(TrapPosition + new Vector3(0.5f, 0f, 0f));

            yield return WaitUntil(() => !_runner.TryFindObject(trapId, out _));
            yield return WaitTicks();

            Assert.That(triggered, Is.EqualTo(new[] { enemy.Id.Value }), "Raised once, with the triggering enemy.");
        }

        [UnityTest]
        public IEnumerator Enemy_WithSeveralColliders_TriggersOnce()
        {
            yield return Begin();
            var trap = SpawnTrap(TrapPosition, Caster, TrapLifetimeSeconds, out var triggered);
            var enemy = SpawnEnemy(TrapPosition);
            int colliders = 0;
            foreach (var collider in enemy.GetComponentsInChildren<Collider2D>())
                if (collider.gameObject.layer == LayerMask.NameToLayer("Character")) colliders++;
            Physics2D.SyncTransforms();

            yield return WaitUntil(() => triggered.Count > 0);
            yield return WaitTicks();
            yield return WaitTicks();

            Assert.That(colliders, Is.GreaterThan(1), "The creature must present several overlapping colliders.");
            Assert.That(triggered, Is.EqualTo(new[] { enemy.Id.Value }));
            Assert.That(trap == null || !_runner.TryFindObject(trap.Object.Id, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerAndCaster_NeverTrigger()
        {
            yield return Begin();
            ExpectAvatarValidationError();
            var player = _runner.Spawn(LoadPrefab(PlayerPrefabGuid), TrapPosition, Quaternion.identity, inputAuthority: null);
            Physics2D.SyncTransforms();
            var playerId = player.GetComponent<PlayerCharacter>().Id;
            var byForeignCaster = SpawnTrap(TrapPosition, OtherCaster, TrapLifetimeSeconds, out var foreignTriggered);
            var bySelfCaster = SpawnTrap(TrapPosition, playerId, TrapLifetimeSeconds, out var selfTriggered);

            yield return WaitTicks();
            yield return WaitTicks();
            yield return WaitTicks();

            Assert.That(foreignTriggered, Is.Empty, "A player is never a valid enemy.");
            Assert.That(selfTriggered, Is.Empty, "The caster never triggers its own trap.");
            Assert.That(byForeignCaster.IsTriggered, Is.False);
            Assert.That(bySelfCaster.IsTriggered, Is.False);
            Assert.That(_runner.TryFindObject(byForeignCaster.Object.Id, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator Enemy_OutsideRadius_DoesNotTrigger()
        {
            yield return Begin();
            var trap = SpawnTrap(TrapPosition, Caster, TrapLifetimeSeconds, out var triggered);
            SpawnEnemy(TrapPosition + new Vector3(TriggerRadius + 4f, 0f, 0f));

            yield return WaitTicks();
            yield return WaitTicks();
            yield return WaitTicks();

            Assert.That(triggered, Is.Empty);
            Assert.That(trap.IsTriggered, Is.False);
            Assert.That(_runner.TryFindObject(trap.Object.Id, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator LifetimeExpiry_DespawnsWithoutTriggering()
        {
            yield return Begin();
            var trap = SpawnTrap(TrapPosition, Caster, 0.3f, out var triggered);
            var trapId = trap.Object.Id;

            yield return WaitUntil(() => !_runner.TryFindObject(trapId, out _));

            Assert.That(triggered, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Limit_EvictsTheCasterTrapWithLeastRemainingLifetimeOnly()
        {
            yield return Begin();
            var longLived = SpawnTrap(TrapPosition, Caster, 20f, out _);
            var shortest = SpawnTrap(TrapPosition + Vector3.up * 10f, Caster, 5f, out _);
            var middle = SpawnTrap(TrapPosition + Vector3.up * 20f, Caster, 10f, out _);
            var foreign = SpawnTrap(TrapPosition + Vector3.up * 30f, OtherCaster, 1f + TrapLifetimeSeconds, out _);
            var scratch = new List<NetworkTrap>();
            var longLivedId = longLived.Object.Id;
            var shortestId = shortest.Object.Id;
            var middleId = middle.Object.Id;
            var foreignId = foreign.Object.Id;

            bool belowLimit = NetworkTrap.TryEvictForNewTrap(_runner, Caster, 4, scratch);
            Assert.That(belowLimit, Is.False, "Room remains below the limit.");
            Assert.That(_runner.TryFindObject(shortestId, out _), Is.True);

            bool evicted = NetworkTrap.TryEvictForNewTrap(_runner, Caster, 3, scratch);
            yield return null;

            Assert.That(evicted, Is.True);
            Assert.That(_runner.TryFindObject(shortestId, out _), Is.False, "Least remaining lifetime goes first.");
            Assert.That(_runner.TryFindObject(longLivedId, out _), Is.True);
            Assert.That(_runner.TryFindObject(middleId, out _), Is.True);
            Assert.That(_runner.TryFindObject(foreignId, out _), Is.True, "Another caster is untouched.");
        }

        // ---- Fixture ---------------------------------------------------------------------

        private IEnumerator Begin()
        {
            var runnerObject = new GameObject("NetworkTrapTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"network-trap-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private NetworkTrap SpawnTrap(Vector3 position, EntityId caster, float lifetimeSeconds,
            out List<int> triggeredIds)
        {
            var ids = new List<int>();
            triggeredIds = ids;
            int mask = CharacterMask;
            var spawned = _runner.Spawn(LoadPrefab(TrapPrefabGuid), position, Quaternion.identity,
                onBeforeSpawned: (_, instance) =>
                {
                    var trap = instance.GetComponent<NetworkTrap>();
                    trap.Triggered += id => ids.Add(id.Value);
                    trap.InitializeNetworkState(caster, lifetimeSeconds, TriggerRadius, mask);
                });
            Physics2D.SyncTransforms();
            return spawned.GetComponent<NetworkTrap>();
        }

        private EnemyCharacter SpawnEnemy(Vector3 position)
        {
            var enemy = _runner.Spawn(LoadPrefab(GreenSlimePrefabGuid), position, Quaternion.identity);
            // Keep the creature still: only the test places it.
            foreach (var behaviour in enemy.GetComponents<NetworkBehaviour>())
            {
                if (behaviour is EnemyFSM || behaviour is EnemyMovementAIController || behaviour is EnemyCombatAIController)
                    behaviour.enabled = false;
            }
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private static void ExpectAvatarValidationError() =>
            LogAssert.Expect(UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, " +
                "registry, assignment service, and valid receiver/reader registrations.");

        private NetworkObject LoadPrefab(string guid)
        {
            var id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            return _runner.Config.PrefabTable.Load(id, true);
        }

        private IEnumerator WaitTicks()
        {
            int until = _runner.Tick.Raw + 3;
            yield return WaitUntil(() => _runner.Tick.Raw >= until);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds = 8f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "The expected state was not reached.");
        }
    }
}
#endif
