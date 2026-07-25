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

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_projectileObject);
            Object.DestroyImmediate(_worldObject);
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
    }
}
#endif
