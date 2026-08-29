#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Progression
{
    public sealed class CharacterAttributeProfileStoreTests
    {
        private static readonly ProfileId Profile =
            new("91919191919191919191919191919191");

        [Test]
        public void InMemoryProfile_InitializesConfiguredAttributesAndClonesAcceptedCandidates()
        {
            LootDefinitionCatalog catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            try
            {
                var repository = new InMemoryLocalProfileRepository();
                Assert.That(repository.Initialize(Profile, catalog), Is.True);
                Assert.That(
                    repository.Snapshot.CharacterAttributes,
                    Is.EqualTo(ProgressionBalanceDefaults.InitialCharacterAttributeState));

                Assert.That(CharacterAttributeState.TryCreate(
                    26, 6, 7, 8, 9, 10, 3, out CharacterAttributeState custom), Is.True);
                LocalProfileSnapshot candidate = repository.Snapshot.Clone();
                candidate.CharacterAttributes = custom;

                Assert.That(repository.TrySave(candidate, out string error), Is.True, error);
                candidate.CharacterAttributes = default;

                Assert.That(repository.Snapshot.CharacterAttributes, Is.EqualTo(custom));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void TryGet_DistinguishesUnavailableFromValidDefaultState()
        {
            var unavailableRepository = new StubRepository(null, LocalProfilePersistenceStatus.Unavailable);
            var unavailableStore = new LocalProfileStore(unavailableRepository, Profile);

            Assert.That(unavailableStore.TryGetCharacterAttributeState(out CharacterAttributeState unavailable), Is.False);
            Assert.That(unavailable, Is.EqualTo(default(CharacterAttributeState)));

            var availableRepository = new StubRepository(
                new LocalProfileSnapshot { ProfileId = Profile, CharacterAttributes = default },
                LocalProfilePersistenceStatus.Ready);
            var availableStore = new LocalProfileStore(availableRepository, Profile);

            Assert.That(availableStore.TryGetCharacterAttributeState(out CharacterAttributeState available), Is.True);
            Assert.That(available, Is.EqualTo(default(CharacterAttributeState)));
        }

        [Test]
        public void Assignment_CommitsOnlySelectedAttributeAndOneAvailablePoint()
        {
            var repository = ReadyRepository();
            var store = new LocalProfileStore(repository, Profile);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            CharacterAttributeAssignmentCommitResult result = store.TryAssignCharacterAttribute(
                CharacterAttribute.Dexterity,
                out CharacterAttributeAssignmentFailure failure);

            Assert.That(result, Is.EqualTo(CharacterAttributeAssignmentCommitResult.Success));
            Assert.That(failure, Is.EqualTo(CharacterAttributeAssignmentFailure.None));
            Assert.That(store.TryGetCharacterAttributeState(out CharacterAttributeState state), Is.True);
            Assert.That(state.Dexterity, Is.EqualTo(6));
            Assert.That(state.Vitality, Is.EqualTo(5));
            Assert.That(state.Resistance, Is.EqualTo(5));
            Assert.That(state.Strength, Is.EqualTo(5));
            Assert.That(state.Intelligence, Is.EqualTo(5));
            Assert.That(state.Luck, Is.EqualTo(5));
            Assert.That(state.AvailablePoints, Is.EqualTo(9));
            Assert.That(commits, Is.EqualTo(1));
        }

        [Test]
        public void RuleRejection_PreservesSnapshotAndDoesNotPublishCommit()
        {
            Assert.That(CharacterAttributeState.TryCreate(
                26, 5, 5, 5, 5, 5, 10, out CharacterAttributeState aboveMaximum), Is.True);
            var repository = ReadyRepository(aboveMaximum);
            var store = new LocalProfileStore(repository, Profile);
            LocalProfileSnapshot before = repository.Snapshot;
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            CharacterAttributeAssignmentCommitResult result = store.TryAssignCharacterAttribute(
                CharacterAttribute.Vitality,
                out CharacterAttributeAssignmentFailure failure);

            Assert.That(result, Is.EqualTo(CharacterAttributeAssignmentCommitResult.Rejected));
            Assert.That(failure, Is.EqualTo(CharacterAttributeAssignmentFailure.AttributeAtMaximum));
            Assert.That(repository.Snapshot, Is.SameAs(before));
            Assert.That(repository.Snapshot.CharacterAttributes, Is.EqualTo(aboveMaximum));
            Assert.That(commits, Is.Zero);
        }

        [Test]
        public void RepositoryFailure_PreservesObservableSnapshotAndDoesNotPublishCommit()
        {
            var repository = ReadyRepository();
            repository.FailSaves = true;
            var store = new LocalProfileStore(repository, Profile);
            LocalProfileSnapshot before = repository.Snapshot;
            int commits = 0;
            store.ProfileCommitted += _ => commits++;
            LogAssert.Expect(LogType.Error, new Regex("\\[LocalProfileStore\\] Commit failed\\."));

            CharacterAttributeAssignmentCommitResult result = store.TryAssignCharacterAttribute(
                CharacterAttribute.Luck,
                out CharacterAttributeAssignmentFailure failure);

            Assert.That(result, Is.EqualTo(CharacterAttributeAssignmentCommitResult.PersistenceFailed));
            Assert.That(failure, Is.EqualTo(CharacterAttributeAssignmentFailure.None));
            Assert.That(repository.Snapshot, Is.SameAs(before));
            Assert.That(repository.Snapshot.CharacterAttributes.Luck, Is.EqualTo(5));
            Assert.That(commits, Is.Zero);
        }

        [TestCase(99, 1, 99, 10)]
        [TestCase(100, 2, 0, 11)]
        public void ProgressionCommit_GrantsPointsOnlyForLevelsGained(
            long rewardExperience,
            int expectedLevel,
            long expectedExperience,
            int expectedAvailablePoints)
        {
            var repository = ReadyRepository();
            var store = new LocalProfileStore(repository, Profile);
            Assert.That(TryCreateResolution(rewardExperience, out ExpeditionExperienceResolution resolution), Is.True);
            ProgressionReceipt receipt = CreateReceipt(repository.Snapshot, $"raid-{rewardExperience}", 1, resolution);

            Assert.That(store.TryCommitProgression(receipt, resolution), Is.EqualTo(ProgressionCommitResult.Success));
            Assert.That(repository.Snapshot.Level, Is.EqualTo(expectedLevel));
            Assert.That(repository.Snapshot.CurrentExperience, Is.EqualTo(expectedExperience));
            Assert.That(repository.Snapshot.CharacterAttributes.AvailablePoints, Is.EqualTo(expectedAvailablePoints));
        }

        [Test]
        public void ProgressionCommit_AtomicallyGrantsAllLevelPointsAndReplayDoesNotGrantAgain()
        {
            var repository = ReadyRepository();
            var store = new LocalProfileStore(repository, Profile);
            Assert.That(TryCreateResolution(320, out ExpeditionExperienceResolution resolution), Is.True);
            ProgressionReceipt receipt = CreateReceipt(repository.Snapshot, "raid-attributes", 1, resolution);
            int commits = 0;
            store.ProfileCommitted += _ => commits++;

            Assert.That(store.TryCommitProgression(receipt, resolution), Is.EqualTo(ProgressionCommitResult.Success));
            Assert.That(repository.Snapshot.Level, Is.EqualTo(4));
            Assert.That(repository.Snapshot.CurrentExperience, Is.EqualTo(5));
            Assert.That(repository.Snapshot.CharacterAttributes.AvailablePoints, Is.EqualTo(13));
            Assert.That(repository.Snapshot.LastAppliedProgressionResultSequence, Is.EqualTo(1));
            Assert.That(repository.Snapshot.LastProgressionReceipt, Is.EqualTo(receipt));
            Assert.That(commits, Is.EqualTo(1));

            Assert.That(store.TryCommitProgression(receipt, resolution), Is.EqualTo(ProgressionCommitResult.AlreadyApplied));
            Assert.That(repository.Snapshot.CharacterAttributes.AvailablePoints, Is.EqualTo(13));
            Assert.That(commits, Is.EqualTo(1));
        }

        [Test]
        public void FailedProgressionCommit_PreservesLevelExperiencePointsAndWatermark()
        {
            var repository = ReadyRepository();
            repository.FailSaves = true;
            var store = new LocalProfileStore(repository, Profile);
            Assert.That(TryCreateResolution(320, out ExpeditionExperienceResolution resolution), Is.True);
            ProgressionReceipt receipt = CreateReceipt(repository.Snapshot, "raid-failed-attributes", 1, resolution);
            LogAssert.Expect(LogType.Error, new Regex("\\[LocalProfileStore\\] Commit failed\\."));

            Assert.That(store.TryCommitProgression(receipt, resolution), Is.EqualTo(ProgressionCommitResult.PersistenceFailed));
            Assert.That(repository.Snapshot.Level, Is.EqualTo(1));
            Assert.That(repository.Snapshot.CurrentExperience, Is.Zero);
            Assert.That(repository.Snapshot.CharacterAttributes.AvailablePoints, Is.EqualTo(10));
            Assert.That(repository.Snapshot.LastAppliedProgressionResultSequence, Is.Zero);
            Assert.That(repository.Snapshot.LastProgressionReceipt, Is.Null);
        }

        [Test]
        public void RecreatedConsumer_ObservesSameProcessAggregate()
        {
            var repository = ReadyRepository();
            var firstStore = new LocalProfileStore(repository, Profile);
            Assert.That(
                firstStore.TryAssignCharacterAttribute(CharacterAttribute.Strength, out _),
                Is.EqualTo(CharacterAttributeAssignmentCommitResult.Success));

            var recreatedStore = new LocalProfileStore(repository, Profile);

            Assert.That(recreatedStore.TryGetCharacterAttributeState(out CharacterAttributeState state), Is.True);
            Assert.That(state.Strength, Is.EqualTo(6));
            Assert.That(state.AvailablePoints, Is.EqualTo(9));
        }

        private static StubRepository ReadyRepository(CharacterAttributeState? attributes = null)
        {
            var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
            if (attributes.HasValue)
            {
                snapshot.CharacterAttributes = attributes.Value;
            }
            return new StubRepository(snapshot, LocalProfilePersistenceStatus.Ready);
        }

        private static bool TryCreateResolution(
            long experience,
            out ExpeditionExperienceResolution resolution)
        {
            resolution = default;
            return ExpeditionExperienceRules.TryApplyNormalReward(
                    default,
                    ExpeditionExperienceCategory.Kill,
                    experience,
                    out ExpeditionExperienceSnapshot snapshot,
                    out _) &&
                ExpeditionExperienceResolutionRules.TryResolve(
                    default,
                    snapshot,
                    ExpeditionExperienceResolutionOutcome.Extracted,
                    ProgressionBalanceDefaults.InitialExpeditionExperienceRetentionPolicy,
                    out resolution,
                    out _);
        }

        private static ProgressionReceipt CreateReceipt(
            LocalProfileSnapshot current,
            string raidId,
            int sequence,
            in ExpeditionExperienceResolution resolution)
        {
            Assert.That(ConsolidatedExperienceApplicationRules.TryApply(
                ProgressionBalanceDefaults.InitialExperienceCurve,
                default,
                current.Level,
                current.CurrentExperience,
                resolution,
                out ConsolidatedExperienceApplication application,
                out _), Is.True);
            return new ProgressionReceipt(
                raidId,
                Profile,
                sequence,
                resolution.ConsolidatedExperience,
                application.Result.ResultingLevel);
        }

        private sealed class StubRepository : ILocalProfileRepository
        {
            public StubRepository(LocalProfileSnapshot snapshot, LocalProfilePersistenceStatus status)
            {
                Snapshot = snapshot;
                Status = status;
            }

            public bool FailSaves { get; set; }
            public LocalProfilePersistenceStatus Status { get; private set; }
            public string LastError { get; private set; }
            public LocalProfileSnapshot Snapshot { get; private set; }

            public bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog) => false;

            public bool TrySave(LocalProfileSnapshot snapshot, out string error)
            {
                if (FailSaves)
                {
                    error = "Simulated repository failure.";
                    LastError = error;
                    return false;
                }

                Snapshot = snapshot.Clone();
                error = null;
                LastError = null;
                return true;
            }
        }
    }
}
#endif
