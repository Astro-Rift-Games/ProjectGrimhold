#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Abilities
{
    /// <summary>Avatar-owned deadline and real trap targeting on one authoritative runner, not migration proof.</summary>
    public sealed class PlayerImmobilizationPlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string TrapGuid = "f350a40a118648c4e8e6edf70efaa3b5";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private CharacterAttributeState _attributes;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner == null) yield break;
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
            Object.Destroy(_runner.gameObject);
        }

        [UnityTest]
        public IEnumerator Deadline_RefreshesWithoutStacking_ExpiresAndRefusesOutsideSimulation()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var effect = player.GetComponent<PlayerImmobilizationNetworkEffect>();
            Assert.That(effect.IsActive, Is.False);
            Assert.That(effect.TryApply(3f), Is.False);
            yield return InSimulation(() => Assert.That(effect.TryApply(3f), Is.True));
            TickTimer original = ReadDeadline(effect);
            yield return WaitTicks(3);
            yield return InSimulation(() => Assert.That(effect.TryApply(0.25f), Is.True));
            Assert.That(ReadDeadline(effect).TargetTick, Is.LessThan(original.TargetTick));
            Assert.That(effect.IsActive, Is.True);
            yield return WaitFor(() => !effect.IsActive);
        }

        [UnityTest]
        public IEnumerator Purification_RemovesActiveState_RejectsReapplicationDuringProtection()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var effect = player.GetComponent<PlayerImmobilizationNetworkEffect>();
            var purification = player.GetComponent<PurificationNetworkEffect>();
            yield return InSimulation(() =>
            {
                Assert.That(effect.TryApply(3f), Is.True);
                Assert.That(purification.HasPurifiableState, Is.True);
                Assert.That(purification.TryApply(0.25f), Is.True);
                Assert.That(effect.IsActive, Is.False);
                Assert.That(purification.HasPurifiableState, Is.False);
                Assert.That(effect.TryApply(3f), Is.False);
            });
            yield return WaitFor(() => !purification.IsProtectedFromPurifiableStates);
            yield return InSimulation(() => Assert.That(effect.TryApply(3f), Is.True));
            Assert.That(effect.IsActive, Is.True);
        }

        [UnityTest]
        public IEnumerator StateCopyAndSpawnedReplay_KeepDeadline_DeathClearsRestriction()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var target = SpawnAvatar(2, Vector3.right * 20f);
            var effect = player.GetComponent<PlayerImmobilizationNetworkEffect>();
            var copied = target.GetComponent<PlayerImmobilizationNetworkEffect>();
            yield return InSimulation(() => Assert.That(effect.TryApply(3f), Is.True));
            TickTimer deadline = ReadDeadline(effect);
            yield return InSimulation(() =>
            {
                copied.CopyStateFrom(effect);
                copied.Spawned();
                copied.Spawned();
                Assert.That(ReadDeadline(copied), Is.EqualTo(deadline));
                Assert.That(copied.IsActive, Is.True);
                typeof(CharacterBase).GetProperty("Health", Members).SetValue(target, 0f);
                Assert.That(copied.IsActive, Is.False);
            });
            yield return WaitTicks(2);
            Assert.That(ReadDeadline(copied).IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator Downed_ClearsDeadlineAndRefusesReapplication()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var effect = player.GetComponent<PlayerImmobilizationNetworkEffect>();
            yield return InSimulation(() =>
            {
                Assert.That(effect.TryApply(3f), Is.True);
                Assert.That(player.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True);
                Assert.That(effect.IsActive, Is.False);
                Assert.That(effect.TryApply(3f), Is.False);
            });
            yield return WaitTicks(2);
            Assert.That(ReadDeadline(effect).IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator Trap_AlliesUnknownAndProtectedRivalDoNotConsume_EligibleRivalTriggersWithoutDamage()
        {
            yield return Begin();
            Vector3 center = new Vector3(60f, 0f, 0f);
            var caster = SpawnAvatar(1, Vector3.zero);
            SpawnAvatar(3, center); // Initial teammate.
            SpawnAvatar(4, center); // Not present in the frozen roster.
            var rival = SpawnAvatar(2, center);
            var effect = rival.GetComponent<PlayerImmobilizationNetworkEffect>();
            var purification = rival.GetComponent<PurificationNetworkEffect>();
            yield return InSimulation(() => Assert.That(purification.TryApply(0.5f), Is.True));
            var trapObject = _runner.Spawn(Load(TrapGuid), center, Quaternion.identity,
                onBeforeSpawned: (_, instance) => instance.GetComponent<NetworkTrap>().InitializeNetworkState(
                    caster.Id, 10f, 1.5f, LayerMask.GetMask("Character"), 3f, 999f, 0.1f,
                    DamageType.Physical, true, 0.5f));
            NetworkId trapId = trapObject.Id;
            int triggers = 0;
            trapObject.GetComponent<NetworkTrap>().Triggered += id =>
            {
                Assert.That(id, Is.EqualTo(rival.Id));
                triggers++;
            };
            float health = rival.Health;
            yield return WaitTicks(3);
            Assert.That(_runner.TryFindObject(trapId, out _), Is.True);
            Assert.That(triggers, Is.Zero);
            yield return WaitFor(() => effect.IsActive);
            Assert.That(triggers, Is.EqualTo(1));
            Assert.That(_runner.TryFindObject(trapId, out _), Is.False);
            yield return WaitFor(() => !effect.IsActive);
            Assert.That(rival.Health, Is.EqualTo(health), "Player immobilization never inherits creature DoT.");
        }

        private IEnumerator Begin()
        {
            var owner = new GameObject("PlayerImmobilizationTestRunner");
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
            var avatar = _runner.Spawn(Load(PlayerGuid), position, Quaternion.identity,
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

        private static TickTimer ReadDeadline(PlayerImmobilizationNetworkEffect effect) =>
            (TickTimer)typeof(PlayerImmobilizationNetworkEffect).GetProperty("ExpiryTimer", Members).GetValue(effect);
    }
}
#endif
