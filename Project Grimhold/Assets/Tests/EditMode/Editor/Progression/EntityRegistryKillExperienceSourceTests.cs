using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Progression
{
    public sealed class EntityRegistryKillExperienceSourceTests
    {
        private GameObject _registryObject;
        private GameObject _entityObject;
        private GameObject _otherObject;
        private EntityRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registryObject = new GameObject("Registry");
            _entityObject = new GameObject("Entity");
            _otherObject = new GameObject("Other");
            _registry = _registryObject.AddComponent<EntityRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_otherObject);
            Object.DestroyImmediate(_entityObject);
            Object.DestroyImmediate(_registryObject);
        }

        [Test]
        public void Registration_IsIdempotentForOwnerAndRejectsConflicts()
        {
            var id = new EntityId(101);
            Source owner = CreateSource(_entityObject, id);
            Source conflict = CreateSource(_otherObject, id);

            Assert.That(_registry.TryRegisterKillExperienceSource(id, owner), Is.True);
            Assert.That(_registry.TryRegisterKillExperienceSource(id, owner), Is.True);
            Assert.That(_registry.TryRegisterKillExperienceSource(id, conflict), Is.False);
            Assert.That(_registry.TryGetKillExperienceSource(id, out IKillExperienceSource resolved), Is.True);
            Assert.That(resolved, Is.SameAs(owner));
            Assert.That(_registry.TryUnregisterKillExperienceSource(id, conflict), Is.False);
            Assert.That(_registry.TryUnregisterKillExperienceSource(id, owner), Is.True);
            Assert.That(_registry.TryGetKillExperienceSource(id, out _), Is.False);
        }

        [Test]
        public void Capability_PreservesSharedColliderMappingUntilLastOwnerLeaves()
        {
            var id = new EntityId(102);
            Damageable damageable = _entityObject.AddComponent<Damageable>();
            damageable.IdValue = id.Value;
            Source source = CreateSource(_otherObject, id);
            Collider2D collider = _entityObject.AddComponent<BoxCollider2D>();

            Assert.That(_registry.TryRegisterEntity(id, damageable, new[] { collider }), Is.True);
            Assert.That(_registry.TryRegisterKillExperienceSource(id, source), Is.True);
            Assert.That(_registry.TryUnregisterEntity(id, damageable), Is.True);
            Assert.That(_registry.TryGetEntityId(collider, out EntityId mapped), Is.True);
            Assert.That(mapped, Is.EqualTo(id));

            Assert.That(_registry.TryUnregisterKillExperienceSource(id, source), Is.True);
            Assert.That(_registry.TryGetEntityId(collider, out _), Is.False);
        }

        [Test]
        public void KillAndExtractionSources_AreIndependentAndClearTogether()
        {
            var id = new EntityId(103);
            Source killSource = CreateSource(_entityObject, id);
            ExtractionSource extractionSource = _otherObject.AddComponent<ExtractionSource>();
            extractionSource.IdValue = id.Value;

            Assert.That(_registry.TryRegisterKillExperienceSource(id, killSource), Is.True);
            Assert.That(
                _registry.TryRegisterExtractionProgressDefeatSource(id, extractionSource),
                Is.True);
            Assert.That(_registry.TryGetKillExperienceSource(id, out _), Is.True);
            Assert.That(_registry.TryGetExtractionProgressDefeatSource(id, out _), Is.True);

            Assert.That(_registry.TryUnregisterExtractionProgressDefeatSource(id, extractionSource), Is.True);
            Assert.That(_registry.TryGetKillExperienceSource(id, out _), Is.True);

            Assert.That(
                _registry.TryRegisterExtractionProgressDefeatSource(id, extractionSource),
                Is.True);
            _registry.ClearForRaidClosure();
            Assert.That(_registry.TryGetKillExperienceSource(id, out _), Is.False);
            Assert.That(_registry.TryGetExtractionProgressDefeatSource(id, out _), Is.False);
        }

        private static Source CreateSource(GameObject gameObject, EntityId id)
        {
            Source source = gameObject.AddComponent<Source>();
            source.IdValue = id.Value;
            return source;
        }

        private sealed class Source : MonoBehaviour, IKillExperienceSource
        {
            public int IdValue { get; set; }
            public EntityId Id => new(IdValue);
            public long KillExperience => 10;
            public bool IsAvailable => true;
            public bool TryGrantTo(PlayerExpeditionExperienceLedger ledger) => false;
        }

        private sealed class ExtractionSource : MonoBehaviour, IExtractionProgressDefeatSource
        {
            public int IdValue { get; set; }
            public EntityId Id => new(IdValue);
            public int DefeatProgressReward => 99;
        }

        private sealed class Damageable : MonoBehaviour, IDamageable
        {
            public int IdValue { get; set; }
            public EntityId Id => new(IdValue);
            public bool CanReceiveDamage => true;
            public DamageResult ApplyDamage(in DamageRequest request) => default;
        }
    }
}
