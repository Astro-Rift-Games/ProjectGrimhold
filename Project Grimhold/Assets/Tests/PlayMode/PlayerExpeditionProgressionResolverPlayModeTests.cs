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
    public sealed class PlayerExpeditionProgressionResolverPlayModeTests
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
        [Category("TASK143")]
        public IEnumerator AdmissionWatermark_InitializesParticipantResultSequence()
        {
            yield return StartRunner();

            NetworkObject participantObject = SpawnParticipant(10, 2, 20, 12);
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionProgressionResolver resolver =
                participantObject.GetComponent<PlayerExpeditionProgressionResolver>();

            Assert.That(participant.ResultSequence, Is.EqualTo(12));
            Assert.That(resolver.BaselineLevel, Is.EqualTo(2));
            Assert.That(resolver.BaselineExperience, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator DefinitiveCauses_CommitExactlyOnceWithTheirConfiguredRetention()
        {
            yield return StartRunner();

            ExpeditionProgressionFinalizationCause[] causes =
            {
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed,
                ExpeditionProgressionFinalizationCause.DefeatConfirmed,
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed,
                ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed
            };
            ExpeditionExperienceResolutionOutcome[] outcomes =
            {
                ExpeditionExperienceResolutionOutcome.Extracted,
                ExpeditionExperienceResolutionOutcome.Defeated,
                ExpeditionExperienceResolutionOutcome.Abandoned,
                ExpeditionExperienceResolutionOutcome.DefinitivelyDisconnected
            };
            long[] consolidated = { 100, 20, 0, 0 };

            for (int index = 0; index < causes.Length; index++)
            {
                NetworkObject participantObject = SpawnParticipant(index + 1, 1, 0);
                NetworkRaidParticipant participant =
                    participantObject.GetComponent<NetworkRaidParticipant>();
                PlayerExpeditionExperienceLedger ledger =
                    participantObject.GetComponent<PlayerExpeditionExperienceLedger>();
                PlayerExpeditionProgressionResolver resolver =
                    participantObject.GetComponent<PlayerExpeditionProgressionResolver>();

                yield return Register(ledger, ExpeditionExperienceSource.PveKill, 100);
                RaidParticipantState state = causes[index] switch
                {
                    ExpeditionProgressionFinalizationCause.ExtractionConfirmed =>
                        RaidParticipantState.Extracted,
                    ExpeditionProgressionFinalizationCause.DefeatConfirmed =>
                        RaidParticipantState.Defeated,
                    _ => RaidParticipantState.Aborted
                };
                ExtractionExperienceTransactionPhase phase = causes[index] ==
                    ExpeditionProgressionFinalizationCause.ExtractionConfirmed
                    ? ExtractionExperienceTransactionPhase.ProgressionPending
                    : ExtractionExperienceTransactionPhase.None;
                yield return Configure(participant, state, causes[index], phase);
                yield return Finalize(resolver, causes[index]);

                Assert.That(resolver.TryGetResolution(out ExpeditionExperienceResolution resolution),
                    Is.True);
                Assert.That(resolution.Outcome, Is.EqualTo(outcomes[index]));
                Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(consolidated[index]));
                Assert.That((bool)ledger.IsFrozen, Is.True);

                yield return Finalize(resolver, causes[index]);
                Assert.That(
                    _driver.LastProgressionResult.Status,
                    Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.AlreadyCommitted));
                Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(consolidated[index]));
            }
        }

        [UnityTest]
        public IEnumerator VoluntaryAbandonAck_ConfirmsPersistenceWithoutAuthorizingReturn()
        {
            yield return StartRunner();

            NetworkObject participantObject = SpawnParticipant(10, 1, 0);
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionProgressionResolver resolver =
                participantObject.GetComponent<PlayerExpeditionProgressionResolver>();

            yield return Register(ledger, ExpeditionExperienceSource.PveKill, 100);
            yield return Configure(
                participant,
                RaidParticipantState.Aborted,
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed,
                ExtractionExperienceTransactionPhase.None);
            yield return Finalize(
                resolver,
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed);

            Assert.That((bool)participant.IsProgressionCommitConfirmed, Is.False);
            Assert.That((bool)participant.IsReturnAuthorized, Is.False);

            int sequence = _driver.CompletionSequence;
            _driver.RequestConfirmProgressionCommit(participant);
            yield return WaitForDriver(sequence);

            Assert.That(_driver.LastResult, Is.True);
            Assert.That((bool)participant.IsProgressionCommitConfirmed, Is.True);
            Assert.That((bool)participant.IsReturnAuthorized, Is.False);
        }

        [UnityTest]
        public IEnumerator Extraction_AppliesMultipleLevelsAndFailureBeforeCommitIsAtomic()
        {
            yield return StartRunner();
            NetworkObject participantObject = SpawnParticipant(5, 1, 90);
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionProgressionResolver resolver =
                participantObject.GetComponent<PlayerExpeditionProgressionResolver>();

            yield return Register(ledger, ExpeditionExperienceSource.PveKill, 250);
            ExpeditionExperienceSnapshot before = ledger.Snapshot;
            yield return Finalize(resolver, ExpeditionProgressionFinalizationCause.None);
            Assert.That(
                _driver.LastProgressionResult.Status,
                Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.IncompatibleLifecycle));
            Assert.That((bool)ledger.IsFrozen, Is.False);
            Assert.That(ledger.Snapshot, Is.EqualTo(before));
            Assert.That((bool)resolver.Committed, Is.False);

            yield return Configure(
                participant,
                RaidParticipantState.Aborted,
                ExpeditionProgressionFinalizationCause.None,
                ExtractionExperienceTransactionPhase.None);
            yield return Finalize(
                resolver,
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed);
            Assert.That(
                _driver.LastProgressionResult.Status,
                Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.IncompatibleLifecycle));
            Assert.That((bool)ledger.IsFrozen, Is.False);
            Assert.That(ledger.Snapshot, Is.EqualTo(before));
            Assert.That((bool)resolver.Committed, Is.False);

            yield return Configure(
                participant,
                RaidParticipantState.Extracted,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed,
                ExtractionExperienceTransactionPhase.ProgressionPending,
                eligibleExtractedLootValue: 100,
                extractedLootExperience: 10);

            int sequence = _driver.CompletionSequence;
            _driver.RequestConfigureProgressionBaseline(resolver, 0, 0);
            yield return WaitForDriver(sequence);
            yield return Finalize(
                resolver,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed);
            Assert.That(
                _driver.LastProgressionResult.Status,
                Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.MissingOrInvalidBaseline));
            Assert.That(participant.ExtractedLootCandidateEligibleValue, Is.EqualTo(100));
            Assert.That(participant.ExtractedLootCandidateExperience, Is.EqualTo(10));
            Assert.That(ledger.Snapshot.ExtractedLootExperience, Is.EqualTo(10));
            Assert.That((bool)ledger.IsFrozen, Is.False);
            Assert.That((bool)resolver.Committed, Is.False);

            sequence = _driver.CompletionSequence;
            _driver.RequestConfigureProgressionBaseline(resolver, 1, 90);
            yield return WaitForDriver(sequence);
            yield return Finalize(
                resolver,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed);

            Assert.That(resolver.TryGetApplication(out ConsolidatedExperienceApplication application),
                Is.True);
            Assert.That(application.Result.PreviousLevel, Is.EqualTo(1));
            Assert.That(application.Result.PreviousExperience, Is.EqualTo(90));
            Assert.That(application.Result.ResultingLevel, Is.GreaterThan(2));
            Assert.That(application.Result.LevelsGained,
                Is.EqualTo(application.Result.ResultingLevel - 1));
        }

        [UnityTest]
        public IEnumerator MissingBaseline_ReturnsIntegrationFailureWithoutFreezingLedger()
        {
            yield return StartRunner();
            NetworkObject participantObject = SpawnParticipant(6, 1, 0);
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionProgressionResolver resolver =
                participantObject.GetComponent<PlayerExpeditionProgressionResolver>();

            yield return Register(ledger, ExpeditionExperienceSource.PveKill, 10);
            int sequence = _driver.CompletionSequence;
            _driver.RequestConfigureProgressionBaseline(resolver, 0, 0);
            yield return WaitForDriver(sequence);
            yield return Configure(
                participant,
                RaidParticipantState.Defeated,
                ExpeditionProgressionFinalizationCause.None,
                ExtractionExperienceTransactionPhase.None);
            yield return Finalize(
                resolver,
                ExpeditionProgressionFinalizationCause.DefeatConfirmed);

            Assert.That(
                _driver.LastProgressionResult.Status,
                Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.MissingOrInvalidBaseline));
            Assert.That((bool)ledger.IsFrozen, Is.False);
            Assert.That((bool)resolver.Committed, Is.False);
        }

        [UnityTest]
        public IEnumerator CommittedResult_IsCompleteRepeatableAndPreservedByStateCopy()
        {
            yield return StartRunner();
            NetworkObject sourceObject = SpawnParticipant(7, 1, 90);
            NetworkRaidParticipant participant =
                sourceObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                sourceObject.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionProgressionResolver resolver =
                sourceObject.GetComponent<PlayerExpeditionProgressionResolver>();

            Assert.That(resolver.TryGetProgressionResult(out _), Is.False);
            yield return Register(ledger, ExpeditionExperienceSource.PveKill, 10);
            yield return Register(ledger, ExpeditionExperienceSource.PvpKill, 15);
            yield return Register(ledger, ExpeditionExperienceSource.PveAssist, 4);
            yield return Register(ledger, ExpeditionExperienceSource.PvpAssist, 3);
            yield return Register(ledger, ExpeditionExperienceSource.FirstOpenChest, 5);
            yield return Configure(
                participant,
                RaidParticipantState.Extracted,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed,
                ExtractionExperienceTransactionPhase.ProgressionPending,
                eligibleExtractedLootValue: 100,
                extractedLootExperience: 10);
            yield return Finalize(
                resolver,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed);

            ExpeditionExperienceSnapshot frozen = ledger.Snapshot;
            yield return RegisterRejected(
                ledger,
                ExpeditionExperienceSource.PveKill,
                1,
                ExpeditionExperienceLedgerFailure.LedgerFrozen);
            Assert.That(ledger.Snapshot, Is.EqualTo(frozen));
            Assert.That(ledger.PveKillCount, Is.EqualTo(1));
            Assert.That(resolver.TryGetProgressionResult(out ExpeditionProgressionResult first),
                Is.True);
            Assert.That(resolver.TryGetProgressionResult(out ExpeditionProgressionResult second),
                Is.True);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(ledger.Snapshot, Is.EqualTo(frozen));
            Assert.That(first.Outcome, Is.EqualTo(ExpeditionExperienceResolutionOutcome.Extracted));
            Assert.That(first.PveKillCount, Is.EqualTo(1));
            Assert.That(first.PvpKillCount, Is.EqualTo(1));
            Assert.That(first.PveAssistCount, Is.EqualTo(1));
            Assert.That(first.PvpAssistCount, Is.EqualTo(1));
            Assert.That(first.FirstOpenChestCount, Is.EqualTo(1));
            Assert.That(first.CombatExperience, Is.EqualTo(32));
            Assert.That(first.ExplorationExperience, Is.EqualTo(5));
            Assert.That(first.LootExperience, Is.EqualTo(10));
            Assert.That(first.EligibleExtractedLootValue, Is.EqualTo(100));
            Assert.That(first.ProvisionalExperienceTotal, Is.EqualTo(47));
            Assert.That(first.RetentionBasisPoints, Is.EqualTo(10_000));
            Assert.That(first.ConsolidatedExperience, Is.EqualTo(47));
            Assert.That(first.PreviousLevel, Is.EqualTo(1));
            Assert.That(first.PreviousExperience, Is.EqualTo(90));
            Assert.That(first.ResultingLevel, Is.EqualTo(2));
            Assert.That(first.ResultingExperience, Is.EqualTo(37));
            Assert.That(first.LevelsGained, Is.EqualTo(1));
            Assert.That(first.IsMaxLevel, Is.False);
            Assert.That(first.NextLevelExperienceRequirement, Is.EqualTo(105));

            NetworkObject restoredObject = SpawnParticipant(8, 1, 0);
            PlayerExpeditionExperienceLedger restoredLedger =
                restoredObject.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionProgressionResolver restoredResolver =
                restoredObject.GetComponent<PlayerExpeditionProgressionResolver>();
            int sequence = _driver.CompletionSequence;
            _driver.RequestCopyProgressionState(
                restoredLedger,
                restoredResolver,
                ledger,
                resolver);
            yield return WaitForDriver(sequence);

            Assert.That(
                restoredResolver.TryGetProgressionResult(
                    out ExpeditionProgressionResult restoredResult),
                Is.True);
            Assert.That(restoredResult, Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator CommittedMaximumLevel_IsUnambiguous()
        {
            yield return StartRunner();
            NetworkObject participantObject = SpawnParticipant(9, 30, 0);
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionProgressionResolver resolver =
                participantObject.GetComponent<PlayerExpeditionProgressionResolver>();

            yield return Register(ledger, ExpeditionExperienceSource.PveKill, 10);
            yield return Configure(
                participant,
                RaidParticipantState.Defeated,
                ExpeditionProgressionFinalizationCause.None,
                ExtractionExperienceTransactionPhase.None);
            yield return Finalize(
                resolver,
                ExpeditionProgressionFinalizationCause.DefeatConfirmed);

            Assert.That(resolver.TryGetProgressionResult(out ExpeditionProgressionResult result),
                Is.True);
            Assert.That(result.IsMaxLevel, Is.True);
            Assert.That(result.NextLevelExperienceRequirement, Is.Zero);
            Assert.That(result.EligibleExtractedLootValue, Is.Zero);
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("ExpeditionProgressionResolverTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            _driver = runnerObject.AddComponent<ExpeditionExperienceSimulationDriver>();

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-110-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private NetworkObject SpawnParticipant(
            int participantId,
            int baselineLevel,
            long baselineExperience,
            int baselineResultSequence = 0)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(ParticipantPrefabGuid));
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            return _runner.Spawn(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        $"task-110-{participantId}",
                        CreateParticipantId(participantId),
                        ProgressionBalanceDefaults.InitialCharacterAttributeState,
                        baselineLevel,
                        baselineExperience,
                        "task-110-generation",
                        baselineResultSequence: baselineResultSequence));
        }

        private IEnumerator Register(
            PlayerExpeditionExperienceLedger ledger,
            ExpeditionExperienceSource source,
            long amount)
        {
            int sequence = _driver.CompletionSequence;
            _driver.RequestRegisterReward(ledger, source, amount);
            yield return WaitForDriver(sequence);
            Assert.That(_driver.LastResult, Is.True, _driver.LastFailure.ToString());
        }

        private IEnumerator RegisterRejected(
            PlayerExpeditionExperienceLedger ledger,
            ExpeditionExperienceSource source,
            long amount,
            ExpeditionExperienceLedgerFailure expectedFailure)
        {
            int sequence = _driver.CompletionSequence;
            _driver.RequestRegisterReward(ledger, source, amount);
            yield return WaitForDriver(sequence);
            Assert.That(_driver.LastResult, Is.False);
            Assert.That(_driver.LastFailure, Is.EqualTo(expectedFailure));
        }

        private IEnumerator Configure(
            NetworkRaidParticipant participant,
            RaidParticipantState state,
            ExpeditionProgressionFinalizationCause cause,
            ExtractionExperienceTransactionPhase phase,
            long eligibleExtractedLootValue = 0,
            long extractedLootExperience = 0)
        {
            int sequence = _driver.CompletionSequence;
            _driver.RequestConfigureProgressionFinalization(
                participant,
                state,
                cause,
                phase,
                eligibleExtractedLootValue,
                extractedLootExperience);
            yield return WaitForDriver(sequence);
        }

        private IEnumerator Finalize(
            PlayerExpeditionProgressionResolver resolver,
            ExpeditionProgressionFinalizationCause cause)
        {
            int sequence = _driver.CompletionSequence;
            _driver.RequestFinalizeProgression(resolver, cause);
            yield return WaitForDriver(sequence);
        }

        private IEnumerator WaitForDriver(int previousSequence)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_driver.CompletionSequence == previousSequence &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(_driver.CompletionSequence, Is.Not.EqualTo(previousSequence));
        }

        private static RaidParticipantId CreateParticipantId(int value)
        {
            Assert.That(RaidParticipantId.TryCreate(value, out RaidParticipantId id), Is.True);
            return id;
        }
    }
}
#endif
