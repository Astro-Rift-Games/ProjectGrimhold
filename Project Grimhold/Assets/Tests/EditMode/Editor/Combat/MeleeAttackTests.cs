using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public class MeleeAttackTests
    {
        private MeleeAttackConfig CreateConfig(float range = 1f, float radius = 0.5f, int maxTargets = 2, float damage = 10f, DamageType damageType = DamageType.Physical)
        {
            var config = ScriptableObject.CreateInstance<MeleeAttackConfig>();
            var type = typeof(MeleeAttackConfig);
            var baseType = typeof(AttackConfig);

            baseType.GetField("_damage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, damage);
            baseType.GetField("_damageType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, damageType);
            baseType.GetField("_cooldownSeconds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, 0.5f);
            baseType.GetField("_inputMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, AttackInputMode.Press);

            type.GetField("_range", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, range);
            type.GetField("_radius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, radius);
            type.GetField("_maximumTargets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, maxTargets);

            var layerMask = new LayerMask { value = -1 };
            type.GetField("_targetLayerMask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, layerMask);

            return config;
        }

        private GameObject _holder;
        private MeleeAttack _meleeAttack;
        private FakeTargetQuery _fakeQuery;
        private FakeDamageResolver _fakeResolver;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("MeleeAttackHolder");
            _meleeAttack = _holder.AddComponent<MeleeAttack>();
            _fakeQuery = new FakeTargetQuery();
            _fakeResolver = new FakeDamageResolver();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        [Test]
        public void Execute_DeliversNormalizedDirectionToQuery()
        {
            var config = CreateConfig();
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, new Vector2(2f, 0f), 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(new Vector2(1f, 0f), _fakeQuery.LastQuery.Direction);
        }

        [Test]
        public void Execute_WithoutTargets_ReturnsExecuted()
        {
            var config = CreateConfig();
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            var result = _meleeAttack.Execute(request);

            Assert.IsTrue(result.WasExecuted);
            Assert.AreEqual(0, _fakeResolver.ResolvedRequests.Count);
        }

        [Test]
        public void Execute_ExcludesAttackerDefensively()
        {
            var config = CreateConfig();
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);
            
            var attackerId = new EntityId(1);
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(attackerId, Vector2.right));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(2), Vector2.up));

            var request = new AttackRequest(attackerId, Vector2.zero, Vector2.right, 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(1, _fakeResolver.ResolvedRequests.Count);
            Assert.AreEqual(new EntityId(2), _fakeResolver.ResolvedRequests[0].TargetId);
        }

        [Test]
        public void Execute_DeduplicatesTargetsDefensively()
        {
            var config = CreateConfig(maxTargets: 5);
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            var targetId = new EntityId(2);
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(targetId, Vector2.right));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(targetId, Vector2.up));

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(1, _fakeResolver.ResolvedRequests.Count);
        }

        [Test]
        public void Execute_RespectsMaximumTargets()
        {
            var config = CreateConfig(maxTargets: 2);
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(2), Vector2.right));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(3), Vector2.up));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(4), Vector2.left));

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(2, _fakeResolver.ResolvedRequests.Count);
        }

        [Test]
        public void Execute_CopiesDamageDetailsFromConfig()
        {
            var config = CreateConfig(damage: 25f, damageType: DamageType.Physical);
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(2), Vector2.right));

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(1, _fakeResolver.ResolvedRequests.Count);
            Assert.AreEqual(25f, _fakeResolver.ResolvedRequests[0].Amount);
            Assert.AreEqual(DamageType.Physical, _fakeResolver.ResolvedRequests[0].DamageType);
        }

        [Test]
        public void Execute_CopiesSimulationTickDirectionAndHitPoint()
        {
            var config = CreateConfig();
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            var hitPoint = new Vector2(1.5f, 0.5f);
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(2), hitPoint));

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 42);
            _meleeAttack.Execute(request);

            Assert.AreEqual(1, _fakeResolver.ResolvedRequests.Count);
            Assert.AreEqual(42, _fakeResolver.ResolvedRequests[0].SimulationTick);
            Assert.AreEqual(Vector2.right, _fakeResolver.ResolvedRequests[0].Direction);
            Assert.AreEqual(hitPoint, _fakeResolver.ResolvedRequests[0].HitPoint);
        }

        [Test]
        public void Execute_WithInvalidDirection_RejectsExecution()
        {
            var config = CreateConfig();
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.zero, 10);
            var result = _meleeAttack.Execute(request);

            Assert.IsFalse(result.WasExecuted);
            Assert.AreEqual(AttackFailureReason.InvalidDirection, result.FailureReason);
        }

        [Test]
        public void Execute_WithoutVisualComponents_RunsSuccessfully()
        {
            var config = CreateConfig();
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            var result = _meleeAttack.Execute(request);

            Assert.IsTrue(result.WasExecuted);
        }

        [Test]
        public void Execute_DuplicatesDoNotConsumeMaximumTargets()
        {
            var config = CreateConfig(maxTargets: 2);
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            var targetA = new EntityId(2);
            var targetB = new EntityId(3);

            _fakeQuery.TargetsToReturn.Add(new AttackTarget(targetA, Vector2.right));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(targetA, Vector2.up));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(targetB, Vector2.left));

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(2, _fakeResolver.ResolvedRequests.Count);
            Assert.AreEqual(targetA, _fakeResolver.ResolvedRequests[0].TargetId);
            Assert.AreEqual(targetB, _fakeResolver.ResolvedRequests[1].TargetId);
        }

        [Test]
        public void Execute_AttackerDoesNotConsumeMaximumTargets()
        {
            var config = CreateConfig(maxTargets: 1);
            _meleeAttack.Initialize(config, _fakeQuery, _fakeResolver);

            var attackerId = new EntityId(1);
            var targetA = new EntityId(2);

            _fakeQuery.TargetsToReturn.Add(new AttackTarget(attackerId, Vector2.right));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(targetA, Vector2.left));

            var request = new AttackRequest(attackerId, Vector2.zero, Vector2.right, 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(1, _fakeResolver.ResolvedRequests.Count);
            Assert.AreEqual(targetA, _fakeResolver.ResolvedRequests[0].TargetId);
        }

        [Test]
        public void SlimeAttackClips_InvokePendingDamageAtHitFrame()
        {
            string[] clipPaths =
            {
                "Assets/Animations/Enemies/Melee/Slime/Attack/Slime-Attack-Down.anim",
                "Assets/Animations/Enemies/Melee/Slime/Attack/Slime-Attack-DownLeft.anim",
                "Assets/Animations/Enemies/Melee/Slime/Attack/Slime-Attack-DownRight.anim",
                "Assets/Animations/Enemies/Melee/Slime/Attack/Slime-Attack-Up.anim",
                "Assets/Animations/Enemies/Melee/Slime/Attack/Slime-Attack-UpLeft.anim",
                "Assets/Animations/Enemies/Melee/Slime/Attack/Slime-Attack-UpRight.anim"
            };

            for (int clipIndex = 0; clipIndex < clipPaths.Length; clipIndex++)
            {
                string clipPath = clipPaths[clipIndex];
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

                Assert.That(clip, Is.Not.Null, $"Missing slime attack clip at {clipPath}.");

                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
                Assert.That(events, Has.Length.EqualTo(1), $"Unexpected event count in {clipPath}.");
                Assert.That(
                    events[0].functionName,
                    Is.EqualTo(nameof(EnemyAttackAnimationListener.OnAttackHit)),
                    $"Wrong hit-frame event in {clipPath}.");
            }
        }

        private sealed class FakeTargetQuery : IAttackTargetQuery
        {
            public AttackTargetQuery LastQuery { get; private set; }
            public List<AttackTarget> TargetsToReturn { get; } = new();

            public IReadOnlyList<AttackTarget> FindTargets(in AttackTargetQuery query)
            {
                LastQuery = query;
                return TargetsToReturn;
            }
        }

        private sealed class FakeDamageResolver : IDamageResolver
        {
            public List<DamageRequest> ResolvedRequests { get; } = new();

            public DamageResult Resolve(in DamageRequest request)
            {
                ResolvedRequests.Add(request);
                return new DamageResult(request.TargetId, true, request.Amount, 100f, false, DamageFailureReason.None);
            }
        }
    }
}
