using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Abilities
{
    public sealed class AbilityEnemyTargetMarkerTests
    {
        private static readonly EntityId CasterId = new EntityId(1);

        [Test]
        public void EnemyCharacter_ImplementsTheEnemyTargetMarker()
        {
            Assert.That(typeof(IAbilityEnemyTarget).IsAssignableFrom(typeof(EnemyCharacter)), Is.True);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        public void TrainingDummy_ImplementsTheEnemyTargetMarker()
        {
            Assert.That(typeof(IAbilityEnemyTarget).IsAssignableFrom(typeof(TrainingDummyCharacter)), Is.True);
        }

        [Test]
        public void TrainingDummy_AlwaysAcceptsDamageRequests()
        {
            var go = new GameObject("Dummy");
            try
            {
                var dummy = go.AddComponent<TrainingDummyCharacter>();
                Assert.That(dummy.CanReceiveDamage, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
#endif

        [Test]
        public void Predicate_AcceptsMarkedLivingDamageable()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new MarkedTarget(2)), Is.True);
        }

        [Test]
        public void Predicate_RejectsUnmarkedDamageable()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new UnmarkedTarget(2)), Is.False);
        }

        [Test]
        public void Predicate_RejectsMarkedCaster()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new MarkedTarget(1)), Is.False);
        }

        [Test]
        public void Predicate_RejectsMarkedDeadTarget()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new MarkedTarget(2) { IsAlive = false }), Is.False);
        }

        [Test]
        public void Predicate_RejectsMarkedTargetThatCannotReceiveDamage()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new MarkedTarget(2) { CanReceiveDamage = false }), Is.False);
        }

        [Test]
        public void Predicate_RejectsNull()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, null), Is.False);
        }

        private class UnmarkedTarget : IDamageable, ICharacter
        {
            public UnmarkedTarget(int id) { Id = new EntityId(id); }
            public EntityId Id { get; }
            public bool IsAlive { get; set; } = true;
            public bool CanReceiveDamage { get; set; } = true;

            public DamageResult ApplyDamage(in DamageRequest request) =>
                new DamageResult(Id, true, request.Amount, 100f, false, DamageFailureReason.None);
        }

        private sealed class MarkedTarget : UnmarkedTarget, IAbilityEnemyTarget
        {
            public MarkedTarget(int id) : base(id) { }
        }
    }
}
