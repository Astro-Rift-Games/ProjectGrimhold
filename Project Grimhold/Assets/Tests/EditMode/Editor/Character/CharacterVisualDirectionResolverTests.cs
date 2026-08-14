using NUnit.Framework;
using UnityEngine;

namespace ProjectGrimhold.Tests.EditMode.Character
{
    [TestFixture]
    public sealed class CharacterVisualDirectionResolverTests
    {
        private const float Tolerance = 0.0001f;

        [TestCase(0f, -1f, CharacterVisualDirection.South)]
        [TestCase(0.70710678f, -0.70710678f, CharacterVisualDirection.SouthEast)]
        [TestCase(0.70710678f, 0.70710678f, CharacterVisualDirection.NorthEast)]
        [TestCase(0f, 1f, CharacterVisualDirection.North)]
        [TestCase(-0.70710678f, 0.70710678f, CharacterVisualDirection.NorthWest)]
        [TestCase(-0.70710678f, -0.70710678f, CharacterVisualDirection.SouthWest)]
        public void Resolve_CanonicalDirections_ReturnsExpectedEnum(
            float x,
            float y,
            CharacterVisualDirection expectedDirection)
        {
            CharacterVisualDirection result =
                CharacterVisualDirectionResolver.Resolve(new Vector2(x, y));

            Assert.That(result, Is.EqualTo(expectedDirection));
        }

        [Test]
        public void Resolve_ExactEast_ResolvesToSouthEast()
        {
            CharacterVisualDirection result =
                CharacterVisualDirectionResolver.Resolve(Vector2.right);

            Assert.That(result, Is.EqualTo(CharacterVisualDirection.SouthEast));
        }

        [Test]
        public void Resolve_ExactWest_ResolvesToSouthWest()
        {
            CharacterVisualDirection result =
                CharacterVisualDirectionResolver.Resolve(Vector2.left);

            Assert.That(result, Is.EqualTo(CharacterVisualDirection.SouthWest));
        }

        [TestCase(67.4f, CharacterVisualDirection.NorthEast)]
        [TestCase(67.5f, CharacterVisualDirection.NorthEast)]
        [TestCase(67.6f, CharacterVisualDirection.North)]
        [TestCase(112.4f, CharacterVisualDirection.North)]
        [TestCase(112.5f, CharacterVisualDirection.NorthWest)]
        [TestCase(112.6f, CharacterVisualDirection.NorthWest)]
        [TestCase(-67.4f, CharacterVisualDirection.SouthEast)]
        [TestCase(-67.5f, CharacterVisualDirection.SouthEast)]
        [TestCase(-67.6f, CharacterVisualDirection.South)]
        [TestCase(-112.4f, CharacterVisualDirection.South)]
        [TestCase(-112.5f, CharacterVisualDirection.SouthWest)]
        [TestCase(-112.6f, CharacterVisualDirection.SouthWest)]
        [TestCase(0.1f, CharacterVisualDirection.NorthEast)]
        [TestCase(0f, CharacterVisualDirection.SouthEast)]
        [TestCase(-0.1f, CharacterVisualDirection.SouthEast)]
        [TestCase(179.9f, CharacterVisualDirection.NorthWest)]
        [TestCase(180f, CharacterVisualDirection.SouthWest)]
        [TestCase(-179.9f, CharacterVisualDirection.SouthWest)]
        public void Resolve_SectorBoundariesWithPerturbations_FollowsDeterministicPolicy(
            float angleDegrees,
            CharacterVisualDirection expectedDirection)
        {
            Vector2 facing = DirectionFromDegrees(angleDegrees);
            CharacterVisualDirection result =
                CharacterVisualDirectionResolver.Resolve(facing);

            Assert.That(result, Is.EqualTo(expectedDirection));
        }

        [Test]
        public void SanitizeFacing_WithoutHistory_ReturnsSouthOnInvalid()
        {
            Vector2 resultZero = CharacterVisualDirectionResolver.SanitizeFacing(Vector2.zero);
            Vector2 resultNaN = CharacterVisualDirectionResolver.SanitizeFacing(new Vector2(float.NaN, float.NaN));
            Vector2 resultInf = CharacterVisualDirectionResolver.SanitizeFacing(new Vector2(float.PositiveInfinity, 0f));

            AssertVector(resultZero, Vector2.down);
            AssertVector(resultNaN, Vector2.down);
            AssertVector(resultInf, Vector2.down);
        }

