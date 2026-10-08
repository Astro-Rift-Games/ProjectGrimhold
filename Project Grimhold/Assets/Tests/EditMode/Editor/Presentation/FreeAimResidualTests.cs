using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class FreeAimResidualTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void AngleDegrees_IsZeroWhenTheAimIsTheBucketAxis()
        {
            Assert.That(FreeAimResidual.AngleDegrees(Vector2.down, Vector2.down), Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(0f, 1f, 0f, 1f, 0f)]
        [TestCase(1f, 0f, 1f, 1f, 45f)]
        [TestCase(1f, 0f, 1f, -1f, -45f)]
        [TestCase(0f, -1f, 1f, -2f, 26.5651f)]
        public void AngleDegrees_IsTheSignedTurnFromTheBucketToTheAim(
            float facingX, float facingY, float aimX, float aimY, float expected)
        {
            Assert.That(
                FreeAimResidual.AngleDegrees(new Vector2(facingX, facingY), new Vector2(aimX, aimY)),
                Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void AngleDegrees_WrapsAcrossTheSeamToTheShortestTurn()
        {
            Vector2 facing = new Vector2(-1f, 0.01f).normalized;
            Vector2 aim = new Vector2(-1f, -0.01f).normalized;

            float residual = FreeAimResidual.AngleDegrees(facing, aim);

            // 179.43 degrees to -179.43 degrees is a 1.146 degree counterclockwise turn through 180, not -358.9.
            Assert.That(residual, Is.EqualTo(1.1459f).Within(0.01f));
        }

        [Test]
        public void AngleDegrees_StaysWithinHalfATurnForEveryPair()
        {
            for (int a = 0; a < 360; a += 15)
            {
                for (int b = 0; b < 360; b += 15)
                {
                    float residual = FreeAimResidual.AngleDegrees(Direction(a), Direction(b));

                    Assert.That(residual, Is.InRange(-180f, 180f), $"{a},{b}");
                }
            }
        }

        [Test]
        public void AngleDegrees_AddedToTheBucketAngleReachesTheAimAngle()
        {
            Vector2 facing = Direction(135f);
            Vector2 aim = Direction(170f);

            float pivotAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg +
                FreeAimResidual.AngleDegrees(facing, aim);

            Assert.That(Mathf.DeltaAngle(pivotAngle, 170f), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void AngleDegrees_FallsBackToZeroForUnusableVectors()
        {
            Assert.That(FreeAimResidual.AngleDegrees(Vector2.zero, Vector2.up), Is.EqualTo(0f));
            Assert.That(FreeAimResidual.AngleDegrees(Vector2.up, Vector2.zero), Is.EqualTo(0f));
            Assert.That(FreeAimResidual.AngleDegrees(Vector2.up, new Vector2(float.NaN, 1f)), Is.EqualTo(0f));
        }

        [Test]
        public void RotateAbout_TurnsAPointAroundThePivot()
        {
            Vector2 result = PointRotation.About(new Vector2(2f, 1f), new Vector2(1f, 1f), 90f);

            Assert.That(result.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void RotateAbout_KeepsThePivotAndTheDistance()
        {
            Vector2 pivot = new Vector2(0.3f, -0.4f);
            Vector2 point = new Vector2(1.1f, 0.7f);

            Assert.That(PointRotation.About(pivot, pivot, 37f), Is.EqualTo(pivot));
            Assert.That(
                Vector2.Distance(PointRotation.About(point, pivot, 37f), pivot),
                Is.EqualTo(Vector2.Distance(point, pivot)).Within(Tolerance));
        }

        [Test]
        public void RotateAbout_ZeroDegreesIsTheIdentity()
        {
            Vector2 point = new Vector2(1.1f, 0.7f);

            Assert.That(PointRotation.About(point, Vector2.one, 0f), Is.EqualTo(point));
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
