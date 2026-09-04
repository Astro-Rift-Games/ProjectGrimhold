using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Combat
{
    public class MeleeAttackTests
    {
        private MeleeAttackConfig CreateConfig(float radius = 0.5f, int maxTargets = 2)
        {
            var config = ScriptableObject.CreateInstance<MeleeAttackConfig>();
            var type = typeof(MeleeAttackConfig);
            typeof(AttackConfig).GetField("_inputMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, AttackInputMode.Press);
            type.GetField("_radius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, radius);
            type.GetField("_maximumTargets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, maxTargets);

            var layerMask = new LayerMask { value = -1 };
            type.GetField("_targetLayerMask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(config, layerMask);

            return config;
        }

        private static AttackExecutionParameters CreateParameters(
            float damage = 10f,
            DamageType damageType = DamageType.Physical,
            float cooldown = 0.5f,
            float effectiveRange = 1.5f,
            float knockback = 7f) =>
            new(damage, damageType, cooldown, effectiveRange, knockback);

        private void Initialize(MeleeAttackConfig config, AttackExecutionParameters? parameters = null) =>
            _meleeAttack.Initialize(config, parameters ?? CreateParameters(), _fakeQuery, _fakeResolver);

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
            Initialize(config);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, new Vector2(2f, 0f), 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(new Vector2(1f, 0f), _fakeQuery.LastQuery.Direction);
        }

        [Test]
        public void Execute_WithoutTargets_ReturnsExecuted()
        {
            var config = CreateConfig();
            Initialize(config);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            var result = _meleeAttack.Execute(request);

            Assert.IsTrue(result.WasExecuted);
            Assert.AreEqual(0, _fakeResolver.ResolvedRequests.Count);
        }

        [Test]
        public void Execute_ExcludesAttackerDefensively()
        {
            var config = CreateConfig();
            Initialize(config);
            
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
            Initialize(config);

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
            Initialize(config);

            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(2), Vector2.right));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(3), Vector2.up));
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(4), Vector2.left));

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(2, _fakeResolver.ResolvedRequests.Count);
        }

        [Test]
        public void Execute_CopiesDamageDetailsFromRuntimeParameters()
        {
            var config = CreateConfig();
            Initialize(config, CreateParameters(damage: 25f, damageType: DamageType.Magical));

            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(2), Vector2.right));

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            _meleeAttack.Execute(request);

            Assert.AreEqual(1, _fakeResolver.ResolvedRequests.Count);
            Assert.AreEqual(25f, _fakeResolver.ResolvedRequests[0].Amount);
            Assert.AreEqual(DamageType.Magical, _fakeResolver.ResolvedRequests[0].DamageType);
        }

        [Test]
        public void TryConfigure_UpdatesAllRuntimeParametersWithoutMutatingConfig()
        {
            var config = CreateConfig(radius: 0.5f);
            Initialize(config);
            var parameters = CreateParameters(31.75f, DamageType.Magical, 1.25f, 2f, 15f);

            Assert.That(_meleeAttack.TryConfigure(config, parameters), Is.True);
            _fakeQuery.TargetsToReturn.Add(new AttackTarget(new EntityId(2), Vector2.right));

            _meleeAttack.Execute(new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10));

            Assert.That(_fakeResolver.ResolvedRequests, Has.Count.EqualTo(1));
            Assert.That(_fakeResolver.ResolvedRequests[0].Amount, Is.EqualTo(31.75f));
            Assert.That(_fakeResolver.ResolvedRequests[0].DamageType, Is.EqualTo(DamageType.Magical));
            Assert.That(_fakeResolver.ResolvedRequests[0].KnockbackForce, Is.EqualTo(15f));
            Assert.That(_meleeAttack.CooldownSeconds, Is.EqualTo(1.25f));
            Assert.That(_fakeQuery.LastQuery.Range, Is.EqualTo(1.5f));
            Assert.That(config.Radius, Is.EqualTo(0.5f));
        }

        [Test]
        public void TryConfigure_WhenEffectiveRangeIsLessThanRadius_IsRejected()
        {
            var config = CreateConfig(radius: 0.5f);
            Initialize(config);

            LogAssert.Expect(
                LogType.Error,
                "MeleeAttack: Effective range must be at least the detection radius on GameObject MeleeAttackHolder.");
            Assert.That(_meleeAttack.TryConfigure(config, CreateParameters(effectiveRange: 0.49f)), Is.False);
        }

        [Test]
        public void Execute_UsesEffectiveRangeMinusRadiusAsDetectionCenterOffset()
        {
            var config = CreateConfig(radius: 0.5f);
            Initialize(config, CreateParameters(effectiveRange: 1.5f));

            _meleeAttack.Execute(new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10));

            Assert.That(_fakeQuery.LastQuery.Range, Is.EqualTo(1f));
            Assert.That(_meleeAttack.EffectiveRange, Is.EqualTo(1.5f));
        }

        [Test]
        public void Execute_CopiesSimulationTickDirectionAndHitPoint()
        {
            var config = CreateConfig();
            Initialize(config);

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
            Initialize(config);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.zero, 10);
            var result = _meleeAttack.Execute(request);

            Assert.IsFalse(result.WasExecuted);
            Assert.AreEqual(AttackFailureReason.InvalidDirection, result.FailureReason);
        }

        [Test]
        public void Execute_WithoutVisualComponents_RunsSuccessfully()
        {
            var config = CreateConfig();
            Initialize(config);

            var request = new AttackRequest(new EntityId(1), Vector2.zero, Vector2.right, 10);
            var result = _meleeAttack.Execute(request);

            Assert.IsTrue(result.WasExecuted);
        }

        [Test]
        public void Execute_DuplicatesDoNotConsumeMaximumTargets()
        {
            var config = CreateConfig(maxTargets: 2);
            Initialize(config);

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
            Initialize(config);

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
            string[] clipGuids = AssetDatabase.FindAssets(
                "t:AnimationClip",
                new[] { "Assets/Animations/Enemies/Melee/Slimes" });
            string[] allClipPaths = System.Array.ConvertAll(clipGuids, AssetDatabase.GUIDToAssetPath);
            string[] clipPaths = System.Array.FindAll(
                allClipPaths,
                path => path.Contains("/Attack/"));

            Assert.That(clipPaths, Has.Length.EqualTo(18));

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
