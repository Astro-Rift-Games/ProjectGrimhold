#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Consumables
{
    public sealed class ThrowableConsumablesPlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string EnemyGuid = "5deca87613df0fa409d98702aec643d4";

        private const string DaggerGuid = "8a414d957a3ff4a4986d927015eb8733";
        private const string BombGuid = "56d18b8e310b7684ba426e653d32a627";
        private const string SmokeGuid = "6b30ec697296f084ba882dba18790102";

        private static readonly LootId BombId = PlayerConsumableNetworkController.ExplosiveBombId;
        private static readonly LootId DaggerId = PlayerConsumableNetworkController.ThrowingDaggerId;
        private static readonly LootId SmokeId = PlayerConsumableNetworkController.SmokeBombId;

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private CharacterAttributeState _attributes;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SmokeZoneRegistry.Clear();
            if (_runner == null) yield break;
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
            Object.Destroy(_runner.gameObject);
        }

        [UnityTest]
        public IEnumerator ImmediateConsumption_BombAndDagger_DeductOneUnitImmediatelyAtStart()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();

            consume.ConfigureThrowables(
                new NetworkPrefabRef(DaggerGuid),
                new NetworkPrefabRef(BombGuid),
                new NetworkPrefabRef(SmokeGuid));

            yield return InSimulation(() =>
            {
                AddLoot(loot, BombId, 2);
                AddLoot(loot, DaggerId, 2);
            });

            Assert.That(loot.GetLootAmount(BombId), Is.EqualTo(2));
            Assert.That(loot.GetLootAmount(DaggerId), Is.EqualTo(2));

            // Throw bomb: must deduct 1 unit immediately upon start confirmed by State Authority
            Assert.That(consume.TryRequestConsume(BombId, new Vector2(3f, 0f)), Is.True);
            yield return WaitTicks(3);

            Assert.That(loot.GetLootAmount(BombId), Is.EqualTo(1),
                "Executing a bomb throw must deduct 1 unit immediately from inventory.");

            // Throw dagger: must deduct 1 unit immediately upon start
            Assert.That(consume.TryRequestConsume(DaggerId, new Vector2(3f, 0f)), Is.True);
            yield return WaitTicks(3);

            Assert.That(loot.GetLootAmount(DaggerId), Is.EqualTo(1),
                "Executing a dagger throw must deduct 1 unit immediately from inventory.");
        }

        [UnityTest]
        public IEnumerator Throwing_WhenZeroAmount_RejectsConsumption()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();

            Assert.That(loot.GetLootAmount(DaggerId), Is.EqualTo(0));
            Assert.That(consume.TryRequestConsume(DaggerId, new Vector2(3f, 0f)), Is.False);
        }

        [UnityTest]
        public IEnumerator ThrowingDagger_TravelsLinearly_AndDamagesTarget()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();

            consume.ConfigureThrowables(
                new NetworkPrefabRef(DaggerGuid),
                new NetworkPrefabRef(BombGuid),
                new NetworkPrefabRef(SmokeGuid));

            EnemyCharacter enemy = SpawnEnemy(new Vector3(3f, 0f, 0f));
            float enemyStartHealth = enemy.Health;

            yield return InSimulation(() => AddLoot(loot, DaggerId, 1));
            Assert.That(loot.GetLootAmount(DaggerId), Is.EqualTo(1));

            Assert.That(consume.TryRequestConsume(DaggerId, new Vector2(3f, 0f)), Is.True);
            yield return WaitTicks(2);
            Assert.That(loot.GetLootAmount(DaggerId), Is.EqualTo(0), "Dagger unit deducted immediately.");

            // Wait for dagger projectile to travel and impact enemy
            yield return WaitFor(() => enemy.Health < enemyStartHealth);

            Assert.That(enemyStartHealth - enemy.Health, Is.EqualTo(20f).Within(0.01f),
                "Throwing dagger must deal exactly 20 base damage to the first target hit.");
        }

        [UnityTest]
        public IEnumerator ExplosiveBomb_DetonatesAfter1_5s_Inflicts50DamageInRadius()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();

            consume.ConfigureThrowables(
                new NetworkPrefabRef(DaggerGuid),
                new NetworkPrefabRef(BombGuid),
                new NetworkPrefabRef(SmokeGuid));

            // Enemy A at (2, 0) is within 2.5m of bomb target (2, 0)
            EnemyCharacter enemyA = SpawnEnemy(new Vector3(2f, 0f, 0f));
            // Enemy B at (6, 0) is outside 2.5m radius of target (2, 0)
            EnemyCharacter enemyB = SpawnEnemy(new Vector3(6f, 0f, 0f));

            float healthA = enemyA.Health;
            float healthB = enemyB.Health;

            yield return InSimulation(() => AddLoot(loot, BombId, 1));
            Assert.That(loot.GetLootAmount(BombId), Is.EqualTo(1));

            Vector2 targetPos = new Vector2(2f, 0f);
            Assert.That(consume.TryRequestConsume(BombId, targetPos), Is.True);
            yield return WaitTicks(2);
            Assert.That(loot.GetLootAmount(BombId), Is.EqualTo(0), "Bomb unit deducted immediately at throw start.");

            // At 0.5s: bomb is still in fuse, enemy should not have taken damage yet
            yield return WaitSimulationSeconds(0.5f);
            Assert.That(enemyA.Health, Is.EqualTo(healthA), "Bomb must not deal damage before 1.5s fuse expires.");
            Assert.That(enemyB.Health, Is.EqualTo(healthB));

            // Wait until 1.6s total: detonation occurs
            yield return WaitSimulationSeconds(1.2f);

            Assert.That(healthA - enemyA.Health, Is.EqualTo(50f).Within(0.01f),
                "Explosive bomb must deal 50 damage in 2.5m radius upon detonation.");
            Assert.That(enemyB.Health, Is.EqualTo(healthB),
                "Targets outside 2.5m radius must take no damage.");
        }

        [UnityTest]
        public IEnumerator SmokeBomb_Execution0_5s_Deploys3mZoneFor8s_BlocksEnemyLOS()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();

            consume.ConfigureThrowables(
                new NetworkPrefabRef(DaggerGuid),
                new NetworkPrefabRef(BombGuid),
                new NetworkPrefabRef(SmokeGuid));

            yield return InSimulation(() => AddLoot(loot, SmokeId, 1));
            Assert.That(loot.GetLootAmount(SmokeId), Is.EqualTo(1));

            Vector2 smokeTarget = new Vector2(2.5f, 0f);
            Assert.That(consume.TryRequestConsume(SmokeId, smokeTarget), Is.True);
            yield return WaitTicks(2);

            Assert.That(loot.GetLootAmount(SmokeId), Is.EqualTo(0),
                "Smoke bomb unit is deducted immediately upon start of throw.");
            Assert.That(consume.IsConsuming, Is.True, "0.5s execution window active.");
            Assert.That(consume.VoluntaryMovementSpeedMultiplier, Is.EqualTo(0.5f));

            // Wait for 0.5s execution window to complete and zone to deploy
            yield return WaitSimulationSeconds(0.6f);
            Assert.That(consume.IsConsuming, Is.False);
            Assert.That(SmokeZoneRegistry.ActiveZones.Count, Is.GreaterThan(0),
                "Smoke zone must deploy after 0.5s execution.");

            ISmokeZone zone = SmokeZoneRegistry.ActiveZones[0];
            Assert.That(zone.Radius, Is.EqualTo(3.0f).Within(0.01f), "Smoke zone radius must be 3.0m.");

            // Line of sight across the smoke zone must be occluded
            Vector2 enemyPos = new Vector2(6f, 0f);
            Vector2 playerPos = new Vector2(0f, 0f);
            Assert.That(SmokeZoneRegistry.IsLineOccluded(enemyPos, playerPos), Is.True,
                "Smoke zone must block line-of-sight between enemy and player.");

            // Wait until 8.5s total: smoke zone expires and clears
            yield return WaitSimulationSeconds(8.0f);
            Assert.That(SmokeZoneRegistry.ActiveZones.Count, Is.EqualTo(0),
                "Smoke zone must despawn after 8.0s.");
        }

        private void AddLoot(PlayerLootReceiver loot, LootId lootId, int amount)
        {
            var request = new LootTransferRequest(new EntityId(999), loot.Id, lootId, amount, _runner.Tick);
            var origin = RaidLootOriginTransfer.Dungeon(amount);
            Assert.That(loot.ValidateRaidLootOriginReceive(request, origin), Is.EqualTo(LootTransferFailureReason.None));
            loot.CommitRaidLootReceive(request, origin);
        }

        private IEnumerator Begin()
        {
            var owner = new GameObject("ThrowableConsumableTestRunner");
            _runner = owner.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            owner.AddComponent<EntityRegistry>();
            _driver = owner.AddComponent<PlayerAbilityRuntimeSimulationDriver>();

            Assert.That(RaidCode.TryParse("123456", out var code), Is.True);
            Assert.That(RaidTeamId.TryCreate(1, out var firstTeam), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out var secondTeam), Is.True);
            var host = new ProfileId("throwable-player-1");
            var roster = new[]
            {
                new RaidLaunchParticipant(host, firstTeam),
                new RaidLaunchParticipant(new ProfileId("throwable-player-2"), secondTeam)
            };
            Assert.That(RaidLaunchContext.TryCreate(code, host, roster, host, 1, out var launch), Is.True);
            Assert.That(owner.AddComponent<NetworkSpawnManager>().InitializeForRunner(_runner, default, default,
                Array.Empty<NetworkPrefabRef>(), SessionStartupContext.FreshSession, launch), Is.True);

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"throwable-test-{Guid.NewGuid():N}",
                SceneManager = owner.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = owner.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out _attributes), Is.True);
        }

        private PlayerCharacter SpawnAvatar(int sequence, Vector3 position)
        {
            Assert.That(RaidParticipantId.TryCreate(sequence, out var id), Is.True);
            var participant = _runner.Spawn(Load(ParticipantGuid), onBeforeSpawned: (_, instance) =>
                instance.GetComponent<NetworkRaidParticipant>().Initialize($"throwable-player-{sequence}", id,
                    _attributes, ExperienceCurve.InitialLevel, 0, "throwable-test"))
                .GetComponent<NetworkRaidParticipant>();
            var avatar = _runner.Spawn(Load(PlayerGuid), position, Quaternion.identity, _runner.LocalPlayer,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object));
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            ExtractionProgressTestAssertions.AssertRegistered(_runner, avatar);
            return avatar.GetComponent<PlayerCharacter>();
        }

        private EnemyCharacter SpawnEnemy(Vector3 position)
        {
            NetworkObject enemy = _runner.Spawn(Load(EnemyGuid), position, Quaternion.identity);
            foreach (NetworkBehaviour component in enemy.GetComponents<NetworkBehaviour>())
            {
                if (component is EnemyFSM || component is EnemyMovementAIController || component is EnemyCombatAIController)
                {
                    component.enabled = false;
                }
            }
            Physics2D.SyncTransforms();
            return enemy.GetComponent<EnemyCharacter>();
        }

        private NetworkObject Load(string guid) =>
            _runner.Config.PrefabTable.Load(_runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid)), true);

        private IEnumerator InSimulation(Action action)
        {
            int before = _driver.CompletionSequence;
            _driver.RequestOperation(action);
            yield return WaitFor(() => _driver.CompletionSequence != before);
        }

        private IEnumerator WaitTicks(int count)
        {
            int until = _runner.Tick.Raw + count;
            yield return WaitFor(() => _runner.Tick.Raw >= until);
        }

        private IEnumerator WaitSimulationSeconds(float seconds)
        {
            int until = _runner.Tick.Raw + Mathf.CeilToInt(seconds / _runner.DeltaTime);
            yield return WaitFor(() => _runner.Tick.Raw >= until);
        }

        private static IEnumerator WaitFor(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Condition not satisfied within deadline.");
        }
    }
}
#endif
