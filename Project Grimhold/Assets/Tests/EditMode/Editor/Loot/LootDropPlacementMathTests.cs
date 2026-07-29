using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class LootDropPlacementMathTests
    {
        [Test]
        public void Candidates_FollowStableFacingRelativeOrder()
        {
            Vector2 origin = new(3f, 4f);
            Vector2[] expectedDirections =
            {
                Vector2.up,
                new Vector2(-1f, 1f).normalized,
                new Vector2(1f, 1f).normalized,
                Vector2.left,
                Vector2.right,
                new Vector2(-1f, -1f).normalized,
                new Vector2(1f, -1f).normalized,
                Vector2.down
            };

            for (int i = 0; i < expectedDirections.Length; i++)
            {
                Vector2 candidate = LootDropPlacementMath.GetCandidate(origin, Vector2.up, 0.75f, i);
                Assert.That(
                    Vector2.Distance(candidate, origin + expectedDirections[i] * 0.75f),
                    Is.LessThan(0.0001f));
            }
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(0f, 0f)]
        public void InvalidFacing_UsesDownFallback(float x, float y)
        {
            Vector2 candidate = LootDropPlacementMath.GetCandidate(
                Vector2.zero,
                new Vector2(x, y),
                1f,
                0);

            Assert.That(candidate, Is.EqualTo(Vector2.down));
        }
    }
}
