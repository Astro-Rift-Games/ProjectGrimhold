using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class PlayerAimMathTests
    {
        [TestCase(4f, 0f, 1f, 0f)]
        [TestCase(0f, 4f, 0f, 1f)]
        public void TryResolveDirection_ValidAxisDirection_ReturnsNormalizedDirection(
            float aimX,
            float aimY,
            float expectedX,
            float expectedY)
        {
            bool resolved = PlayerAimMath.TryResolveDirection(
                Vector2.zero,
                new Vector2(aimX, aimY),
                out Vector2 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void TryResolveDirection_DiagonalFromDifferentOrigin_ReturnsNormalizedDirection()
        {
            bool resolved = PlayerAimMath.TryResolveDirection(
                new Vector2(2f, -3f),
                new Vector2(5f, 1f),
                out Vector2 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(new Vector2(3f, 4f).normalized));
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void TryResolveDirection_DeltaBelowThreshold_ReturnsFalseAndZero()
        {
            bool resolved = PlayerAimMath.TryResolveDirection(
                Vector2.zero,
                new Vector2(0.009f, 0f),
                out Vector2 direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TryResolveDirection_DeltaAboveThreshold_ReturnsNormalizedDirection()
        {
            bool resolved = PlayerAimMath.TryResolveDirection(
                Vector2.zero,
                new Vector2(0.01001f, 0f),
                out Vector2 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction, Is.EqualTo(Vector2.right));
        }

        [TestCase(float.NaN, 0f, 1f, 0f)]
        [TestCase(float.PositiveInfinity, 0f, 1f, 0f)]
        [TestCase(0f, float.NaN, 1f, 0f)]
        [TestCase(0f, float.PositiveInfinity, 1f, 0f)]
        [TestCase(1f, 0f, float.NaN, 0f)]
        [TestCase(1f, 0f, float.PositiveInfinity, 0f)]
        [TestCase(1f, 0f, 0f, float.NaN)]
        [TestCase(1f, 0f, 0f, float.PositiveInfinity)]
        public void TryResolveDirection_NonFiniteInput_ReturnsFalseAndZero(
            float originX,
            float originY,
            float aimX,
            float aimY)
        {
            bool resolved = PlayerAimMath.TryResolveDirection(
                new Vector2(originX, originY),
                new Vector2(aimX, aimY),
                out Vector2 direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TryResolveDirection_OverflowingDelta_ReturnsFalseAndZero()
        {
            bool resolved = PlayerAimMath.TryResolveDirection(
                new Vector2(float.MaxValue, 0f),
                new Vector2(-float.MaxValue, 0f),
                out Vector2 direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(Vector2.zero));
        }

        [TestCase(4f, 0f, 1f, 0f)]
        [TestCase(3f, 4f, 0.6f, 0.8f)]
        public void NormalizeInitialFacing_ValidDirection_ReturnsNormalizedDirection(
            float facingX,
            float facingY,
            float expectedX,
            float expectedY)
        {
            Vector2 facing = PlayerAimMath.NormalizeInitialFacing(
                new Vector2(facingX, facingY));

            Assert.That(facing.x, Is.EqualTo(expectedX).Within(0.0001f));
            Assert.That(facing.y, Is.EqualTo(expectedY).Within(0.0001f));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.009f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        public void NormalizeInitialFacing_InvalidDirection_ReturnsDown(float facingX, float facingY)
        {
            Vector2 facing = PlayerAimMath.NormalizeInitialFacing(
                new Vector2(facingX, facingY));

            Assert.That(facing, Is.EqualTo(Vector2.down));
        }
    }
}
