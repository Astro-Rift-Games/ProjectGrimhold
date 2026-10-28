#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class DamageResolverFriendlyFireTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void AllyDamage_FriendlyFireDisabled_BlocksBeforeTargetMutation(bool flagUnavailable)
        {
            var root = new GameObject("Resolver");
            var attackerObject = new GameObject("Attacker");
            var targetObject = new GameObject("Target");
            try
            {
                var registry = root.AddComponent<EntityRegistry>();
                var resolver = root.AddComponent<DamageResolver>();
                SetRegistry(resolver, registry);
                var attacker = attackerObject.AddComponent<PlayerCharacter>();
                var target = targetObject.AddComponent<PlayerCharacter>();
                // Seed only the lookup table: real player IDs require a spawned Fusion Object.
                var entities = (System.Collections.Generic.Dictionary<EntityId, IDamageable>)
                    typeof(EntityRegistry).GetField("_entities", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(registry);
                entities.Add(new EntityId(11), attacker);
                entities.Add(new EntityId(22), target);
                resolver.AreInitialTeammates = (_, _) => true;
                if (!flagUnavailable) resolver.FriendlyFireEnabled = () => false;
                // Unspawned network state would throw if either damage or knockback reached the target.
                DamageResult result = resolver.Resolve(Request(11));
                Assert.That(result.IsApplied, Is.False);
                Assert.That(result.AppliedDamage, Is.Zero);
                Assert.That(result.FailureReason, Is.EqualTo(DamageFailureReason.FriendlyFireBlocked));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(attackerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ZeroDamagePush_ResolverStillAppliesEligibleKnockback()
        {
            var root = new GameObject("Resolver");
            try
            {
                var registry = root.AddComponent<EntityRegistry>();
                var resolver = root.AddComponent<DamageResolver>();
                SetRegistry(resolver, registry);
                var target = new Target();
                registry.TryRegisterEntity(target.Id, target, Array.Empty<Collider2D>());
                var request = new DamageRequest(new EntityId(11), target.Id, 0f, DamageType.TrueDamage,
                    Vector2.right, Vector2.zero, 1, 2f);
                DamageResult result = resolver.Resolve(request);
                Assert.That(result.IsApplied, Is.False);
                Assert.That(target.KnockbackCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(11)]
        [TestCase(0)]
        public void NonAllyOrEnvironmentalDamage_IgnoresFriendlyFireMultiplier(int attackerId)
        {
            var root = new GameObject("Resolver");
            try
            {
                var registry = root.AddComponent<EntityRegistry>();
                var resolver = root.AddComponent<DamageResolver>();
                SetRegistry(resolver, registry);
                typeof(DamageResolver).GetField("_allyDamageMultiplier", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(resolver, 0.25f);
                resolver.FriendlyFireEnabled = () => false;
                resolver.AreInitialTeammates = (_, _) => throw new InvalidOperationException("Not PvP");
                var target = new Target();
                registry.TryRegisterEntity(target.Id, target, Array.Empty<Collider2D>());
                DamageResult result = resolver.Resolve(Request(attackerId));
                Assert.That(result.IsApplied, Is.True);
                Assert.That(target.LastRequest.Amount, Is.EqualTo(10f));
                Assert.That(target.LastRequest.IsAllyDirectDamage, Is.False);
                Assert.That(target.KnockbackCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static DamageRequest Request(int attackerId) => new DamageRequest(
            new EntityId(attackerId), new EntityId(22), 10f, DamageType.TrueDamage,
            Vector2.right, Vector2.zero, 1, 2f);

        private static void SetRegistry(DamageResolver resolver, EntityRegistry registry) =>
            typeof(DamageResolver).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(resolver, registry);

        private sealed class Target : IDamageable, IKnockbackReceiver
        {
            public EntityId Id => new EntityId(22);
            public bool CanReceiveDamage => true;
            public bool IsKnockbackEligible => true;
            public DamageRequest LastRequest { get; private set; }
            public int KnockbackCount { get; private set; }
            public DamageResult ApplyDamage(in DamageRequest request)
            {
                LastRequest = request;
                return request.Amount <= 0f
                    ? new DamageResult(Id, false, 0f, 10f, false, DamageFailureReason.InvalidAmount)
                    : new DamageResult(Id, true, request.Amount, 10f, false, DamageFailureReason.None);
            }
            public KnockbackDecision ReceiveKnockback(in KnockbackRequest request)
            {
                KnockbackCount++;
                return default;
            }
        }
    }
}
#endif
