using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;
using UnityEngine;

namespace Tests.EditMode.Visibility
{
    public sealed class VisionConeRulesTests
    {
        private const float NearRadius = 2f;
        private const float ConeAngleDegrees = 90f;

        private static readonly Vector2 Origin = new Vector2(3f, -1f);
        private static readonly Vector2 FacingRight = Vector2.right;

        [Test]
        public void PointInsideConeEdge_IsVisible_AndJustOutside_IsNot()
        {
            Vector2 insideEdge = Origin + Rotate(Vector2.right, 44f) * 6f;
            Vector2 outsideEdge = Origin + Rotate(Vector2.right, 46f) * 6f;

            Assert.That(IsInShape(FacingRight, insideEdge), Is.True);
            Assert.That(IsInShape(FacingRight, outsideEdge), Is.False);
        }

        [Test]
        public void PointBehind_InsideNearRadius_IsVisible()
        {
            Assert.That(IsInShape(FacingRight, Origin + Vector2.left * 1.5f), Is.True);
        }

        [Test]
        public void PointBehind_OutsideNearRadius_IsNotVisible()
        {
            Assert.That(IsInShape(FacingRight, Origin + Vector2.left * 2.5f), Is.False);
        }

        [Test]
        public void PointInsideAngle_AtAnyDistance_IsVisible()
        {
            Assert.That(IsInShape(FacingRight, Origin + Vector2.right * 10.1f), Is.True);
            Assert.That(IsInShape(FacingRight, Origin + Vector2.right * 500f), Is.True);
        }

        [Test]
        public void PointAtOrigin_IsVisible_EvenWithZeroNearRadius()
        {
            float cosine = VisionConeRules.CosineOfHalfAngle(ConeAngleDegrees);

            Assert.That(
                VisionConeRules.IsWithinVisionShape(Origin, FacingRight, Origin, 0f, cosine),
                Is.True);
        }

        [Test]
        public void ZeroFacing_FallsBackToProvidedDirection()
        {
            Vector2 resolved = VisionConeRules.ResolveFacing(Vector2.zero, Vector2.down);

            Assert.That(resolved, Is.EqualTo(Vector2.down));
            Assert.That(IsInShape(resolved, Origin + Vector2.down * 5f), Is.True);
            Assert.That(IsInShape(resolved, Origin + Vector2.up * 5f), Is.False);
        }

        [Test]
        public void NonNormalizedFacing_IsNormalized()
        {
            Vector2 resolved = VisionConeRules.ResolveFacing(new Vector2(0f, 4f), Vector2.down);

            Assert.That(resolved.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(resolved.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void FullCircleCone_SeesEveryDirection()
        {
            float cosine = VisionConeRules.CosineOfHalfAngle(360f);

            Assert.That(
                VisionConeRules.IsWithinVisionShape(Origin, FacingRight, Origin + Vector2.left * 8f, 0f, cosine),
                Is.True);
        }

        private static bool IsInShape(Vector2 facing, Vector2 point)
        {
            float cosine = VisionConeRules.CosineOfHalfAngle(ConeAngleDegrees);
            return VisionConeRules.IsWithinVisionShape(Origin, facing, point, NearRadius, cosine);
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }
    }
}
