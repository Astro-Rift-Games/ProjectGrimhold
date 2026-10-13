using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Abilities
{
    public sealed class AbilityAllyTargetingTests
    {
        private const int CasterIdValue = 1;
        private static readonly EntityId CasterId = new EntityId(CasterIdValue);

        private readonly List<GameObject> _objects = new();
        private readonly HashSet<int> _teammates = new();
        private EntityRegistry _registry;
        private Physics2DAttackTargetQuery _query;
        private int _layer;
        private DummyCharacter _caster;
        private List<DummyCharacter> _results;

        [SetUp]
        public void SetUp()
        {
            _layer = LayerMask.NameToLayer("Default");
            _results = new List<DummyCharacter>();
            _teammates.Clear();
            var registryHolder = new GameObject("Registry");
            _registry = registryHolder.AddComponent<EntityRegistry>();
            _objects.Add(registryHolder);

            var queryHolder = new GameObject("Query");
            _query = queryHolder.AddComponent<Physics2DAttackTargetQuery>();
            _objects.Add(queryHolder);
            Inject(_query, "_registry", _registry);
            Inject(_query, "_colliderBuffer", new Collider2D[64]);

            _caster = CreateDummy(CasterIdValue, Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _objects.Clear();
        }

        // ---- Predicate -------------------------------------------------------------------

        [TestCase(true, true, false, ExpectedResult = true)]
        [TestCase(true, true, true, ExpectedResult = true)]
        [TestCase(false, true, true, ExpectedResult = true)]
        [TestCase(false, true, false, ExpectedResult = false)]
        [TestCase(true, false, false, ExpectedResult = false)]
        [TestCase(false, false, true, ExpectedResult = false)]
        public bool Predicate_AllyRuleIsAliveAndCasterOrTeammate(bool isCaster, bool isAlive, bool isTeammate) =>
            AbilityTargetPredicate.IsAllyEligible(isCaster, isAlive, isTeammate);

        [Test]
        public void Predicate_PlayersSelfIsAlwaysAnAllyWithoutAskingForAffiliation()
        {
            PlayerCharacter caster = CreatePlayer();
            int calls = 0;

            bool result = AbilityTargetPredicate.IsValidAlly(caster, caster, (_, _) => { calls++; return false; });

            Assert.That(result, Is.True);
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Predicate_PlayersFollowTheInjectedTeammateDecision()
        {
            PlayerCharacter caster = CreatePlayer();
            PlayerCharacter other = CreatePlayer();

            Assert.That(AbilityTargetPredicate.IsValidAlly(caster, other, (_, _) => true), Is.True);
            Assert.That(AbilityTargetPredicate.IsValidAlly(caster, other, (_, _) => false), Is.False);
        }

        [Test]
        public void Predicate_NullCasterOrCandidateIsNotAnAlly()
        {
            PlayerCharacter player = CreatePlayer();

            Assert.That(AbilityTargetPredicate.IsValidAlly(null, player, (_, _) => true), Is.False);
            Assert.That(AbilityTargetPredicate.IsValidAlly(player, null, (_, _) => true), Is.False);
        }

        [Test]
        public void Affiliation_FailsClosedWhenNothingIsResolved()
        {
            PlayerCharacter left = CreatePlayer();
            PlayerCharacter right = CreatePlayer();

            Assert.That(AbilityAllyAffiliation.AreInitialTeammates(left, right), Is.False);
            Assert.That(AbilityAllyAffiliation.AreInitialTeammates(left, null), Is.False);
            Assert.That(AbilityAllyAffiliation.AreInitialTeammates(null, right), Is.False);
            Assert.That(AbilityTargetPredicate.IsValidAlly(left, right), Is.False);
        }

        // ---- Area query ------------------------------------------------------------------

        [Test]
        public void Allies_SelfAloneSatisfiesTheSet()
        {
            Assert.That(Collect(Vector2.zero, 3f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { CasterIdValue }));
        }

        [Test]
        public void Allies_TeammateInsideTheRadiusIsIncludedAfterTheCaster()
        {
            CreateTeammate(2, new Vector2(1f, 0f));

            Assert.That(Collect(Vector2.zero, 3f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { CasterIdValue, 2 }));
        }

        [Test]
        public void Allies_TeammateOutsideTheRadiusIsExcluded()
        {
            CreateTeammate(2, new Vector2(5f, 0f));

            Assert.That(Collect(Vector2.zero, 3f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { CasterIdValue }));
        }

        [Test]
        public void Allies_NonTeammateIsExcluded()
        {
            CreateDummy(2, new Vector2(1f, 0f));

            Assert.That(Collect(Vector2.zero, 3f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { CasterIdValue }));
        }

        [Test]
        public void Allies_DeadTeammateIsExcluded()
        {
            DummyCharacter dead = CreateTeammate(2, new Vector2(1f, 0f));
            dead.IsAlive = false;
            CreateTeammate(3, new Vector2(1.5f, 0f));

            Assert.That(Collect(Vector2.zero, 3f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { CasterIdValue, 3 }));
        }

        [Test]
        public void Allies_DeadCasterIsNotItsOwnAlly()
        {
            _caster.IsAlive = false;
            CreateTeammate(2, new Vector2(1f, 0f));

            Assert.That(Collect(Vector2.zero, 3f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Allies_AreOrderedCasterThenDistanceThenEntityIdAndAreRepeatable()
        {
            CreateTeammate(9, new Vector2(2f, 0f));
            CreateTeammate(5, new Vector2(1f, 0f));
            CreateTeammate(4, new Vector2(0f, 1f)); // same distance as 5: lower id first

            Collect(Vector2.zero, 3f);
            int[] first = Ids();
            Collect(Vector2.zero, 3f);

            Assert.That(first, Is.EqualTo(new[] { CasterIdValue, 4, 5, 9 }));
            Assert.That(Ids(), Is.EqualTo(first));
        }

        [Test]
        public void Allies_AreDeduplicatedEvenWhenTheQueryReturnsTheCasterOrSeveralHitboxes()
        {
            // The caster has its own collider on the target layer, and a teammate has two hitboxes.
            var multi = CreateTeammate(2, new Vector2(1f, 0f));
            var second = multi.gameObject.AddComponent<CircleCollider2D>();
            second.radius = 0.1f;
            second.offset = new Vector2(0.3f, 0f);
            _registry.TryRegister(multi.Id, multi, new[] { multi.GetComponent<Collider2D>(), second });
            Physics2D.SyncTransforms();

            Assert.That(Collect(Vector2.zero, 3f), Is.True);

            Assert.That(Ids(), Is.EqualTo(new[] { CasterIdValue, 2 }));
        }

        [Test]
        public void Allies_AreRebuiltAroundTheCurrentOrigin()
        {
            CreateTeammate(2, new Vector2(10f, 0f));

            Collect(Vector2.zero, 2f);
            Assert.That(Ids(), Is.EqualTo(new[] { CasterIdValue }));
            Collect(new Vector2(9f, 0f), 2f);
            Assert.That(Ids(), Is.EqualTo(new[] { CasterIdValue, 2 }));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Allies_InvalidRadiusIsRejectedWithEmptyResults(float radius)
        {
            CreateTeammate(2, new Vector2(1f, 0f));
            _results.Add(_caster);

            Assert.That(Collect(Vector2.zero, radius), Is.False);

            Assert.That(_results, Is.Empty);
        }

        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(0f, float.NegativeInfinity)]
        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.NaN)]
        public void Allies_NonfiniteOriginIsRejectedBeforeQueryOrSelfInclusion(float x, float y)
        {
            _results.Add(_caster);
            int predicateCalls = 0;
            Assert.That(AbilityAreaTargetFinder.TryCollectAllies(_query, _registry, CasterId, _caster,
                new Vector2(x, y), 2f, 1 << _layer, _results,
                (caster, candidate) => { predicateCalls++; return true; }), Is.False);
            Assert.That(_results, Is.Empty);
            Assert.That(predicateCalls, Is.Zero);
        }

        [Test]
        public void Allies_MissingDependenciesAreRejectedWithEmptyResults()
        {
            Assert.That(AbilityAreaTargetFinder.TryCollectAllies(null, _registry, CasterId, _caster, Vector2.zero, 2f,
                1 << _layer, _results, IsAlly), Is.False);
            Assert.That(AbilityAreaTargetFinder.TryCollectAllies(_query, null, CasterId, _caster, Vector2.zero, 2f,
                1 << _layer, _results, IsAlly), Is.False);
            Assert.That(AbilityAreaTargetFinder.TryCollectAllies(_query, _registry, CasterId, (DummyCharacter)null,
                Vector2.zero, 2f, 1 << _layer, _results, IsAlly), Is.False);
            Assert.That(AbilityAreaTargetFinder.TryCollectAllies(_query, _registry, CasterId, _caster, Vector2.zero, 2f,
                0, _results, IsAlly), Is.False);
            Assert.That(AbilityAreaTargetFinder.TryCollectAllies(_query, _registry, CasterId, _caster, Vector2.zero, 2f,
                1 << _layer, null, IsAlly), Is.False);
            Assert.That(AbilityAreaTargetFinder.TryCollectAllies(_query, _registry, CasterId, _caster, Vector2.zero, 2f,
                1 << _layer, _results, null), Is.False);
            Assert.That(_results, Is.Empty);
        }

        // ---- Helpers ---------------------------------------------------------------------

        private bool IsAlly(DummyCharacter caster, DummyCharacter candidate) =>
            AbilityTargetPredicate.IsAllyEligible(ReferenceEquals(caster, candidate), candidate.IsAlive,
                _teammates.Contains(candidate.Id.Value));

        private bool Collect(Vector2 origin, float radius) =>
            AbilityAreaTargetFinder.TryCollectAllies(_query, _registry, CasterId, _caster, origin, radius,
                1 << _layer, _results, IsAlly);

        private int[] Ids()
        {
            var ids = new int[_results.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = _results[i].Id.Value;
            return ids;
        }

        private DummyCharacter CreateTeammate(int id, Vector2 position)
        {
            _teammates.Add(id);
            return CreateDummy(id, position);
        }

        private PlayerCharacter CreatePlayer()
        {
            // Inactive so Awake/Fusion composition never runs; IsAlive and affiliation are all the tests need.
            var go = new GameObject("Player");
            go.SetActive(false);
            _objects.Add(go);
            return go.AddComponent<PlayerCharacter>();
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
