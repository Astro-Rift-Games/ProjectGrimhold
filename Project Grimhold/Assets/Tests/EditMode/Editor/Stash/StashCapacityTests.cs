#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Stash
{
    public sealed class StashCapacityTests
    {
        private readonly ProfileId _profile = new("49494949494949494949494949494949");
        private LootDefinitionCatalog _catalog;
        private LootDefinition[] _definitions;
        private LootId[] _ids;
        private InMemoryLocalProfileRepository _repository;
        private LocalProfileStore _store;
        private int _commits;

        [SetUp]
        public void SetUp()
        {
            _definitions = Enumerable.Range(0, 70).Select(i =>
            {
                var definition = ScriptableObject.CreateInstance<LootDefinition>();
                typeof(LootDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(definition, $"stash_capacity_{i}");
                return definition;
            }).ToArray();
            _catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            typeof(LootDefinitionCatalog).GetField("_definitions", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_catalog, new System.Collections.Generic.List<LootDefinition>(_definitions));
            _ids = _definitions.Select(d => d.LootId).ToArray();
            Assert.That(_catalog.DefinitionCount, Is.EqualTo(70));
        }

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null) UnityEngine.Object.DestroyImmediate(_catalog);
            if (_definitions != null)
                foreach (LootDefinition definition in _definitions) UnityEngine.Object.DestroyImmediate(definition);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullStash_SingleAndAllTransfers_RejectAtomically(bool all)
        {
            Create(60);
            string before = LocalProfileSaveCodec.Encode(_repository.Snapshot);
            Assert.That(all ? _store.EvaluateTransferAllToStash() : _store.EvaluateTransferToStash(_ids[69], 1),
                Is.Not.EqualTo(StashOperationResult.Success));
            Assert.That(all ? _store.TryTransferAllToStash() : _store.TryTransferToStash(_ids[69], 1),
                Is.Not.EqualTo(StashOperationResult.Success));
            AssertUnchanged(before);
        }

        [TestCase(60)]
        [TestCase(65)]
        public void AtOrAboveCapacity_ExistingIdMergeSucceedsWithoutQuantityCap(int count)
        {
            Create(count);
            Assert.That(_store.TrySecureLoot(new[] { new StashItem(_ids[0], 1000000) }),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(_store.GetStash(), Has.Count.EqualTo(count));
            Assert.That(_store.GetStash().Single(x => x.LootId == _ids[0]).Amount, Is.EqualTo(1000001));
            string before = LocalProfileSaveCodec.Encode(_repository.Snapshot);
            Assert.That(_store.TrySecureLoot(new[] { new StashItem(_ids[69], 1) }),
                Is.Not.EqualTo(StashOperationResult.Success));
            AssertUnchanged(before, 1);
        }

        [Test]
        public void HydratedExcess_CodecRoundTripPreservesEveryAcceptedId()
        {
            Create(65);
            string encoded = LocalProfileSaveCodec.Encode(_repository.Snapshot);
            Assert.That(LocalProfileSaveCodec.TryDecode(encoded, _profile, _catalog, out var decoded, out _, out string error),
                Is.True, error);
            CollectionAssert.AreEqual(_repository.Snapshot.Stash, decoded.Stash);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverCapacity_TransferExistingId_DoesNotRequireANewSlot(bool all)
        {
            Create(65);
            var snapshot = _repository.Snapshot.Clone();
            snapshot.Loadout.Clear();
            snapshot.Loadout.Add(new StashItem(_ids[0], 5));
            Assert.That(_repository.TrySave(snapshot, out _), Is.True);
            Assert.That(all ? _store.EvaluateTransferAllToStash() : _store.EvaluateTransferToStash(_ids[0], 5),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(all ? _store.TryTransferAllToStash() : _store.TryTransferToStash(_ids[0], 5),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(_store.GetStash(), Has.Count.EqualTo(65));
            Assert.That(_store.GetLoadout(), Is.Empty);
        }

        [Test]
        public void SecureLoot_PackageExceedingCapacity_RejectsExistingAndNewItemsAtomically()
        {
            Create(59);
            string before = LocalProfileSaveCodec.Encode(_repository.Snapshot);
            Assert.That(_store.TrySecureLoot(new[] {
                new StashItem(_ids[0], 5), new StashItem(_ids[60], 1), new StashItem(_ids[61], 1) }),
                Is.Not.EqualTo(StashOperationResult.Success));
            AssertUnchanged(before);
        }

        [Test]
        public void SecureLoot_DuplicateIncomingIds_RejectsInvalidPackageAtomically()
        {
            Create(59);
            string before = LocalProfileSaveCodec.Encode(_repository.Snapshot);
            Assert.That(_store.TrySecureLoot(new[] { new StashItem(_ids[60], 2), new StashItem(_ids[60], 3) }),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            AssertUnchanged(before);
        }

        [Test]
        public void SecureLoot_IntegerOverflow_RejectsWholePackage()
        {
            Create(60);
            string before = LocalProfileSaveCodec.Encode(_repository.Snapshot);
            Assert.That(_store.TrySecureLoot(new[] { new StashItem(_ids[1], 2), new StashItem(_ids[0], int.MaxValue) }),
                Is.Not.EqualTo(StashOperationResult.Success));
            AssertUnchanged(before);
        }

        [Test]
        public void MissionClaim_FullStash_LeavesEntireRewardPackageAndPendingStateUnchanged()
        {
            Create(60);
            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            try
            {
                typeof(MissionDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(mission, "stash_capacity_claim");
                mission.Rewards = new System.Collections.Generic.List<RewardDefinition> {
                    new() { Type = RewardDefinition.RewardType.Gold, Amount = 100 },
                    new() { Type = RewardDefinition.RewardType.Experience, Amount = 50 },
                    new() { Type = RewardDefinition.RewardType.Item, ReferenceId = _ids[69].Value, Amount = 2 } };
                var snapshot = _repository.Snapshot.Clone();
                snapshot.ActiveMissions.Add(new MissionInstanceState(mission.MissionId, MissionState.PendienteDeReclamar));
                Assert.That(_repository.TrySave(snapshot, out _), Is.True);
                string before = LocalProfileSaveCodec.Encode(_repository.Snapshot);
                Assert.That(_store.TryClaimMission(mission), Is.Not.EqualTo(StashOperationResult.Success));
                AssertUnchanged(before); // Compares the entire encoded aggregate, including progression receipts and mission state.
            }
            finally { UnityEngine.Object.DestroyImmediate(mission); }
        }

        [Test]
        public void MissionClaim_RepeatedRewardId_ConsumesOneRemainingSlot()
        {
            Create(59);
            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            try
            {
                typeof(MissionDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(mission, "stash_capacity_duplicate_claim");
                mission.Rewards = new System.Collections.Generic.List<RewardDefinition> {
                    new() { Type = RewardDefinition.RewardType.Item, ReferenceId = _ids[69].Value, Amount = 2 },
                    new() { Type = RewardDefinition.RewardType.Item, ReferenceId = _ids[69].Value, Amount = 3 } };
                var snapshot = _repository.Snapshot.Clone();
                snapshot.ActiveMissions.Add(new MissionInstanceState(mission.MissionId, MissionState.PendienteDeReclamar));
                Assert.That(_repository.TrySave(snapshot, out _), Is.True);
                Assert.That(_store.TryClaimMission(mission), Is.EqualTo(StashOperationResult.Success));
                Assert.That(_store.GetStash(), Has.Count.EqualTo(60));
                Assert.That(_store.GetStash().Single(x => x.LootId == _ids[69]).Amount, Is.EqualTo(5));
            }
            finally { UnityEngine.Object.DestroyImmediate(mission); }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_NonPositiveCapacity_Throws(int capacity)
        {
            Create(0);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalProfileStore(_repository, _profile, _catalog, stashCapacity: capacity));
        }

        [Test]
        public void ConfigurationCapacity_ComposesIntoStoreWithoutChangingSnapshotSchema()
        {
            Create(2);
            var config = ScriptableObject.CreateInstance<LocalProfilePersistenceConfiguration>();
            try
            {
                Assert.That(config.StashCapacity, Is.EqualTo(60));
                typeof(LocalProfilePersistenceConfiguration).GetField("_stashCapacity", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(config, 2);
                var store = new LocalProfileStore(_repository, _profile, _catalog, stashCapacity: config.StashCapacity);
                Assert.That(store.StashCapacity, Is.EqualTo(2));
                Assert.That(store.EvaluateTransferToStash(_ids[69], 1), Is.EqualTo(StashOperationResult.InventoryFull));
                Assert.That(store.TrySecureLoot(new[] { new StashItem(_ids[0], 2) }), Is.EqualTo(StashOperationResult.Success));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private void Create(int count)
        {
            _repository = new InMemoryLocalProfileRepository();
            Assert.That(_repository.Initialize(_profile, _catalog), Is.True);
            _store = new LocalProfileStore(_repository, _profile, _catalog);
            var inventory = new InventoryData { revision = 2, preparedEquipment = new PreparedEquipmentData(),
                stash = _ids.Take(count).Select(id => new InventoryItemData { lootId = id.Value, amount = 1 }).ToArray(),
                loadout = new[] { new InventoryItemData { lootId = _ids[69].Value, amount = 1 } } };
            Assert.That(_store.ReconcileRemoteState(inventory, null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_repository.Snapshot.Stash, Has.Count.EqualTo(count));
            _commits = 0;
            _store.ProfileCommitted += _ => _commits++;
        }

        private void AssertUnchanged(string before, int commits = 0)
        {
            Assert.That(LocalProfileSaveCodec.Encode(_repository.Snapshot), Is.EqualTo(before));
            Assert.That(_commits, Is.EqualTo(commits));
        }
    }
}
#endif
