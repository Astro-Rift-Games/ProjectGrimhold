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
        public void Outward_SouthAimGetsTheFullOffsetAlongTheAimScaledByTheBlend()
        {
            Vector2 full = AimBlockRotation.Outward(new Vector2(0f, -5f), 0.4f, 1f);
            Vector2 half = AimBlockRotation.Outward(Vector2.down, 0.4f, 0.5f);

            Assert.That(full.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(full.y, Is.EqualTo(-0.4f).Within(Tolerance));
            Assert.That(half.y, Is.EqualTo(-0.2f).Within(Tolerance));
        }

        [Test]
        public void Outward_SouthEastAndSouthWestGetAPartOfTheOffset()
        {
            Vector2 diagonal = new Vector2(0.7071f, -0.7071f);

            Vector2 southEast = AimBlockRotation.Outward(diagonal, 0.4f, 1f);
            Vector2 southWest = AimBlockRotation.Outward(new Vector2(-diagonal.x, diagonal.y), 0.4f, 1f);

            // Along the aim, scaled by the southward component of the aim (0.7071).
            Assert.That(southEast.x, Is.EqualTo(diagonal.x * 0.4f * 0.7071f).Within(Tolerance));
            Assert.That(southEast.y, Is.EqualTo(diagonal.y * 0.4f * 0.7071f).Within(Tolerance));
            Assert.That(southWest.x, Is.EqualTo(-southEast.x).Within(Tolerance));
            Assert.That(southWest.y, Is.EqualTo(southEast.y).Within(Tolerance));
        }

        [Test]
        public void Outward_GrowsTowardSouthAndIsZeroFromHorizontalToNorth()
        {
            float previous = 0f;
            for (float degrees = 0f; degrees <= 90f; degrees += 5f)
            {
                float radians = (-degrees) * Mathf.Deg2Rad;
                float magnitude = AimBlockRotation.Outward(
                    new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), 0.4f, 1f).magnitude;

                Assert.That(magnitude, Is.GreaterThanOrEqualTo(previous - Tolerance), degrees.ToString());
                previous = magnitude;
            }
        }

        [TestCase(0f)]
        [TestCase(15f)]
        [TestCase(60f)]
        [TestCase(90f)]
        [TestCase(120f)]
        [TestCase(180f)]
        public void Outward_HorizontalAndNorthAimsGetExactlyZero(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;

            Vector2 outward = AimBlockRotation.Outward(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), 0.4f, 1f);

            Assert.That(outward, Is.EqualTo(Vector2.zero), degrees.ToString());
        }

        [Test]
        public void Outward_ZeroOffsetIsZeroForAnyBlend()
        {
            Assert.That(AimBlockRotation.Outward(Vector2.down, 0f, 1f), Is.EqualTo(Vector2.zero));
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(2f, 1f)]
        public void Outward_ClampsTheBlend(float blend, float expectedFraction)
        {
            Vector2 outward = AimBlockRotation.Outward(Vector2.down, 0.4f, blend);

            Assert.That(outward.y, Is.EqualTo(-0.4f * expectedFraction).Within(Tolerance));
        }

        [Test]
        public void StringHandPin_MovesTheHandGripOntoTheNockByTheWeight()
        {
            Vector2 grip = new Vector2(0.1f, 0.2f);
            Vector2 nock = new Vector2(0.5f, -0.2f);

            Assert.That(StringHandPin.Offset(grip, nock, 0f), Is.EqualTo(Vector2.zero));
            Assert.That(grip + StringHandPin.Offset(grip, nock, 1f), Is.EqualTo(nock));
            Vector2 half = grip + StringHandPin.Offset(grip, nock, 0.5f);
            Assert.That(half.x, Is.EqualTo(0.3f).Within(Tolerance));
            Assert.That(half.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(-1f)]
        [TestCase(2f)]
        public void StringHandPin_ClampsTheWeight(float weight)
        {
            Vector2 offset = StringHandPin.Offset(Vector2.zero, Vector2.right, weight);

            Assert.That(offset.x, Is.EqualTo(Mathf.Clamp01(weight)).Within(Tolerance));
        }

        [TestCase(false, 0.14f)]
        [TestCase(true, -0.14f)]
        public void StringHandPin_MirrorsTheNockWithTheWeapon(bool mirrored, float expectedX)
        {
            Vector2 nock = StringHandPin.NockForFacing(new Vector2(0.14f, -0.58f), mirrored);

            Assert.That(nock.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(nock.y, Is.EqualTo(-0.58f).Within(Tolerance));
        }

        [Test]
        public void Outward_UnusableAimGivesNoOffset()
        {
            Assert.That(AimBlockRotation.Outward(Vector2.zero, 0.4f, 1f), Is.EqualTo(Vector2.zero));
        }
    }
}
