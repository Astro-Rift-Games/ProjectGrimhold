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

        [Test]
        public void DefaultTargetMode_StillConsumesAnotherPlayersDamageCollider()
        {
            NetworkProjectile projectile = CreateRegisteredTarget(false, out Collider2D collider);

            Assert.That(InvokeTargetModeImpact(projectile, collider, new EntityId(1), false, out EntityId targetId), Is.True);
            Assert.That(targetId.Value, Is.EqualTo(2));
        }

        [Test]
        public void EnemyOnlyMode_IgnoresAnotherPlayersDamageCollider()
        {
            NetworkProjectile projectile = CreateRegisteredTarget(false, out Collider2D collider);

            Assert.That(InvokeTargetModeImpact(projectile, collider, new EntityId(1), true, out _), Is.False);
        }

        [Test]
        public void EnemyOnlyMode_ConsumesALivingMarkedEnemyDamageCollider()
        {
            NetworkProjectile projectile = CreateRegisteredTarget(true, out Collider2D collider);

            Assert.That(InvokeTargetModeImpact(projectile, collider, new EntityId(1), true, out EntityId targetId), Is.True);
            Assert.That(targetId.Value, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothTargetModes_IgnoreCasterDamageColliders(bool enemyOnly)
        {
            NetworkProjectile projectile = CreateRegisteredTarget(true, out Collider2D collider);

            Assert.That(InvokeTargetModeImpact(projectile, collider, new EntityId(2), enemyOnly, out _), Is.False);
        }

        [Test]
        public void EnemyOnlyMode_IgnoresDeadOrUndamageableEnemies()
        {
            NetworkProjectile projectile = CreateRegisteredTarget(true, out Collider2D collider);
            var enemy = _targetObject.GetComponent<MarkedEnemyDamageable>();
            enemy.IsAlive = false;
            Assert.That(InvokeTargetModeImpact(projectile, collider, new EntityId(1), true, out _), Is.False);

            enemy.IsAlive = true;
            enemy.CanReceiveDamage = false;
            Assert.That(InvokeTargetModeImpact(projectile, collider, new EntityId(1), true, out _), Is.False);
        }

        [Test]
        public void EnemyOnlyMode_IgnoresEnemyMovementColliderWhenDamageHitboxIsExplicit()
        {
            NetworkProjectile projectile = CreateRegisteredTarget(true, out Collider2D collider);
            BoxCollider2D movementCollider = _targetObject.AddComponent<BoxCollider2D>();
            EntityRegistry registry = _registryObject.GetComponent<EntityRegistry>();
            var enemy = _targetObject.GetComponent<MarkedEnemyDamageable>();
            Assert.That(registry.TryRegisterDamageable(enemy.Id, enemy,
                new Collider2D[] { collider, movementCollider }, new Collider2D[] { collider }), Is.True);

            Assert.That(InvokeTargetModeImpact(projectile, movementCollider, new EntityId(1), true, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothTargetModes_ConsumeUnregisteredEnvironmentWithoutDamageTarget(bool enemyOnly)
        {
            NetworkProjectile projectile = CreateRegisteredTarget(true, out _);
            _worldObject = new GameObject("Wall");
            var collider = _worldObject.AddComponent<BoxCollider2D>();

            Assert.That(InvokeTargetModeImpact(projectile, collider, new EntityId(1), enemyOnly, out EntityId targetId), Is.True);
            Assert.That(targetId.Value, Is.Zero);
        }

        [Test]
        public void EnemyOnlyMode_StillAcceptsAnEnemyAfterCasterRegistrationIsGone()
        {
            // The caster is deliberately absent from the registry, as after defeat/extraction/despawn.
            NetworkProjectile projectile = CreateRegisteredTarget(true, out Collider2D collider);

            Assert.That(InvokeTargetModeImpact(projectile, collider, new EntityId(99), true, out EntityId targetId), Is.True);
            Assert.That(targetId.Value, Is.EqualTo(2));
        }

        private NetworkProjectile CreateRegisteredTarget(bool markedEnemy, out Collider2D collider)
        {
            _projectileObject = CreateProjectileObject();
            NetworkProjectile projectile = _projectileObject.AddComponent<NetworkProjectile>();
            _registryObject = new GameObject("Registry");
            EntityRegistry registry = _registryObject.AddComponent<EntityRegistry>();
            _targetObject = new GameObject(markedEnemy ? "Enemy" : "OtherPlayer");
            collider = _targetObject.AddComponent<BoxCollider2D>();
            IDamageable target = markedEnemy
                ? (IDamageable)_targetObject.AddComponent<MarkedEnemyDamageable>()
                : _targetObject.AddComponent<PlayerLikeDamageable>();
            Assert.That(registry.TryRegisterDamageable(target.Id, target,
                new Collider2D[] { collider }, new Collider2D[] { collider }), Is.True);
            typeof(NetworkProjectile).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(projectile, registry);
            return projectile;
        }

        // Explicit owner/mode parameters keep target selection testable without touching unspawned Fusion state.
        private static bool InvokeTargetModeImpact(NetworkProjectile projectile, Collider2D collider,
            EntityId ownerId, bool enemyOnly, out EntityId targetId)
        {
            MethodInfo method = typeof(NetworkProjectile).GetMethod("TryGetImpact",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(Collider2D), typeof(EntityId), typeof(bool), typeof(EntityId).MakeByRefType() }, null);
            Assert.That(method, Is.Not.Null,
                "TASK202 requires a deterministic TryGetImpact(Collider2D, EntityId, bool enemyOnly, out EntityId) seam.");
            object[] arguments = { collider, ownerId, enemyOnly, default(EntityId) };
            bool result = (bool)method.Invoke(projectile, arguments);
            targetId = (EntityId)arguments[3];
            return result;
        }

        private static bool InvokeTryGetImpact(
            NetworkProjectile projectile,
            Collider2D collider,
            out EntityId targetId)
        {
            MethodInfo method = typeof(NetworkProjectile).GetMethod(
                "TryGetImpact",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(Collider2D), typeof(EntityId).MakeByRefType() }, null);
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

        private class PlayerLikeDamageable : MonoBehaviour, IDamageable, ICharacter
        {
            public EntityId Id => new EntityId(2);
            public bool IsAlive { get; set; } = true;
            public bool CanReceiveDamage { get; set; } = true;
            public DamageResult ApplyDamage(in DamageRequest request) => default;
        }

        private sealed class MarkedEnemyDamageable : PlayerLikeDamageable, IAbilityEnemyTarget { }

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
