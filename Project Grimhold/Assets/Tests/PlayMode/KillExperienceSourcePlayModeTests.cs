#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Progression
{
    public sealed class KillExperienceSourcePlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string BlueSlimePrefabGuid = "a559f183b6c025f41bdca5fdd17eda02";
        private const string GreenSlimePrefabGuid = "5deca87613df0fa409d98702aec643d4";
        private const string RedSlimePrefabGuid = "e67f9247cfa1c6948b390f8306c1a8af";
        private const string RangedEnemyPrefabGuid = "6f7ab2fe6d6193a4ea17a843ff58f94b";

        private NetworkRunner _runner;
        private EnemyFatalDamageSimulationDriver _damageDriver;
        private ExpeditionExperienceSimulationDriver _experienceDriver;
        private NetworkObject _firstPlayer;
        private NetworkObject _secondPlayer;
        private NetworkRaidParticipant _firstParticipant;
        private NetworkRaidParticipant _secondParticipant;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null && _runner.IsRunning)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted)
                {
                    yield return null;
                }
            }

            if (_runner != null)
            {
                Object.DestroyImmediate(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator FatalRewardsExactValuesOnceAndIsolatesParticipants()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject slime = SpawnEnemy(GreenSlimePrefabGuid, Vector3.zero);
            NetworkObject ranged = SpawnEnemy(RangedEnemyPrefabGuid, Vector3.right * 2f);

            yield return ResolveDamage(slime, _firstPlayer, 1000f);
            AssertLedger(_firstParticipant, 10);
            AssertLedger(_secondParticipant, 0);
            Assert.That((bool)slime.GetComponent<KillExperienceSource>().IsGranted, Is.True);

            yield return ResolveDamage(slime, _secondPlayer, 1000f);
            Assert.That(_damageDriver.LastResult.IsApplied, Is.False);
            AssertLedger(_firstParticipant, 10);
            AssertLedger(_secondParticipant, 0);

            yield return ResolveDamage(ranged, _secondPlayer, 1000f);
            AssertLedger(_firstParticipant, 10);
            AssertLedger(_secondParticipant, 15);
            Assert.That((bool)ranged.GetComponent<KillExperienceSource>().IsGranted, Is.True);
        }

        [UnityTest]
        public IEnumerator NonFatalAndInvalidAttackerPreserveReward()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject nonFatalTarget = SpawnEnemy(RedSlimePrefabGuid, Vector3.zero);
            KillExperienceSource nonFatalSource = nonFatalTarget.GetComponent<KillExperienceSource>();

            yield return ResolveDamage(nonFatalTarget, _firstPlayer, 1f);
            Assert.That(_damageDriver.LastResult.IsFatal, Is.False);
            Assert.That(nonFatalSource.IsAvailable, Is.True);
            AssertLedger(_firstParticipant, 0);

            NetworkObject invalidAttackerTarget = SpawnEnemy(BlueSlimePrefabGuid, Vector3.right * 2f);
            KillExperienceSource invalidSource = invalidAttackerTarget.GetComponent<KillExperienceSource>();
            yield return ResolveDamage(
                invalidAttackerTarget,
                new EntityId(int.MaxValue),
                1000f);

            Assert.That(_damageDriver.LastResult.IsFatal, Is.True);
            Assert.That(invalidSource.IsAvailable, Is.True);
            AssertLedger(_firstParticipant, 0);
            AssertLedger(_secondParticipant, 0);

            NetworkObject enemyAttacker = SpawnEnemy(GreenSlimePrefabGuid, Vector3.left * 2f);
            NetworkObject nonPlayerTarget = SpawnEnemy(BlueSlimePrefabGuid, Vector3.up * 2f);
            yield return ResolveDamage(
                nonPlayerTarget,
                enemyAttacker.GetComponent<EnemyCharacter>().Id,
                1000f);

            Assert.That(nonPlayerTarget.GetComponent<KillExperienceSource>().IsAvailable, Is.True);
            AssertLedger(_firstParticipant, 0);
            AssertLedger(_secondParticipant, 0);
        }

        [UnityTest]
        public IEnumerator LedgerRejectionDoesNotConsumeSource()
        {
            yield return StartRunnerAndSpawnParticipants();
            yield return SetParticipantState(_firstParticipant, RaidParticipantState.Defeated);
            NetworkObject slime = SpawnEnemy(BlueSlimePrefabGuid, Vector3.zero);
            KillExperienceSource source = slime.GetComponent<KillExperienceSource>();

            yield return ResolveDamage(slime, _firstPlayer, 1000f);

            Assert.That(_damageDriver.LastResult.IsFatal, Is.True);
            Assert.That(source.IsAvailable, Is.True);
            Assert.That((bool)source.IsGranted, Is.False);
            AssertLedger(_firstParticipant, 0);
        }

        [UnityTest]
        public IEnumerator InvalidLinkAndStaleParticipantAvatarDoNotConsumeSources()
        {
            yield return StartRunnerAndSpawnParticipants();

            NetworkObject unlinkedAvatar = SpawnAvatarWithoutCurrentParticipation(
                participantObject: null,
                Vector3.up * 2f);
            NetworkObject unlinkedTarget = SpawnEnemy(BlueSlimePrefabGuid, Vector3.zero);
            yield return ResolveDamage(unlinkedTarget, unlinkedAvatar, 1000f);
            Assert.That(unlinkedTarget.GetComponent<KillExperienceSource>().IsAvailable, Is.True);

            NetworkObject staleAvatar = SpawnAvatarWithoutCurrentParticipation(
                _firstParticipant.Object,
                Vector3.down * 2f);
            NetworkObject staleTarget = SpawnEnemy(BlueSlimePrefabGuid, Vector3.right * 2f);
            yield return ResolveDamage(staleTarget, staleAvatar, 1000f);
            Assert.That(staleTarget.GetComponent<KillExperienceSource>().IsAvailable, Is.True);

            AssertLedger(_firstParticipant, 0);
            AssertLedger(_secondParticipant, 0);
        }

        [UnityTest]
        public IEnumerator CopyStateFromPreservesAvailableAndGrantedOneShotState()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject grantedObject = SpawnEnemy(BlueSlimePrefabGuid, Vector3.zero);
            NetworkObject availableObject = SpawnEnemy(BlueSlimePrefabGuid, Vector3.right * 2f);

            yield return ResolveDamage(grantedObject, _firstPlayer, 1000f);
            KillExperienceSource granted = grantedObject.GetComponent<KillExperienceSource>();
            KillExperienceSource available = availableObject.GetComponent<KillExperienceSource>();
            Assert.That((bool)granted.IsGranted, Is.True);
            Assert.That((bool)available.IsGranted, Is.False);

            available.CopyStateFrom(granted);
            Assert.That((bool)available.IsGranted, Is.True);
            Assert.That(available.TryGrantTo(GetLedger(_secondParticipant)), Is.False);
            AssertLedger(_secondParticipant, 0);

            NetworkObject freshAvailableObject = SpawnEnemy(BlueSlimePrefabGuid, Vector3.right * 4f);
            granted.CopyStateFrom(freshAvailableObject.GetComponent<KillExperienceSource>());
            Assert.That((bool)granted.IsGranted, Is.False);
            Assert.That(granted.IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator ParticipantCopyStateFromPreservesStableRaidParticipantIdWithoutReassignment()
        {
            yield return StartRunnerAndSpawnParticipants();
            NetworkObject restoredObject = _runner.Spawn(
                LoadPrefab(ParticipantPrefabGuid),
                Vector3.up * 4f,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        "replacement-profile",
                        CreateParticipantId(16),
                        ExperienceCurve.InitialLevel,
                        0,
                        "replacement-generation"));
            NetworkRaidParticipant restored =
                restoredObject.GetComponent<NetworkRaidParticipant>();

            restored.CopyStateFrom(_firstParticipant);

            Assert.That(restored.RaidParticipantId, Is.EqualTo(_firstParticipant.RaidParticipantId));
            Assert.That(restored.ProfileId, Is.EqualTo(_firstParticipant.ProfileId));
            Assert.That(restored.RaidParticipantId.Value, Is.EqualTo(1));
        }

        private IEnumerator StartRunnerAndSpawnParticipants()
        {
            var runnerObject = new GameObject("KillExperienceSourceTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _damageDriver = runnerObject.AddComponent<EnemyFatalDamageSimulationDriver>();
            _experienceDriver = runnerObject.AddComponent<ExpeditionExperienceSimulationDriver>();

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-130-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            (_firstParticipant, _firstPlayer) = SpawnParticipantAndAvatar("first-profile", Vector3.left);
            (_secondParticipant, _secondPlayer) = SpawnParticipantAndAvatar("second-profile", Vector3.right);
            yield return null;
        }

        private (NetworkRaidParticipant participant, NetworkObject avatar) SpawnParticipantAndAvatar(
            string profileId,
            Vector3 position)
        {
            NetworkObject participantObject = _runner.Spawn(
                LoadPrefab(ParticipantPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        profileId,
                        CreateParticipantId(position.x < 0f ? 1 : 2),
                        ExperienceCurve.InitialLevel,
                        0,
                        "task-130-generation"));
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();

            ExpectBasePrefabExtractionProgressValidationError();
            NetworkObject avatar = _runner.Spawn(
                LoadPrefab(PlayerPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participantObject));
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            return (participant, avatar);
        }

        private static RaidParticipantId CreateParticipantId(int value)
        {
            RaidParticipantId.TryCreate(value, out RaidParticipantId participantId);
            return participantId;
        }

        private NetworkObject SpawnEnemy(string prefabGuid, Vector3 position)
        {
            NetworkObject enemy = _runner.Spawn(
                LoadPrefab(prefabGuid),
                position,
                Quaternion.identity);
            Assert.That(enemy, Is.Not.Null);
            return enemy;
        }

        private NetworkObject SpawnAvatarWithoutCurrentParticipation(
            NetworkObject participantObject,
            Vector3 position)
        {
            ExpectBasePrefabExtractionProgressValidationError();
            return _runner.Spawn(
                LoadPrefab(PlayerPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: participantObject == null
                    ? null
                    : (_, instance) =>
                        instance.GetComponent<RaidAvatarParticipantLink>()
                            .Initialize(participantObject));
        }

        private IEnumerator ResolveDamage(
            NetworkObject target,
            NetworkObject attacker,
            float amount)
        {
            yield return ResolveDamage(
                target,
                attacker.GetComponent<PlayerCharacter>().Id,
                amount);
        }

        private IEnumerator ResolveDamage(NetworkObject target, EntityId attackerId, float amount)
        {
            int tick = _runner.Tick;
            _damageDriver.Target = target.GetComponent<EnemyCharacter>();
            _damageDriver.Resolver = target.GetComponent<DamageResolver>();
            _damageDriver.AttackerId = attackerId;
            _damageDriver.DamageAmount = amount;
            _damageDriver.IsRequested = true;
            while (_damageDriver.IsRequested && _runner.Tick < tick + 20)
            {
                yield return null;
            }

            Assert.That(_damageDriver.IsRequested, Is.False, "Damage request did not execute.");
        }

        private IEnumerator SetParticipantState(
            NetworkRaidParticipant participant,
            RaidParticipantState state)
        {
            int previousSequence = _experienceDriver.CompletionSequence;
            _experienceDriver.RequestSetParticipantState(participant, state);
            while (_experienceDriver.CompletionSequence == previousSequence)
            {
                yield return null;
            }
        }

        private NetworkObject LoadPrefab(string guid)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            Assert.That(prefabId.IsValid, Is.True, guid);
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null, guid);
            return prefab;
        }

        private static PlayerExpeditionExperienceLedger GetLedger(
            NetworkRaidParticipant participant) =>
            participant.GetComponent<PlayerExpeditionExperienceLedger>();

        private static void AssertLedger(NetworkRaidParticipant participant, long expectedKill)
        {
            PlayerExpeditionExperienceLedger ledger = GetLedger(participant);
            ExpeditionExperienceSnapshot snapshot = ledger.Snapshot;
            Assert.That(snapshot.KillExperience, Is.EqualTo(expectedKill));
            Assert.That(snapshot.TotalExperience, Is.EqualTo(expectedKill));
            Assert.That(ledger.PveKillCount, Is.EqualTo(expectedKill > 0 ? 1 : 0));
            Assert.That(ledger.PvpKillCount, Is.Zero);
        }

        private static void ExpectBasePrefabExtractionProgressValidationError()
        {
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.");
        }
    }
}
#endif
