#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Consumables
{
    public sealed class TimedConsumablePlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private CharacterAttributeState _attributes;
        private static readonly LootId Potion = new("healthpotion");

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner == null) yield break;
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
            Object.Destroy(_runner.gameObject);
        }

        [UnityTest]
        public IEnumerator Completion_AppliesEffectAndSpendsExactlyOnce_NotAtStart()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();
            yield return InSimulation(() => { AddPotions(loot); Damage(player, 20); });
            float before = player.Health;
            int confirmations = 0;
            consume.ConsumeConfirmed += _ => confirmations++;
            Assert.That(consume.TryRequestConsume(Potion), Is.True);
            yield return WaitFor(() => consume.IsConsuming);
            Assert.That(player.Health, Is.EqualTo(before));
            Assert.That(loot.GetLootAmount(Potion), Is.EqualTo(2));
            Assert.That(consume.TryGetConsumingDefinition(out var definition), Is.True);
            Assert.That(definition.LootId, Is.EqualTo(Potion));
            Assert.That(consume.TryRequestConsume(Potion), Is.False);
            yield return WaitFor(() => !consume.IsConsuming);
            yield return WaitFor(() => confirmations == 1);
            Assert.That(player.Health, Is.GreaterThan(before));
            Assert.That(loot.GetLootAmount(Potion), Is.EqualTo(1));
            yield return WaitTicks(5);
            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(loot.GetLootAmount(Potion), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PositiveDamage_CancelsWithoutHealingOrSpending()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();
            yield return InSimulation(() => { AddPotions(loot); Damage(player, 20); });
            float before = player.Health;
            Assert.That(consume.TryRequestConsume(Potion), Is.True);
            yield return WaitFor(() => consume.IsConsuming);
            yield return InSimulation(() => Damage(player, 1));
            Assert.That(consume.IsConsuming, Is.False);
            Assert.That(player.Health, Is.EqualTo(before - 1));
            Assert.That(loot.GetLootAmount(Potion), Is.EqualTo(2));
            yield return WaitTicks(150);
            Assert.That(player.Health, Is.EqualTo(before - 1));
            Assert.That(loot.GetLootAmount(Potion), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RejectedZeroDamage_DoesNotCancel_DownedDoes()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();
            yield return InSimulation(() => { AddPotions(loot); Damage(player, 20); });
            Assert.That(consume.TryRequestConsume(Potion), Is.True);
            yield return WaitFor(() => consume.IsConsuming);
            yield return InSimulation(() => Damage(player, 0));
            Assert.That(consume.IsConsuming, Is.True);
            yield return InSimulation(() => Assert.That(player.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True));
            yield return WaitFor(() => !consume.IsConsuming);
            Assert.That(loot.GetLootAmount(Potion), Is.EqualTo(2));
        }

        private void AddPotions(PlayerLootReceiver loot)
        {
            var request = new LootTransferRequest(new EntityId(123), loot.Id, Potion, 2, _runner.Tick);
            var origin = RaidLootOriginTransfer.Dungeon(2);
            Assert.That(loot.ValidateRaidLootOriginReceive(request, origin), Is.EqualTo(LootTransferFailureReason.None));
            loot.CommitRaidLootReceive(request, origin);
        }

        private void Damage(PlayerCharacter player, float amount) =>
            player.ApplyDamage(new DamageRequest(new EntityId(123), player.Id, amount,
                DamageType.TrueDamage, Vector2.right, Vector2.zero, _runner.Tick));
        private IEnumerator Begin()
        {
            var owner = new GameObject("TimedConsumableTestRunner");
            _runner = owner.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            owner.AddComponent<EntityRegistry>();
            _driver = owner.AddComponent<PlayerAbilityRuntimeSimulationDriver>();
            Assert.That(RaidCode.TryParse("123456", out var code), Is.True);
            Assert.That(RaidTeamId.TryCreate(1, out var firstTeam), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out var secondTeam), Is.True);
            var host = new ProfileId("trap-player-1");
            var roster = new[]
            {
                new RaidLaunchParticipant(host, firstTeam),
                new RaidLaunchParticipant(new ProfileId("trap-player-2"), secondTeam),
                new RaidLaunchParticipant(new ProfileId("trap-player-3"), firstTeam)
            };
            Assert.That(RaidLaunchContext.TryCreate(code, host, roster, host, 1, out var launch), Is.True);
            Assert.That(owner.AddComponent<NetworkSpawnManager>().InitializeForRunner(_runner, default, default,
                Array.Empty<NetworkPrefabRef>(), SessionStartupContext.FreshSession, launch), Is.True);
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"player-immobilization-{Guid.NewGuid():N}",
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
                instance.GetComponent<NetworkRaidParticipant>().Initialize($"trap-player-{sequence}", id,
                    _attributes, ExperienceCurve.InitialLevel, 0, "trap-test"))
                .GetComponent<NetworkRaidParticipant>();
            var avatar = _runner.Spawn(Load(PlayerGuid), position, Quaternion.identity, _runner.LocalPlayer,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object));
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            ExtractionProgressTestAssertions.AssertRegistered(_runner, avatar);
            Assert.That(avatar.GetComponent<PlayerImmobilizationNetworkEffect>(), Is.Not.Null,
                "NetworkPlayer must compose the player immobilization effect and its serialized owner/protection.");
            return avatar.GetComponent<PlayerCharacter>();
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

        private static IEnumerator WaitFor(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Expected authoritative immobilization state was not reached.");
        }

    }
}
#endif
