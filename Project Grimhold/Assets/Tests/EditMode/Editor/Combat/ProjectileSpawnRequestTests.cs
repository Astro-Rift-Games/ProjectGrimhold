using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public sealed class ProjectileSpawnRequestTests
    {
        [Test]
        public void LegacyRequest_DefaultsToBoundedAndAllDamageableTargets()
        {
            var request = LegacyRequest(5f, 10f);

            Assert.That(request.TryValidate(), Is.True);
            Assert.That(ReadFlag(request, "IsUnbounded"), Is.False);
            Assert.That(ReadFlag(request, "EnemyOnly"), Is.False);
        }

        [TestCase(0f, 10f)]
        [TestCase(5f, 0f)]
        [TestCase(-1f, 10f)]
        [TestCase(5f, -1f)]
        public void LegacyRequest_StillRequiresPositiveLifetimeAndRange(float lifetime, float range)
        {
            Assert.That(LegacyRequest(lifetime, range).TryValidate(), Is.False);
        }

        [Test]
        public void UnboundedOptIn_AcceptsZeroLifetimeAndRangeWithoutInventingLargeLimits()
        {
            var request = OptInRequest(0f, 0f, true, true);

            Assert.That(request.TryValidate(), Is.True);
            Assert.That(request.LifetimeSeconds, Is.Zero);
            Assert.That(request.MaximumRange, Is.Zero);
            Assert.That(ReadFlag(request, "IsUnbounded"), Is.True);
            Assert.That(ReadFlag(request, "EnemyOnly"), Is.True);
        }

        [Test]
        public void EnemyOnlyOptIn_DoesNotImplicitlyDisableLifetimeOrRange()
        {
            Assert.That(OptInRequest(0f, 0f, false, true).TryValidate(), Is.False);
            Assert.That(OptInRequest(5f, 10f, false, true).TryValidate(), Is.True);
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, -1f)]
        public void UnboundedOptIn_StillRejectsInvalidLimitPayload(float lifetime, float range)
        {
            Assert.That(OptInRequest(lifetime, range, true, true).TryValidate(), Is.False);
        }

        [TestCase(0f, 10f, 1f)]
        [TestCase(float.NaN, 10f, 1f)]
        [TestCase(5f, 0f, 1f)]
        [TestCase(5f, float.PositiveInfinity, 1f)]
        [TestCase(5f, 10f, 0f)]
        [TestCase(5f, 10f, float.NaN)]
        public void UnboundedOptIn_DoesNotBypassDamageSpeedOrDirectionValidation(float damage, float speed, float directionX)
        {
            var request = OptInRequest(0f, 0f, true, true, damage, speed, new Vector2(directionX, 0f));

            Assert.That(request.TryValidate(), Is.False);
        }

        [Test]
        public void ExplicitPrefabAndMask_AreRetainedIndependentlyOfWeaponConfiguration()
        {
            var prefab = new NetworkPrefabRef("cdc42ba57b303264fb49354983542d59");
            var request = OptInRequest(0f, 0f, true, true, prefab: prefab, mask: 1 << 8);

            Assert.That(request.ProjectilePrefab, Is.EqualTo(prefab));
            Assert.That(request.ImpactLayerMask, Is.EqualTo(1 << 8));
            Assert.That(request.TryValidate(), Is.True);
        }

        private static ProjectileSpawnRequest LegacyRequest(float lifetime, float range) =>
            new ProjectileSpawnRequest(new EntityId(1), Vector2.zero, Vector2.right, 5f,
                DamageType.Magical, 10f, lifetime, range, 100);

        private static bool ReadFlag(ProjectileSpawnRequest request, string name)
        {
            PropertyInfo property = typeof(ProjectileSpawnRequest).GetProperty(name);
            Assert.That(property, Is.Not.Null, $"TASK202 requires opt-in ProjectileSpawnRequest.{name}.");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(bool)));
            return (bool)property.GetValue(request);
        }

        // Reflect the additive constructor so this test file compiles before the opt-in API exists.
        private static ProjectileSpawnRequest OptInRequest(float lifetime, float range, bool unbounded, bool enemyOnly,
            float damage = 5f, float speed = 10f, Vector2? direction = null,
            NetworkPrefabRef prefab = default, int mask = 0)
        {
            ConstructorInfo constructor = null;
            foreach (ConstructorInfo candidate in typeof(ProjectileSpawnRequest).GetConstructors())
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length == 14 && parameters[12].ParameterType == typeof(bool) &&
                    parameters[13].ParameterType == typeof(bool)) constructor = candidate;
            }
            Assert.That(constructor, Is.Not.Null,
                "Append optional bool isUnbounded = false and bool enemyOnly = false to the existing request constructor.");
            ParameterInfo[] args = constructor.GetParameters();
            Assert.That(args[12].IsOptional && args[13].IsOptional, Is.True, "Existing bounded attack callers must compile unchanged.");
            Assert.That(args[12].DefaultValue, Is.EqualTo(false));
            Assert.That(args[13].DefaultValue, Is.EqualTo(false));
            return (ProjectileSpawnRequest)constructor.Invoke(new object[]
            {
                new EntityId(1), Vector2.zero, direction ?? Vector2.right, damage, DamageType.Magical,
                speed, lifetime, range, 100, 0f, prefab, mask, unbounded, enemyOnly
            });
        }
    }
}
