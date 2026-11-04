using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Consumables
{
    public sealed class ThrowingDaggerProjectilePrefabTests
    {
        [TestCase(1f, 0f)]
        [TestCase(1f, 1f)]
        [TestCase(0f, 1f)]
        [TestCase(-1f, 1f)]
        [TestCase(-1f, 0f)]
        [TestCase(-1f, -1f)]
        [TestCase(0f, -1f)]
        [TestCase(1f, -1f)]
        public void VisualTip_AlignsWithFlightDirection(float x, float y)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Consumables/ThrowingDaggerProjectile.prefab");
            Assert.That(prefab, Is.Not.Null);
            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo(
                "Assets/Art/Consumables & Utilities/ThrowingDagger/ThrowingDagger/CON_THROWING_DAGGER-Icon.png"));
            Assert.That(renderer.flipX, Is.False);
            Assert.That(renderer.flipY, Is.False);

            // The supplied artwork points from its lower-left grip to its upper-right tip.
            Vector3 authoredTipAxis = new Vector3(1f, 1f, 0f).normalized;
            Vector3 tipInProjectileSpace = prefab.transform.InverseTransformVector(
                renderer.transform.TransformVector(authoredTipAxis)).normalized;
            Vector3 direction = new Vector3(x, y, 0f).normalized;
            float flightAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector3 renderedTip = Quaternion.Euler(0f, 0f, flightAngle) * tipInProjectileSpace;

            Assert.That(Vector3.Angle(renderedTip, direction), Is.LessThan(0.01f),
                "The blade tip must face the flight direction without rotating the physics root.");
        }
    }
}
