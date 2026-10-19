using System;
using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;

namespace Tests.EditMode.Visibility
{
    public sealed class VisionFogFanRulesTests
    {
        private const float Tolerance = 0.0001f;
        // Independent of MaxRayCount so its RED stub cannot cause indexing failures.
        private readonly float[] _angles = new float[512];
        private readonly bool[] _inCone = new bool[512];

        [Test]
        public void NinetyDegreeCone_HasExactBoundaryPairs()
        {
            int count = Build(0f, 90f);

            Assert.That(count, Is.EqualTo(119));
            AssertRay(0, -45f, true);
            AssertRay(90, 45f, true);
            AssertRay(91, 45f, false);
            AssertRay(count - 1, 315f, false);
        }

        [Test]
        public void RayAngles_IncreaseExceptBoundaryPair_AndRespectSteps()
        {
            int count = Build(0f, 90f);

            Assert.That(count, Is.EqualTo(119));
            for (int i = 1; i < count; i++)
            {
                float difference = _angles[i] - _angles[i - 1];
                if (i == 91)
                {
                    Assert.That(difference, Is.EqualTo(0f).Within(Tolerance));
                    continue;
                }

                Assert.That(difference, Is.GreaterThan(0f));
                float step = _inCone[i] ? 1f : 10f;
                Assert.That(difference, Is.LessThanOrEqualTo(step + Tolerance));
            }
        }

        [Test]
        public void FacingNinety_ShiftsEveryAngleByNinety()
        {
            int count = Build(0f, 90f);
            Assert.That(count, Is.EqualTo(119));
            var shiftedAngles = new float[512];
            var shiftedInCone = new bool[512];

            int shiftedCount = VisionFogFanRules.BuildRayAngles(
                90f, 90f, 1f, 10f, shiftedAngles, shiftedInCone);

            Assert.That(shiftedCount, Is.EqualTo(count));
            for (int i = 0; i < count; i++)
            {
                Assert.That(shiftedAngles[i], Is.EqualTo(_angles[i] + 90f).Within(Tolerance));
                Assert.That(shiftedInCone[i], Is.EqualTo(_inCone[i]));
            }
        }

        [Test]
        public void FullCircleCone_ContainsOnlyConeRays()
        {
            int count = Build(0f, 360f);

            Assert.That(count, Is.EqualTo(361));
            AssertRay(0, -180f, true);
            AssertRay(count - 1, 180f, true);
            for (int i = 0; i < count; i++)
            {
                Assert.That(_inCone[i], Is.True);
            }
        }

        [Test]
        public void ZeroCone_ContainsOnlyOuterRays_WithoutClosingDuplicate()
        {
            int count = Build(0f, 0f);

            Assert.That(count, Is.EqualTo(36));
            for (int i = 0; i < count; i++)
            {
                AssertRay(i, i * 10f, false);
                Assert.That(_angles[i], Is.GreaterThanOrEqualTo(0f).And.LessThan(360f));
            }
        }

        [TestCase(0f, 36)]
        [TestCase(90f, 119)]
        [TestCase(359f, 362)]
        [TestCase(360f, 361)]
        public void MaxRayCount_BoundsEveryConeLayout(float coneAngle, int expectedCount)
        {
            int capacity = VisionFogFanRules.MaxRayCount(1f, 10f);
            int count = Build(0f, coneAngle);

            Assert.That(capacity, Is.EqualTo(398));
            Assert.That(count, Is.EqualTo(expectedCount));
            Assert.That(capacity, Is.GreaterThanOrEqualTo(count));
        }

        [Test]
        public void ShortAngleBuffer_ThrowsArgumentException()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                VisionFogFanRules.BuildRayAngles(0f, 90f, 1f, 10f, new float[118], _inCone));

            Assert.That(exception.ParamName, Is.EqualTo("anglesDegrees"));
        }

        [Test]
        public void ShortMembershipBuffer_ThrowsArgumentException()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                VisionFogFanRules.BuildRayAngles(0f, 90f, 1f, 10f, _angles, new bool[118]));

            Assert.That(exception.ParamName, Is.EqualTo("inCone"));
        }

        [Test]
        public void NullAngleBuffer_ThrowsArgumentException()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                VisionFogFanRules.BuildRayAngles(0f, 90f, 1f, 10f, null, _inCone));

            Assert.That(exception.ParamName, Is.EqualTo("anglesDegrees"));
        }

        [Test]
        public void NullMembershipBuffer_ThrowsArgumentException()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                VisionFogFanRules.BuildRayAngles(0f, 90f, 1f, 10f, _angles, null));

            Assert.That(exception.ParamName, Is.EqualTo("inCone"));
        }

        private int Build(float facing, float coneAngle)
        {
            return VisionFogFanRules.BuildRayAngles(facing, coneAngle, 1f, 10f, _angles, _inCone);
        }

        private void AssertRay(int index, float angle, bool inCone)
        {
            Assert.That(_angles[index], Is.EqualTo(angle).Within(Tolerance));
            Assert.That(_inCone[index], Is.EqualTo(inCone));
        }
    }
}
