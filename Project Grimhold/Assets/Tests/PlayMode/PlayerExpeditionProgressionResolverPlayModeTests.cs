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

                yield return Register(ledger, ExpeditionExperienceCategory.Kill, 100);
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
                Assert.That(ledger.IsFrozen, Is.True);

                yield return Finalize(resolver, causes[index]);
                Assert.That(
                    _driver.LastProgressionResult.Status,
                    Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.AlreadyCommitted));
                Assert.That(resolution.ConsolidatedExperience, Is.EqualTo(consolidated[index]));
            }
        }

        [UnityTest]
        public IEnumerator Extraction_AppliesMultipleLevelsAndFailureBeforeCommitIsAtomic()
        {
            yield return StartRunner();
            NetworkObject participantObject = SpawnParticipant(20, 1, 90);
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionProgressionResolver resolver =
                participantObject.GetComponent<PlayerExpeditionProgressionResolver>();

            yield return Register(ledger, ExpeditionExperienceCategory.Kill, 250);
            ExpeditionExperienceSnapshot before = ledger.Snapshot;
            yield return Finalize(resolver, ExpeditionProgressionFinalizationCause.None);
            Assert.That(
                _driver.LastProgressionResult.Status,
                Is.EqualTo(PlayerExpeditionProgressionFinalizationStatus.IncompatibleLifecycle));
            Assert.That(ledger.IsFrozen, Is.False);
            Assert.That(ledger.Snapshot, Is.EqualTo(before));
            Assert.That(resolver.Committed, Is.False);

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
            Assert.That(ledger.IsFrozen, Is.False);
            Assert.That(ledger.Snapshot, Is.EqualTo(before));
            Assert.That(resolver.Committed, Is.False);

            yield return Configure(
                participant,
                RaidParticipantState.Extracted,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed,
                ExtractionExperienceTransactionPhase.ProgressionPending);
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
            NetworkObject participantObject = SpawnParticipant(30, 1, 0);
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();
            PlayerExpeditionExperienceLedger ledger =
                participantObject.GetComponent<PlayerExpeditionExperienceLedger>();
            PlayerExpeditionProgressionResolver resolver =
                participantObject.GetComponent<PlayerExpeditionProgressionResolver>();

            yield return Register(ledger, ExpeditionExperienceCategory.Kill, 10);
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
            Assert.That(ledger.IsFrozen, Is.False);
            Assert.That(resolver.Committed, Is.False);
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
                        baselineLevel,
                        baselineExperience,
                        "task-110-generation",
                        baselineResultSequence: baselineResultSequence));
        }

        private IEnumerator Register(
            PlayerExpeditionExperienceLedger ledger,
            ExpeditionExperienceCategory category,
            long amount)
        {
            int sequence = _driver.CompletionSequence;
            _driver.RequestRegisterReward(ledger, category, amount);
            yield return WaitForDriver(sequence);
            Assert.That(_driver.LastResult, Is.True, _driver.LastFailure.ToString());
        }

        private IEnumerator Configure(
            NetworkRaidParticipant participant,
            RaidParticipantState state,
            ExpeditionProgressionFinalizationCause cause,
            ExtractionExperienceTransactionPhase phase)
        {
            int sequence = _driver.CompletionSequence;
            _driver.RequestConfigureProgressionFinalization(participant, state, cause, phase);
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
