using NUnit.Framework;
using UnityEngine;
using Grimhold.Combat.Presentation;

namespace Grimhold.Tests.Combat.Presentation
{
    [TestFixture]
    public class CombatVfxPresentationMathTests
    {
        [Test]
        public void CalculateRotation_ReturnsExpectedAngles()
        {
            float rotationOffset = 15f;
            Assert.AreEqual(105f, CombatVfxPresentationMath.CalculateRotation(CharacterVisualDirection.North, rotationOffset));
            Assert.AreEqual(60f, CombatVfxPresentationMath.CalculateRotation(CharacterVisualDirection.NorthEast, rotationOffset));
            Assert.AreEqual(-30f, CombatVfxPresentationMath.CalculateRotation(CharacterVisualDirection.SouthEast, rotationOffset));
            Assert.AreEqual(-75f, CombatVfxPresentationMath.CalculateRotation(CharacterVisualDirection.South, rotationOffset));
            Assert.AreEqual(-120f, CombatVfxPresentationMath.CalculateRotation(CharacterVisualDirection.SouthWest, rotationOffset));
            Assert.AreEqual(150f, CombatVfxPresentationMath.CalculateRotation(CharacterVisualDirection.NorthWest, rotationOffset));
        }

        [Test]
        public void ShouldMirror_ReturnsTrueForWesternDirections()
        {
            Assert.IsTrue(CombatVfxPresentationMath.ShouldMirror(CharacterVisualDirection.NorthWest));
            Assert.IsTrue(CombatVfxPresentationMath.ShouldMirror(CharacterVisualDirection.SouthWest));

            Assert.IsFalse(CombatVfxPresentationMath.ShouldMirror(CharacterVisualDirection.North));
            Assert.IsFalse(CombatVfxPresentationMath.ShouldMirror(CharacterVisualDirection.NorthEast));
            Assert.IsFalse(CombatVfxPresentationMath.ShouldMirror(CharacterVisualDirection.SouthEast));
            Assert.IsFalse(CombatVfxPresentationMath.ShouldMirror(CharacterVisualDirection.South));
        }

        [Test]
        public void GetOrientedOffset_RotatesLocalOffsetCorrectly()
        {
            Vector2 canonicalDirection = new Vector2(0f, 1f); // North
            Vector2 localOffset = new Vector2(1f, 1f);
            
            Vector2 result = CombatVfxPresentationMath.GetOrientedOffset(canonicalDirection, localOffset);
            
            // right = (0, 1), up = (-1, 0)
            // result = (0, 1) * 1 + (-1, 0) * 1 = (-1, 1)
            Assert.AreEqual(new Vector2(-1f, 1f), result);
        }
    }
}
