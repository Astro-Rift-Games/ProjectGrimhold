using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class AimDirectionSmoothingTests
    {
        private const float Tolerance = 0.0001f;
        private const float TurnRate = 720f;

        [Test]
        public void Resolve_WithoutSmoothing_ReturnsTheNormalizedAim()
        {
            Vector2 result = AimDirectionSmoothing.Resolve(
                new Vector2(0f, 4f), Vector2.down, Vector2.right, true, 0.016f, TurnRate, smooth: false);

            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Resolve_WithoutPreviousSample_SnapsToTheAim()
        {
            Vector2 result = AimDirectionSmoothing.Resolve(
                Vector2.up, Vector2.down, Vector2.zero, false, 0.016f, TurnRate, smooth: true);

            Assert.That(result.y, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Resolve_Smoothing_TurnsAtMostTheRateTimesDeltaTime()
        {
            Vector2 result = AimDirectionSmoothing.Resolve(
                Vector2.up, Vector2.down, Vector2.right, true, 0.05f, TurnRate, smooth: true);

            Assert.That(Vector2.SignedAngle(Vector2.right, result), Is.EqualTo(36f).Within(0.01f));
        }

        [Test]
        public void Resolve_Smoothing_ReachesTheTargetWithoutOvershoot()
        {
            Vector2 result = AimDirectionSmoothing.Resolve(
                Vector2.up, Vector2.down, new Vector2(1f, 0.1f).normalized, true, 1f, TurnRate, smooth: true);

            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Resolve_Smoothing_TurnsThroughTheShortestArcAcrossTheSeam()
        {
            Vector2 previous = new Vector2(-1f, 0.1f).normalized;
            Vector2 target = new Vector2(-1f, -0.1f).normalized;

            Vector2 result = AimDirectionSmoothing.Resolve(
                target, Vector2.down, previous, true, 0.001f, TurnRate, smooth: true);

            Assert.That(result.x, Is.LessThan(-0.9f), "The short arc stays on the left side and never sweeps through +X.");
        }

        [Test]
        public void Resolve_Smoothing_NeverPassesThroughZeroOnAnOppositeTurn()
        {
            Vector2 previous = Vector2.right;
            for (int step = 0; step < 100; step++)
            {
                previous = AimDirectionSmoothing.Resolve(
                    Vector2.left, Vector2.down, previous, true, 0.01f, TurnRate, smooth: true);

                Assert.That(previous.magnitude, Is.EqualTo(1f).Within(Tolerance));
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(0.0001f, 0f)]
        [TestCase(float.NaN, 1f)]
        public void Resolve_NearZeroOrInvalidAim_FallsBackToTheFacing(float x, float y)
        {
            Vector2 result = AimDirectionSmoothing.Resolve(
                new Vector2(x, y), Vector2.left, Vector2.up, false, 0.016f, TurnRate, smooth: false);

            Assert.That(result.x, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Resolve_InvalidAimAndFacing_KeepsThePreviousDirection()
        {
            Vector2 result = AimDirectionSmoothing.Resolve(
                Vector2.zero, Vector2.zero, Vector2.up, true, 0.016f, TurnRate, smooth: false);

            Assert.That(result.y, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Resolve_InvalidEverything_DefaultsToDown()
        {
            Vector2 result = AimDirectionSmoothing.Resolve(
                Vector2.zero, Vector2.zero, Vector2.zero, false, 0.016f, TurnRate, smooth: false);

            Assert.That(result.y, Is.EqualTo(-1f).Within(Tolerance));
        }
    }
}
