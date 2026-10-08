using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class AimBlockRotationTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Apply_RotatesAPointRigidlyAboutThePivot()
        {
            AimBlockRotation.Apply(new Vector2(1f, 0.1f), 10f, new Vector2(0f, 0.1f), 90f, Vector2.zero,
                out Vector2 position, out float rotation);

            Assert.That(position.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(position.y, Is.EqualTo(1.1f).Within(Tolerance));
            Assert.That(rotation, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void Apply_KeepsTheDistanceToThePivot()
        {
            Vector2 pivot = new Vector2(0f, 0.1f);
            Vector2 point = new Vector2(0.4f, -0.7f);

            AimBlockRotation.Apply(point, 0f, pivot, 37f, Vector2.zero, out Vector2 position, out _);

            Assert.That(Vector2.Distance(position, pivot), Is.EqualTo(Vector2.Distance(point, pivot)).Within(Tolerance));
        }

        [Test]
        public void Apply_KeepsTheRelativeLayoutOfTwoPointsInTheBlock()
        {
            Vector2 pivot = new Vector2(0f, 0.1f);
            Vector2 hand = new Vector2(-0.37f, -0.46f);
            Vector2 weapon = new Vector2(0f, -0.71f);

            AimBlockRotation.Apply(hand, 0f, pivot, -50f, Vector2.zero, out Vector2 rotatedHand, out _);
            AimBlockRotation.Apply(weapon, 0f, pivot, -50f, Vector2.zero, out Vector2 rotatedWeapon, out _);

            Assert.That(Vector2.Distance(rotatedHand, rotatedWeapon),
                Is.EqualTo(Vector2.Distance(hand, weapon)).Within(Tolerance));
        }

        [Test]
        public void Apply_ZeroResidualAndNoOffsetIsTheIdentity()
        {
            Vector2 point = new Vector2(0.4f, -0.7f);

            AimBlockRotation.Apply(point, 33f, new Vector2(0f, 0.1f), 0f, Vector2.zero,
                out Vector2 position, out float rotation);

            Assert.That(position, Is.EqualTo(point));
            Assert.That(rotation, Is.EqualTo(33f));
        }

        [Test]
        public void Apply_OutwardOffsetTranslatesTheWholeBlock()
        {
            AimBlockRotation.Apply(new Vector2(0.4f, -0.7f), 0f, new Vector2(0f, 0.1f), 0f, new Vector2(0.2f, 0f),
                out Vector2 position, out _);

            Assert.That(position.x, Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(position.y, Is.EqualTo(-0.7f).Within(Tolerance));
        }

        [Test]
        public void Outward_PointsAlongTheAimScaledByTheBlend()
        {
            Vector2 full = AimBlockRotation.Outward(new Vector2(0f, 5f), 0.4f, 1f);
            Vector2 half = AimBlockRotation.Outward(Vector2.up, 0.4f, 0.5f);

            Assert.That(full.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(full.y, Is.EqualTo(0.4f).Within(Tolerance));
            Assert.That(half.y, Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void Outward_ZeroOffsetIsZeroForAnyBlend()
        {
            Assert.That(AimBlockRotation.Outward(Vector2.right, 0f, 1f), Is.EqualTo(Vector2.zero));
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(2f, 1f)]
        public void Outward_ClampsTheBlend(float blend, float expectedFraction)
        {
            Vector2 outward = AimBlockRotation.Outward(Vector2.right, 0.4f, blend);

            Assert.That(outward.x, Is.EqualTo(0.4f * expectedFraction).Within(Tolerance));
        }

        [Test]
        public void Outward_UnusableAimGivesNoOffset()
        {
            Assert.That(AimBlockRotation.Outward(Vector2.zero, 0.4f, 1f), Is.EqualTo(Vector2.zero));
        }
    }
}
