using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Abilities
{
    public sealed class AbilityAimResolverTests
    {
        [Test]
        public void TryResolve_ValidAim_ReturnsNormalizedAimIgnoringFacing()
        {
            bool resolved = AbilityAimResolver.TryResolve(Vector2.down, new Vector2(3f, 4f), out Vector2 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(new Vector2(3f, 4f).normalized));
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void TryResolve_ZeroAim_FallsBackToFacing()
        {
            bool resolved = AbilityAimResolver.TryResolve(Vector2.left, Vector2.zero, out Vector2 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(Vector2.left));
        }

        [Test]
        public void TryResolve_NonFiniteAim_FallsBackToFacing()
        {
            bool resolved = AbilityAimResolver.TryResolve(
                Vector2.up, new Vector2(float.NaN, float.PositiveInfinity), out Vector2 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void TryResolve_AimBelowThreshold_FallsBackToFacing()
        {
            bool resolved = AbilityAimResolver.TryResolve(Vector2.right, new Vector2(0.001f, 0f), out Vector2 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(Vector2.right));
        }

        [Test]
        public void TryResolve_NoUsableAimOrFacing_ReturnsFalseAndZero()
        {
            bool resolved = AbilityAimResolver.TryResolve(Vector2.zero, Vector2.zero, out Vector2 direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(Vector2.zero));
        }
    }
}
