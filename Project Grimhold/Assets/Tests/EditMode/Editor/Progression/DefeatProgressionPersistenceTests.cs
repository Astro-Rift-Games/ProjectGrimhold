using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Progression
{
    public sealed class DefeatProgressionPersistenceTests
    {
        private LootDefinitionCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(_catalog, Is.Not.Null);
        }

        [Test]
        public void TryCommitDefeatProgression_AppliesProgression_AndAdvancesSequence()
        {
            var profileId = new ProfileId("test_profile_defeat");
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profileId, _catalog), Is.True);

            var snapshot = new LocalProfileSnapshot
            {
                ProfileId = profileId,
                Level = 1,
                CurrentExperience = 0,
                LastAppliedProgressionResultSequence = 0,
                LastProgressionReceipt = null,
                PendingReservation = new PendingLoadoutReservation("res_1", null)
            };
            Assert.That(repository.TrySave(snapshot, out string initError), Is.True, initError);

            var store = new LocalProfileStore(repository, profileId, _catalog);

            var receipt = new ProgressionReceipt("raid_defeat_1", profileId, 1, 250, 2);
            var result = store.TryCommitDefeatProgression(receipt, resultingLevel: 2, resultingExperience: 100);

            Assert.That(result, Is.EqualTo(StashOperationResult.Success));

            var saved = repository.Snapshot;
            Assert.That(saved.Level, Is.EqualTo(2));
            Assert.That(saved.CurrentExperience, Is.EqualTo(100));
            Assert.That(saved.LastAppliedProgressionResultSequence, Is.EqualTo(1));
            Assert.That(saved.LastProgressionReceipt.HasValue, Is.True);
            Assert.That(saved.LastProgressionReceipt.Value.RaidId, Is.EqualTo("raid_defeat_1"));
            Assert.That(saved.LastProgressionReceipt.Value.ResultSequence, Is.EqualTo(1));
            Assert.That(saved.LastProgressionReceipt.Value.ConsolidatedExperience, Is.EqualTo(250));
            Assert.That(saved.AppliedProgressionReceipts, Has.Count.EqualTo(1));
            Assert.That(saved.AppliedProgressionReceipts[0], Is.EqualTo(saved.LastProgressionReceipt.Value));
            Assert.That(saved.PendingReservation, Is.Null);
        }

        [Test]
        public void TryCommitDefeatProgression_IdempotentOnRepeatedReceipt()
        {
            var profileId = new ProfileId("test_profile_defeat_idempotent");
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profileId, _catalog), Is.True);

            var snapshot = new LocalProfileSnapshot
            {
                ProfileId = profileId,
                Level = 1,
                CurrentExperience = 0,
                LastAppliedProgressionResultSequence = 0
            };
            Assert.That(repository.TrySave(snapshot, out string initError), Is.True, initError);

            var store = new LocalProfileStore(repository, profileId, _catalog);

            var receipt = new ProgressionReceipt("raid_defeat_idem", profileId, 1, 300, 2);
            var result1 = store.TryCommitDefeatProgression(receipt, resultingLevel: 2, resultingExperience: 150);
            Assert.That(result1, Is.EqualTo(StashOperationResult.Success));

            // Replay identical receipt
            var result2 = store.TryCommitDefeatProgression(receipt, resultingLevel: 2, resultingExperience: 150);
            Assert.That(result2, Is.EqualTo(StashOperationResult.Success));

            var saved = repository.Snapshot;
            Assert.That(saved.LastAppliedProgressionResultSequence, Is.EqualTo(1));
            Assert.That(saved.AppliedProgressionReceipts, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryCommitDefeatProgression_RejectsConflictingSequenceForSameRaid()
        {
            var profileId = new ProfileId("test_profile_defeat_conflict");
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profileId, _catalog), Is.True);

            var snapshot = new LocalProfileSnapshot
            {
                ProfileId = profileId,
                Level = 1,
                CurrentExperience = 0,
                LastAppliedProgressionResultSequence = 0
            };
            Assert.That(repository.TrySave(snapshot, out string initError), Is.True, initError);

            var store = new LocalProfileStore(repository, profileId, _catalog);

            var receipt1 = new ProgressionReceipt("raid_defeat_conflict", profileId, 1, 200, 1);
            Assert.That(store.TryCommitDefeatProgression(receipt1, resultingLevel: 1, resultingExperience: 200),
                Is.EqualTo(StashOperationResult.Success));

            // Same raid with different sequence
            var receipt2 = new ProgressionReceipt("raid_defeat_conflict", profileId, 2, 400, 2);
            var result2 = store.TryCommitDefeatProgression(receipt2, resultingLevel: 2, resultingExperience: 400);

            Assert.That(result2, Is.EqualTo(StashOperationResult.InvalidInventory));
        }

        [Test]
        public void TryCommitDefeatProgression_RejectsMismatchedProfileId()
        {
            var profileId = new ProfileId("test_profile_owner");
            var wrongProfileId = new ProfileId("test_profile_impostor");
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profileId, _catalog), Is.True);

            var snapshot = new LocalProfileSnapshot { ProfileId = profileId };
            Assert.That(repository.TrySave(snapshot, out string initError), Is.True, initError);

            var store = new LocalProfileStore(repository, profileId, _catalog);

            var receipt = new ProgressionReceipt("raid_mismatch", wrongProfileId, 1, 100, 1);
            var result = store.TryCommitDefeatProgression(receipt, resultingLevel: 1, resultingExperience: 100);

            Assert.That(result, Is.EqualTo(StashOperationResult.InvalidInventory));
        }
    }
}
