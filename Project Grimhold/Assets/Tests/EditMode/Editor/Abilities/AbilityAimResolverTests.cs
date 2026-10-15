using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Abilities
{
    public sealed class AbilityAimResolverTests
    {
        [Test]
        public void TryResolve_ValidFacing_ReturnsNormalizedFacing()
        {
            bool resolved = AbilityAimResolver.TryResolve(new Vector2(3f, 4f), out Vector2 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(new Vector2(3f, 4f).normalized));
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void TryResolve_NonFiniteFacing_ReturnsFalseAndZero()
        {
            bool resolved = AbilityAimResolver.TryResolve(
                new Vector2(float.NaN, float.PositiveInfinity), out Vector2 direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TryResolve_FacingBelowThreshold_ReturnsFalseAndZero()
        {
            bool resolved = AbilityAimResolver.TryResolve(new Vector2(0.001f, 0f), out Vector2 direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TryResolve_NoUsableFacing_ReturnsFalseAndZero()
        {
            bool resolved = AbilityAimResolver.TryResolve(Vector2.zero, out Vector2 direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(Vector2.zero));
        }
    }
}