        [Test]
        public void SanitizeFacing_SequentialHistory_PreservesLastValidFacing()
        {
            // Valid NorthEast -> (0,0) -> stays NorthEast
            Vector2 validNE = new Vector2(0.7071f, 0.7071f);
            Vector2 safeNE = CharacterVisualDirectionResolver.SanitizeFacing(validNE, Vector2.down);
            AssertVector(safeNE, validNE.normalized);

            Vector2 afterZero = CharacterVisualDirectionResolver.SanitizeFacing(Vector2.zero, safeNE);
            AssertVector(afterZero, safeNE);
            Assert.That(
                CharacterVisualDirectionResolver.Resolve(afterZero),
                Is.EqualTo(CharacterVisualDirection.NorthEast));

            // Valid SouthWest -> NaN -> stays SouthWest
            Vector2 validSW = new Vector2(-0.7071f, -0.7071f);
            Vector2 safeSW = CharacterVisualDirectionResolver.SanitizeFacing(validSW, safeNE);
            AssertVector(safeSW, validSW.normalized);

            Vector2 afterNaN = CharacterVisualDirectionResolver.SanitizeFacing(
                new Vector2(float.NaN, 0f),
                safeSW);
            AssertVector(afterNaN, safeSW);
            Assert.That(
                CharacterVisualDirectionResolver.Resolve(afterNaN),
                Is.EqualTo(CharacterVisualDirection.SouthWest));

            // Valid North -> Infinity -> stays North
            Vector2 validN = Vector2.up;
            Vector2 safeN = CharacterVisualDirectionResolver.SanitizeFacing(validN, safeSW);
            AssertVector(safeN, Vector2.up);

            Vector2 afterInf = CharacterVisualDirectionResolver.SanitizeFacing(
                new Vector2(0f, float.NegativeInfinity),
                safeN);
            AssertVector(afterInf, Vector2.up);
            Assert.That(
                CharacterVisualDirectionResolver.Resolve(afterInf),
                Is.EqualTo(CharacterVisualDirection.North));
        }

        [TestCase(CharacterVisualDirection.South, true)]
        [TestCase(CharacterVisualDirection.SouthEast, true)]
        [TestCase(CharacterVisualDirection.SouthWest, true)]
        [TestCase(CharacterVisualDirection.North, false)]
        [TestCase(CharacterVisualDirection.NorthEast, false)]
        [TestCase(CharacterVisualDirection.NorthWest, false)]
        public void IsFrontFacing_And_CalculateSortingOrder_MatchFrontBackPolicy(
            CharacterVisualDirection direction,
            bool expectedFront)
        {
            bool isFront = CharacterVisualDirectionResolver.IsFrontFacing(direction);
            int order = CharacterVisualDirectionResolver.CalculateSortingOrder(direction, 10, -10);

            Assert.That(isFront, Is.EqualTo(expectedFront));
            Assert.That(order, Is.EqualTo(expectedFront ? 10 : -10));
        }

        [Test]
        public void GetCanonicalVector_ReturnsExactExpectedVectors()
        {
            AssertVector(
                CharacterVisualDirectionResolver.GetCanonicalVector(CharacterVisualDirection.South),
                new Vector2(0f, -1f));
            AssertVector(
                CharacterVisualDirectionResolver.GetCanonicalVector(CharacterVisualDirection.SouthEast),
                new Vector2(0.70710678f, -0.70710678f));
            AssertVector(
                CharacterVisualDirectionResolver.GetCanonicalVector(CharacterVisualDirection.NorthEast),
                new Vector2(0.70710678f, 0.70710678f));
            AssertVector(
                CharacterVisualDirectionResolver.GetCanonicalVector(CharacterVisualDirection.North),
                new Vector2(0f, 1f));
            AssertVector(
                CharacterVisualDirectionResolver.GetCanonicalVector(CharacterVisualDirection.NorthWest),
                new Vector2(-0.70710678f, 0.70710678f));
            AssertVector(
                CharacterVisualDirectionResolver.GetCanonicalVector(CharacterVisualDirection.SouthWest),
                new Vector2(-0.70710678f, -0.70710678f));
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }

        private static Vector2 DirectionFromDegrees(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
