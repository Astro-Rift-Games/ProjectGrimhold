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
    public sealed class PlayerExpeditionExperienceLedgerPlayModeTests
    {
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";

        private NetworkRunner _runner;
        private ExpeditionExperienceSimulationDriver _driver;

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
        public IEnumerator AuthorityLedger_AppliesNormalRewardsAndFreezesForEveryTerminalState()
        {
            yield return StartRunner();
            NetworkObject participantObject = SpawnParticipant();
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();

            Assert.That(participant, Is.Not.Null);
            Assert.That(ledger, Is.Not.Null);
            Assert.That(ledger.HasStateAuthority, Is.True);
            Assert.That(ledger.Snapshot, Is.EqualTo(ExpeditionExperienceSnapshot.Empty));

            yield return Register(
                ledger,
                ExpeditionExperienceCategory.Kill,
                12,
                true,
                ExpeditionExperienceLedgerFailure.None);
            Assert.That(ledger.Snapshot.KillExperience, Is.EqualTo(12));
            Assert.That(ledger.Snapshot.TotalExperience, Is.EqualTo(12));

            yield return Register(
                ledger,
                ExpeditionExperienceCategory.ExtractedLoot,
                20,
                false,
                ExpeditionExperienceLedgerFailure.ExtractedLootRequiresExtractionResolution);
            Assert.That(ledger.Snapshot.ExtractedLootExperience, Is.Zero);
            Assert.That(ledger.Snapshot.TotalExperience, Is.EqualTo(12));

            RaidParticipantState[] terminalStates =
            {
                RaidParticipantState.Defeated,
                RaidParticipantState.Extracted,
                RaidParticipantState.Aborted
            };
            for (int index = 0; index < terminalStates.Length; index++)
            {
                yield return SetParticipantState(participant, terminalStates[index]);
                ExpeditionExperienceSnapshot before = ledger.Snapshot;
                yield return Register(
                    ledger,
                    ExpeditionExperienceCategory.Exploration,
                    1,
                    false,
                    ExpeditionExperienceLedgerFailure.ParticipantNotRaiding);
                Assert.That(ledger.Snapshot, Is.EqualTo(before), terminalStates[index].ToString());
                yield return SetParticipantState(participant, RaidParticipantState.Raiding);
            }
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("ExpeditionExperienceLedgerTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            _driver = runnerObject.AddComponent<ExpeditionExperienceSimulationDriver>();

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-129-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private NetworkObject SpawnParticipant()
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(ParticipantPrefabGuid));
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null);
            return _runner.Spawn(prefab, Vector3.zero, Quaternion.identity);
        }

        private IEnumerator Register(
            PlayerExpeditionExperienceLedger ledger,
            ExpeditionExperienceCategory category,
            long amount,
            bool expectedResult,
            ExpeditionExperienceLedgerFailure expectedFailure)
        {
            int previousSequence = _driver.CompletionSequence;
            _driver.RequestRegisterReward(ledger, category, amount);
            yield return WaitUntilCompletion(previousSequence);
            Assert.That(_driver.LastResult, Is.EqualTo(expectedResult));
            Assert.That(_driver.LastFailure, Is.EqualTo(expectedFailure));
        }

        private IEnumerator SetParticipantState(
            NetworkRaidParticipant participant,
            RaidParticipantState state)
        {
            int previousSequence = _driver.CompletionSequence;
            _driver.RequestSetParticipantState(participant, state);
            yield return WaitUntilCompletion(previousSequence);
            Assert.That(_driver.LastResult, Is.True);
            Assert.That(participant.State, Is.EqualTo(state));
        }

        private IEnumerator WaitUntilCompletion(int previousSequence)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_driver.CompletionSequence == previousSequence &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(_driver.CompletionSequence, Is.Not.EqualTo(previousSequence));
        }
    }
}
#endif
