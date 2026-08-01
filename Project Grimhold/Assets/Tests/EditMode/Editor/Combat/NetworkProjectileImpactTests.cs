#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public sealed class NetworkProjectileImpactTests
    {
        private GameObject _projectileObject;
        private GameObject _worldObject;
        private GameObject _registryObject;
        private GameObject _targetObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_projectileObject);
            Object.DestroyImmediate(_worldObject);
            Object.DestroyImmediate(_registryObject);
            Object.DestroyImmediate(_targetObject);
        }

        [Test]
        public void UnregisteredWorldColliderIsAConsumingImpact()
        {
            _projectileObject = CreateProjectileObject();
            NetworkProjectile projectile = _projectileObject.AddComponent<NetworkProjectile>();
            _worldObject = new GameObject("World");
            Collider2D worldCollider = _worldObject.AddComponent<BoxCollider2D>();

            bool isImpact = InvokeTryGetImpact(projectile, worldCollider, out EntityId targetId);

            Assert.That(isImpact, Is.True);
            Assert.That(targetId.Value, Is.Zero);
        }

        [Test]
        public void ProjectileOwnColliderIsNotAnImpact()
        {
            _projectileObject = CreateProjectileObject();
            NetworkProjectile projectile = _projectileObject.AddComponent<NetworkProjectile>();
            Collider2D projectileCollider = _projectileObject.GetComponent<Collider2D>();

            bool isImpact = InvokeTryGetImpact(projectile, projectileCollider, out _);

            Assert.That(isImpact, Is.False);
        }

        [Test]
        public void RegisteredMovementColliderIsNotAProjectileImpactWhenEntityDefinesDamageHitbox()
        {
            _projectileObject = CreateProjectileObject();
            NetworkProjectile projectile = _projectileObject.AddComponent<NetworkProjectile>();

            _registryObject = new GameObject("Registry");
            EntityRegistry registry = _registryObject.AddComponent<EntityRegistry>();

            _targetObject = new GameObject("Target");
            BoxCollider2D movementCollider = _targetObject.AddComponent<BoxCollider2D>();
            GameObject damageHitboxObject = new GameObject("DamageHitbox");
            damageHitboxObject.transform.SetParent(_targetObject.transform, false);
            BoxCollider2D damageHitbox = damageHitboxObject.AddComponent<BoxCollider2D>();
            var damageable = _targetObject.AddComponent<DummyDamageable>();
            damageable.IdValue = 2;

            Assert.That(
                registry.TryRegisterDamageable(
                    damageable.Id,
                    damageable,
                    new Collider2D[] { movementCollider, damageHitbox },
                    new Collider2D[] { damageHitbox }),
                Is.True);

            typeof(NetworkProjectile)
                .GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(projectile, registry);

            bool isImpact = InvokeTryGetImpact(projectile, movementCollider, out EntityId targetId);

            Assert.That(isImpact, Is.False);
            Assert.That(targetId, Is.EqualTo(damageable.Id));
        }

        private static bool InvokeTryGetImpact(
            NetworkProjectile projectile,
            Collider2D collider,
            out EntityId targetId)
        {
            MethodInfo method = typeof(NetworkProjectile).GetMethod(
                "TryGetImpact",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { collider, default(EntityId) };
            bool result = (bool)method.Invoke(projectile, arguments);
            targetId = (EntityId)arguments[1];
            return result;
        }

        private static GameObject CreateProjectileObject()
        {
            var projectileObject = new GameObject("Projectile");
            projectileObject.AddComponent<Rigidbody2D>();
            projectileObject.AddComponent<BoxCollider2D>();
            return projectileObject;
        }

        private sealed class DummyDamageable : MonoBehaviour, IDamageable
        {
            public int IdValue { get; set; }
            public EntityId Id => new EntityId(IdValue);
            public bool CanReceiveDamage => true;

            public DamageResult ApplyDamage(in DamageRequest request)
            {
                return default;
            }
        }
    }
}
#endif
