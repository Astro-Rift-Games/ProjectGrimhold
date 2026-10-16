using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Scenario
{
    public sealed class ExtractionProgressRegistryTests
    {
        private GameObject _root;
        private EntityRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Extraction progress registry tests");
            _registry = _root.AddComponent<EntityRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void IndependentCapabilities_AreIdempotentAndRejectConflicts()
        {
            var id = new EntityId(41);
            var receiver = new Receiver(id);
            var conflictingReceiver = new Receiver(id);
            var reader = new Reader(id);
            var defeatSource = new DefeatSource(id, 10);

            Assert.That(_registry.TryRegisterExtractionProgressReceiver(id, receiver), Is.True);
            Assert.That(_registry.TryRegisterExtractionProgressReceiver(id, receiver), Is.True);
            Assert.That(_registry.TryRegisterExtractionProgressReceiver(id, conflictingReceiver), Is.False);
            Assert.That(_registry.TryRegisterExtractionProgressDefeatSource(id, defeatSource), Is.True);
            Assert.That(_registry.TryRegisterExtractionProgressReader(id, reader), Is.True);
            Assert.That(_registry.TryGetExtractionProgressReceiver(id, out var resolvedReceiver), Is.True);
            Assert.That(_registry.TryGetExtractionProgressDefeatSource(id, out var resolvedSource), Is.True);
            Assert.That(resolvedReceiver, Is.SameAs(receiver));
            Assert.That(resolvedSource, Is.SameAs(defeatSource));
        }

        [Test]
        public void Unregistration_RequiresExpectedInstanceAndPreservesOtherCapability()
        {
            var id = new EntityId(42);
            var receiver = new Receiver(id);
            var otherReceiver = new Receiver(id);
            var source = new DefeatSource(id, 15);
            _registry.TryRegisterExtractionProgressReceiver(id, receiver);
            _registry.TryRegisterExtractionProgressDefeatSource(id, source);

            Assert.That(_registry.TryUnregisterExtractionProgressReceiver(id, otherReceiver), Is.False);
            Assert.That(_registry.TryUnregisterExtractionProgressReceiver(id, receiver), Is.True);
            Assert.That(_registry.TryGetExtractionProgressReceiver(id, out _), Is.False);
            Assert.That(_registry.TryGetExtractionProgressDefeatSource(id, out _), Is.True);
        }

        [Test]
        public void TeamExtractionProgress_RegistersAndResolvesSingletAndById()
        {
            var id = new EntityId(43);
            var teamProgress = new MockTeamProgress(id);
            var conflictingProgress = new MockTeamProgress(id);

            Assert.That(_registry.TryRegisterTeamExtractionProgress(id, teamProgress), Is.True);
            Assert.That(_registry.TryRegisterTeamExtractionProgress(id, teamProgress), Is.True); // Idempotent
            Assert.That(_registry.TryRegisterTeamExtractionProgress(id, conflictingProgress), Is.False); // Reject conflict

            Assert.That(_registry.TryGetTeamExtractionProgress(id, out ITeamExtractionProgress resolvedById), Is.True);
            Assert.That(resolvedById, Is.SameAs(teamProgress));

            Assert.That(_registry.TryGetTeamExtractionProgress(out ITeamExtractionProgress resolvedSinglet), Is.True);
            Assert.That(resolvedSinglet, Is.SameAs(teamProgress));

            // Unregister with wrong instance fails
            Assert.That(_registry.TryUnregisterTeamExtractionProgress(id, conflictingProgress), Is.False);
            Assert.That(_registry.TryGetTeamExtractionProgress(id, out _), Is.True);

            // Unregister with correct instance succeeds
            Assert.That(_registry.TryUnregisterTeamExtractionProgress(id, teamProgress), Is.True);
            Assert.That(_registry.TryGetTeamExtractionProgress(id, out _), Is.False);
            Assert.That(_registry.TryGetTeamExtractionProgress(out _), Is.False);
        }

        private sealed class MockTeamProgress : ITeamExtractionProgress
        {
            public MockTeamProgress(EntityId id) => Id = id;
            public EntityId Id { get; }
            public bool TryGetProgress(RaidTeamId teamId, out int currentProgress, out int quota)
            {
                currentProgress = 0;
                quota = 100;
                return true;
            }
            public bool TryGetSnapshot(RaidTeamId teamId, out ExtractionProgressSnapshot snapshot)
            {
                snapshot = new ExtractionProgressSnapshot(0, 100, false);
                return true;
            }
            public bool TryApplyContribution(RaidTeamId teamId, long amount, out int nextProgress, out bool completedQuota)
            {
                nextProgress = (int)amount;
                completedQuota = amount >= 100;
                return true;
            }
            public bool TryInitializeFromRoster(RaidTeamRoster roster) => true;
            public bool IsAssignedToTeam(RaidTeamId teamId) => true;
        }

        private sealed class Receiver : IExtractionProgressReceiver
        {
            public Receiver(EntityId id) => Id = id;
            public EntityId Id { get; }
            public bool TryApplyContribution(in ExtractionProgressContribution contribution) => true;
        }

        private sealed class DefeatSource : IExtractionProgressDefeatSource
        {
            public DefeatSource(EntityId id, int reward)
            {
                Id = id;
                DefeatProgressReward = reward;
            }

            public EntityId Id { get; }
            public int DefeatProgressReward { get; }
        }

        private sealed class Reader : IExtractionProgressReader
        {
            public Reader(EntityId id) => Id = id;
            public EntityId Id { get; }
            public bool TryGetSnapshot(out ExtractionProgressSnapshot snapshot)
            {
                snapshot = new ExtractionProgressSnapshot(100, 100, true);
                return true;
            }
        }
    }
}
