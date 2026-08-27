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
                ExpeditionExperienceSource.PveKill,
                12,
                true,
                ExpeditionExperienceLedgerFailure.None);
            Assert.That(ledger.Snapshot.KillExperience, Is.EqualTo(12));
            Assert.That(ledger.Snapshot.TotalExperience, Is.EqualTo(12));

            yield return Register(
                ledger,
                ExpeditionExperienceSource.None,
                20,
                false,
                ExpeditionExperienceLedgerFailure.InvalidSource);
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
                    ExpeditionExperienceSource.FirstOpenChest,
                    1,
                    false,
                    ExpeditionExperienceLedgerFailure.ParticipantNotRaiding);
                Assert.That(ledger.Snapshot, Is.EqualTo(before), terminalStates[index].ToString());
                yield return SetParticipantState(participant, RaidParticipantState.Raiding);
            }
        }

        [UnityTest]
        public IEnumerator ConfirmedExtractionReward_RequiresMatchingConfirmationAndPreservesBreakdown()
        {
            yield return StartRunner();
            NetworkObject participantObject = SpawnParticipant();
            NetworkRaidParticipant participant = participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();

            yield return Register(
                ledger,
                ExpeditionExperienceSource.PveKill,
                12,
                true,
                ExpeditionExperienceLedgerFailure.None);
            yield return ConfigureExtraction(participant, 1, false);
            yield return RegisterExtracted(
                ledger,
                1,
                8,
                false,
                ExpeditionExperienceLedgerFailure.ExtractionNotConfirmed);
            yield return ConfigureExtraction(participant, 1, true);
            yield return RegisterExtracted(
                ledger,
                2,
                8,
                false,
                ExpeditionExperienceLedgerFailure.ResultSequenceMismatch);
            yield return RegisterExtracted(
                ledger,
                1,
                8,
                true,
                ExpeditionExperienceLedgerFailure.None);
            yield return RegisterExtracted(
                ledger,
                1,
                99,
                true,
                ExpeditionExperienceLedgerFailure.None);

            Assert.That(ledger.Snapshot.KillExperience, Is.EqualTo(12));
            Assert.That(ledger.Snapshot.AssistExperience, Is.Zero);
            Assert.That(ledger.Snapshot.ExplorationExperience, Is.Zero);
            Assert.That(ledger.Snapshot.ExtractedLootExperience, Is.EqualTo(8));
            Assert.That(ledger.ExtractedLootResolvedResultSequence, Is.EqualTo(1));
        }

        [Test]
        public void NonAuthorityLedger_RejectsWithoutMutation()
        {
            var participantObject = new GameObject("NonAuthorityLedger");
            PlayerExpeditionExperienceLedger ledger =
                participantObject.AddComponent<PlayerExpeditionExperienceLedger>();

            Assert.That(
                ledger.TryRegisterNormalReward(
                    ExpeditionExperienceSource.PveKill,
                    10,
                    out ExpeditionExperienceLedgerFailure failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(ExpeditionExperienceLedgerFailure.MissingStateAuthority));

            Object.DestroyImmediate(participantObject);
        }

        [UnityTest]
        public IEnumerator NormalSources_RejectInvalidAndOverflowWithoutPartialMutation()
        {
            yield return StartRunner();
            NetworkObject participantObject = SpawnParticipant();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();

            yield return Register(
                ledger,
                ExpeditionExperienceSource.None,
                10,
                false,
                ExpeditionExperienceLedgerFailure.InvalidSource);
            yield return Register(
                ledger,
                (ExpeditionExperienceSource)byte.MaxValue,
                10,
                false,
                ExpeditionExperienceLedgerFailure.InvalidSource);
            yield return Register(
                ledger,
                ExpeditionExperienceSource.PveKill,
                -1,
                false,
                ExpeditionExperienceLedgerFailure.InvalidAmount);
            Assert.That(ledger.Snapshot, Is.EqualTo(ExpeditionExperienceSnapshot.Empty));
            Assert.That(ledger.PveKillCount, Is.Zero);

            yield return Register(
                ledger,
                ExpeditionExperienceSource.PveKill,
                long.MaxValue,
                true,
                ExpeditionExperienceLedgerFailure.None);
            ExpeditionExperienceSnapshot full = ledger.Snapshot;
            yield return Register(
                ledger,
                ExpeditionExperienceSource.PvpKill,
                1,
                false,
                ExpeditionExperienceLedgerFailure.CategoryOverflow);
            yield return Register(
                ledger,
                ExpeditionExperienceSource.FirstOpenChest,
                1,
                false,
                ExpeditionExperienceLedgerFailure.TotalOverflow);
            Assert.That(ledger.Snapshot, Is.EqualTo(full));
            Assert.That(ledger.PveKillCount, Is.EqualTo(1));
            Assert.That(ledger.PvpKillCount, Is.Zero);
            Assert.That(ledger.FirstOpenChestCount, Is.Zero);

            int sequence = _driver.CompletionSequence;
            _driver.RequestConfigureSourceCount(
                ledger,
                ExpeditionExperienceSource.PveKill,
                int.MaxValue);
            yield return WaitUntilCompletion(sequence);
            Assert.That(_driver.LastResult, Is.True);

            ExpeditionExperienceSnapshot before = ledger.Snapshot;
            yield return Register(
                ledger,
                ExpeditionExperienceSource.PveKill,
                10,
                false,
                ExpeditionExperienceLedgerFailure.SourceCountOverflow);
            Assert.That(ledger.Snapshot, Is.EqualTo(before));
            Assert.That(ledger.PveKillCount, Is.EqualTo(int.MaxValue));
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
            ExpeditionExperienceSource source,
            long amount,
            bool expectedResult,
            ExpeditionExperienceLedgerFailure expectedFailure)
        {
            int previousSequence = _driver.CompletionSequence;
            _driver.RequestRegisterReward(ledger, source, amount);
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

        private IEnumerator ConfigureExtraction(
            NetworkRaidParticipant participant,
            int resultSequence,
            bool isConfirmed)
        {
            int previousSequence = _driver.CompletionSequence;
            _driver.RequestConfigureExtraction(participant, resultSequence, isConfirmed);
            yield return WaitUntilCompletion(previousSequence);
            Assert.That(_driver.LastResult, Is.True);
        }

        private IEnumerator RegisterExtracted(
            PlayerExpeditionExperienceLedger ledger,
            int resultSequence,
            long amount,
            bool expectedResult,
            ExpeditionExperienceLedgerFailure expectedFailure)
        {
            int previousSequence = _driver.CompletionSequence;
            _driver.RequestRegisterExtractedLootReward(ledger, resultSequence, amount);
            yield return WaitUntilCompletion(previousSequence);
            Assert.That(_driver.LastResult, Is.EqualTo(expectedResult));
            Assert.That(_driver.LastFailure, Is.EqualTo(expectedFailure));
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
