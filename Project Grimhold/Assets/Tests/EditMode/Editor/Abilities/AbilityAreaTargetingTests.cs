using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Abilities
{
    public sealed class AbilityAreaTargetingTests
    {
        private const int CasterIdValue = 1;
        private static readonly EntityId CasterId = new EntityId(CasterIdValue);

        private readonly List<GameObject> _objects = new();
        private EntityRegistry _registry;
        private Physics2DAttackTargetQuery _query;
        private int _layer;
        private List<AttackTarget> _results;

        [SetUp]
        public void SetUp()
        {
            _layer = LayerMask.NameToLayer("Default");
            _results = new List<AttackTarget>();
            var registryHolder = new GameObject("Registry");
            _registry = registryHolder.AddComponent<EntityRegistry>();
            _objects.Add(registryHolder);

            var queryHolder = new GameObject("Query");
            _query = queryHolder.AddComponent<Physics2DAttackTargetQuery>();
            _objects.Add(queryHolder);
            Inject(_query, "_registry", _registry);
            Inject(_query, "_colliderBuffer", new Collider2D[64]);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        // ---- Predicate -------------------------------------------------------------------

        [Test]
        public void Predicate_ExcludesTheCaster()
        {
            var candidate = CreateDummy(CasterIdValue, Vector2.zero);
            Assert.That(AbilityTargetPredicate.IsValidCombatant(CasterId, candidate), Is.False);
        }

        [Test]
        public void Predicate_ExcludesDeadCandidates()
        {
            var candidate = CreateDummy(2, Vector2.zero);
            candidate.IsAlive = false;
            Assert.That(AbilityTargetPredicate.IsValidCombatant(CasterId, candidate), Is.False);
        }

        [Test]
        public void Predicate_ExcludesCandidatesThatCannotReceiveDamage()
        {
            var candidate = CreateDummy(2, Vector2.zero);
            candidate.CanReceiveDamage = false;
            Assert.That(AbilityTargetPredicate.IsValidCombatant(CasterId, candidate), Is.False);
        }

        [Test]
        public void Predicate_AcceptsALivingDamageableOtherThanTheCaster()
        {
            var candidate = CreateDummy(2, Vector2.zero);
            Assert.That(AbilityTargetPredicate.IsValidCombatant(CasterId, candidate), Is.True);
        }

        [Test]
        public void Predicate_RejectsNonCreatureEntitiesAndNull()
        {
            // A living, damageable entity that is not an EnemyCharacter (players, objects) is not a valid enemy.
            var candidate = CreateDummy(2, Vector2.zero);
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, candidate), Is.False);
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, null), Is.False);
        }

        // ---- Area query ------------------------------------------------------------------

        [Test]
        public void Area_IncludesInsideAndExcludesOutsideTheRadius()
        {
            CreateDummy(2, new Vector2(1f, 0f));
            CreateDummy(3, new Vector2(5f, 0f));

            Assert.That(Collect(Vector2.zero, 2f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Area_DoesNotRequireLineOfSight()
        {
            CreateDummy(2, new Vector2(1.5f, 0f));
            var wall = new GameObject("Wall");
            wall.transform.position = new Vector2(0.75f, 0f);
            wall.layer = _layer;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 4f);
            _objects.Add(wall);
            Physics2D.SyncTransforms();

            Assert.That(Collect(Vector2.zero, 2f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Area_CountsAnEntityWithSeveralHitboxesOnce()
        {
            var multi = CreateDummy(2, new Vector2(1f, 0f));
            var second = multi.gameObject.AddComponent<CircleCollider2D>();
            second.radius = 0.1f;
            second.offset = new Vector2(0.3f, 0f);
            _registry.TryRegister(multi.Id, multi, new[] { multi.GetComponent<Collider2D>(), second });
            Physics2D.SyncTransforms();

            Assert.That(Collect(Vector2.zero, 3f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Area_OrdersByDistanceThenEntityIdAndIsRepeatable()
        {
            CreateDummy(9, new Vector2(2f, 0f));
            CreateDummy(5, new Vector2(1f, 0f));
            CreateDummy(4, new Vector2(0f, 1f)); // same distance as 5: lower id first

            Collect(Vector2.zero, 3f);
            int[] first = Ids();
            Collect(Vector2.zero, 3f);

            Assert.That(first, Is.EqualTo(new[] { 4, 5, 9 }));
            Assert.That(Ids(), Is.EqualTo(first));
        }

        [Test]
        public void Area_RadiusIsAParameterAndDifferentRadiiGiveDifferentSets()
        {
            CreateDummy(2, new Vector2(1f, 0f));
            CreateDummy(3, new Vector2(4f, 0f));

            Collect(Vector2.zero, 2f);
            Assert.That(Ids(), Is.EqualTo(new[] { 2 }));
            Collect(Vector2.zero, 5f);
            Assert.That(Ids(), Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void Area_IsRebuiltAroundTheCurrentOrigin()
        {
            CreateDummy(2, new Vector2(10f, 0f));

            Collect(Vector2.zero, 2f);
            Assert.That(Ids(), Is.Empty);
            Collect(new Vector2(9f, 0f), 2f);
            Assert.That(Ids(), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Area_AppliesTheValidEnemyPredicateAndNeverTheCaster()
        {
            CreateDummy(CasterIdValue, new Vector2(0.5f, 0f));
            CreateDummy(2, new Vector2(1f, 0f));
            CreateDummy(3, new Vector2(1.5f, 0f));

            Assert.That(AbilityAreaTargetFinder.TryCollect(_query, _registry, CasterId, Vector2.zero, 3f,
                1 << _layer, _results, (_, damageable) => damageable.Id.Value != 3), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Area_ProductionPredicateRejectsEveryNonCreature()
        {
            CreateDummy(2, new Vector2(1f, 0f));

            Assert.That(AbilityAreaTargetFinder.TryCollect(_query, _registry, CasterId, Vector2.zero, 3f,
                1 << _layer, _results), Is.True);

            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Area_ResultsAreCallerOwnedAndSurviveASubsequentQuery()
        {
            CreateDummy(2, new Vector2(1f, 0f));
            Collect(Vector2.zero, 2f);
            var kept = new List<AttackTarget>(_results);

            // A second query on the shared target query (for example melee) reuses its internal list.
            _query.FindTargets(new AttackTargetQuery(CasterId, new Vector2(50f, 0f), Vector2.zero, 0f, 1f, 5, 1 << _layer));

            Assert.That(_results, Is.EqualTo(kept));
            Assert.That(_results, Has.Count.EqualTo(1));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Area_InvalidRadiusIsRejectedWithEmptyResults(float radius)
        {
            CreateDummy(2, new Vector2(1f, 0f));
            _results.Add(new AttackTarget(new EntityId(77), Vector2.zero));

            Assert.That(Collect(Vector2.zero, radius), Is.False);

            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Area_MissingDependenciesAreRejectedWithEmptyResults()
        {
            Assert.That(AbilityAreaTargetFinder.TryCollect(null, _registry, CasterId, Vector2.zero, 2f, 1 << _layer,
                _results, AcceptAll), Is.False);
            Assert.That(AbilityAreaTargetFinder.TryCollect(_query, null, CasterId, Vector2.zero, 2f, 1 << _layer,
                _results, AcceptAll), Is.False);
            Assert.That(AbilityAreaTargetFinder.TryCollect(_query, _registry, CasterId, Vector2.zero, 2f, 0,
                _results, AcceptAll), Is.False);
            Assert.That(AbilityAreaTargetFinder.TryCollect(_query, _registry, CasterId, Vector2.zero, 2f, 1 << _layer,
                null, AcceptAll), Is.False);
        }

        // ---- Helpers ---------------------------------------------------------------------

        private static bool AcceptAll(EntityId caster, IDamageable damageable) => true;

        private bool Collect(Vector2 origin, float radius) =>
            AbilityAreaTargetFinder.TryCollect(_query, _registry, CasterId, origin, radius, 1 << _layer, _results,
                AcceptAll);

        private int[] Ids()
        {
            var ids = new int[_results.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = _results[i].TargetId.Value;
            return ids;
        }

        private DummyCharacter CreateDummy(int id, Vector2 position)
        {
            var go = new GameObject($"Entity_{id}");
            go.transform.position = position;
            go.layer = _layer;
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.1f;
            var dummy = go.AddComponent<DummyCharacter>();
            dummy.Id = new EntityId(id);
            dummy.IsAlive = true;
            dummy.CanReceiveDamage = true;
            _registry.TryRegister(dummy.Id, dummy, new[] { collider });
            _objects.Add(go);
            Physics2D.SyncTransforms();
            return dummy;
        }

        private static void Inject(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private sealed class DummyCharacter : MonoBehaviour, IDamageable, ICharacter
        {
            public EntityId Id { get; set; }
            public bool IsAlive { get; set; }
            public bool CanReceiveDamage { get; set; }

            public DamageResult ApplyDamage(in DamageRequest request) =>
                new DamageResult(Id, true, request.Amount, 100f, false, DamageFailureReason.None);
        }
    }
}
