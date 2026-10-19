using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;
using UnityEngine;

namespace Tests.EditMode.Visibility
{
    public sealed class VisionFogRulesTests
    {
        private const float Tolerance = 0.0001f;

        [TestCase(0f)]
        [TestCase(-1f)]
        public void SmoothFacing_NonPositiveSharpness_SnapsToNormalizedTarget(float sharpness)
        {
            AssertVector(VisionFogRules.SmoothFacing(Vector2.right, Vector2.up * 4f, sharpness, 0.05f), Vector2.up);
        }

        [Test]
        public void SmoothFacing_ZeroCurrent_SnapsToNormalizedTarget()
        {
            AssertVector(VisionFogRules.SmoothFacing(Vector2.zero, Vector2.up * 4f, 10f, 0.05f), Vector2.up);
        }

        [Test]
        public void SmoothFacing_UnusableCurrent_SnapsToNormalizedTarget()
        {
            AssertVector(VisionFogRules.SmoothFacing(new Vector2(0.001f, 0f), Vector2.up * 4f, 10f, 0.05f), Vector2.up);
        }

        [Test]
        public void SmoothFacing_UnusableTarget_ReturnsNormalizedCurrent()
        {
            AssertVector(VisionFogRules.SmoothFacing(Vector2.right * 3f, Vector2.zero, 10f, 0.05f), Vector2.right);
        }

        [Test]
        public void SmoothFacing_RightTowardUp_MovesPartiallyAndStaysNormalized()
        {
            Vector2 result = VisionFogRules.SmoothFacing(Vector2.right, Vector2.up, 10f, 0.05f);
            float angle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;

            Assert.That(angle, Is.GreaterThan(0f).And.LessThan(90f));
            Assert.That(angle, Is.EqualTo(90f * (1f - Mathf.Exp(-0.5f))).Within(Tolerance));
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void SmoothFacing_AcrossAngleBoundary_TakesShortestArc()
        {
            Vector2 result = VisionFogRules.SmoothFacing(Direction(170f), Direction(-170f), 10f, 0.05f);
            float angle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;

            Assert.That(Mathf.Abs(angle), Is.GreaterThan(170f));
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void SmoothFacing_TwoHalfSteps_EqualsOneFullStep()
        {
            Vector2 fullStep = VisionFogRules.SmoothFacing(Vector2.right, Vector2.up, 10f, 0.1f);
            Vector2 firstHalf = VisionFogRules.SmoothFacing(Vector2.right, Vector2.up, 10f, 0.05f);
            Vector2 secondHalf = VisionFogRules.SmoothFacing(firstHalf, Vector2.up, 10f, 0.05f);

            AssertVector(secondHalf, fullStep);
        }

        [Test]
        public void StepClearStrength_WithViewer_RisesByDeltaTimeOverFade()
        {
            Assert.That(VisionFogRules.StepClearStrength(0.2f, true, 2f, 0.5f), Is.EqualTo(0.45f).Within(Tolerance));
        }

        [Test]
        public void StepClearStrength_WithoutViewer_FallsByDeltaTimeOverFade()
        {
            Assert.That(VisionFogRules.StepClearStrength(0.8f, false, 2f, 0.5f), Is.EqualTo(0.55f).Within(Tolerance));
        }

        [TestCase(0.9f, true, 1f)]
        [TestCase(0.1f, false, 0f)]
        public void StepClearStrength_Overshoot_ClampsAtEndpoints(float current, bool hasViewer, float expected)
        {
            Assert.That(VisionFogRules.StepClearStrength(current, hasViewer, 1f, 0.5f), Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(true, 0f, 1f)]
        [TestCase(false, 0f, 0f)]
        [TestCase(true, -1f, 1f)]
        [TestCase(false, -1f, 0f)]
        public void StepClearStrength_NonPositiveFade_IsInstant(bool hasViewer, float fadeSeconds, float expected)
        {
            Assert.That(VisionFogRules.StepClearStrength(0.4f, hasViewer, fadeSeconds, 0.05f), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void OrthographicCoverage_AddsMarginOnEverySide()
        {
            AssertVector(VisionFogRules.OrthographicCoverage(5f, 16f / 9f, 1f), new Vector2(19.777778f, 12f));
        }

        [Test]
        public void OrthographicCoverage_NegativeMargin_EqualsZeroMargin()
        {
            Vector2 zeroMargin = VisionFogRules.OrthographicCoverage(5f, 16f / 9f, 0f);
            Vector2 negativeMargin = VisionFogRules.OrthographicCoverage(5f, 16f / 9f, -1f);

            AssertVector(zeroMargin, new Vector2(17.777778f, 10f));
            AssertVector(negativeMargin, zeroMargin);
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }
    }
}
