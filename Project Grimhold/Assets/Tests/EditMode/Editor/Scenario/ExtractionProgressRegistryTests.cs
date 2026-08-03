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
