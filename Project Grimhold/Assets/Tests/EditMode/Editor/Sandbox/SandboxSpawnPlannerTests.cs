using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Sandbox
{
    public sealed class SandboxSpawnPlannerTests
    {
        private const float Tolerance = 0.0001f;

        [TestCase(-5, 50, 1)]
        [TestCase(0, 50, 1)]
        [TestCase(7, 50, 7)]
        [TestCase(500, 50, 50)]
        [TestCase(10, 0, 1)]
        public void ClampCount_KeepsValueInsideOneAndMax(int count, int max, int expected)
        {
            Assert.That(SandboxSpawnPlanner.ClampCount(count, max), Is.EqualTo(expected));
        }

        [TestCase(SandboxSpawnPattern.Ring)]
        [TestCase(SandboxSpawnPattern.Grid)]
        [TestCase(SandboxSpawnPattern.Line)]
        public void Plan_ReturnsClampedCountForEveryPattern(SandboxSpawnPattern pattern)
        {
            Assert.That(SandboxSpawnPlanner.Plan(Vector2.zero, 0, pattern, 1f, 10), Has.Count.EqualTo(1));
            Assert.That(SandboxSpawnPlanner.Plan(Vector2.zero, 4, pattern, 1f, 10), Has.Count.EqualTo(4));
            Assert.That(SandboxSpawnPlanner.Plan(Vector2.zero, 99, pattern, 1f, 10), Has.Count.EqualTo(10));
        }

        [Test]
        public void Plan_SingleEntryAlwaysSitsOnCenter()
        {
            var center = new Vector2(3f, -2f);

            foreach (SandboxSpawnPattern pattern in System.Enum.GetValues(typeof(SandboxSpawnPattern)))
            {
                IReadOnlyList<Vector2> positions = SandboxSpawnPlanner.Plan(center, 1, pattern, 2f, 10);

                Assert.That(positions[0], Is.EqualTo(center), pattern.ToString());
            }
        }

        [Test]
        public void Plan_Line_IsHorizontalAndCenteredWithSpacing()
        {
            IReadOnlyList<Vector2> positions =
                SandboxSpawnPlanner.Plan(new Vector2(10f, 5f), 3, SandboxSpawnPattern.Line, 2f, 10);

            Assert.That(positions[0], Is.EqualTo(new Vector2(8f, 5f)));
            Assert.That(positions[1], Is.EqualTo(new Vector2(10f, 5f)));
            Assert.That(positions[2], Is.EqualTo(new Vector2(12f, 5f)));
        }

        [Test]
        public void Plan_Grid_FillsRowsAndIsCenteredOnCenter()
        {
            IReadOnlyList<Vector2> positions =
                SandboxSpawnPlanner.Plan(Vector2.zero, 4, SandboxSpawnPattern.Grid, 2f, 10);

            Assert.That(positions[0], Is.EqualTo(new Vector2(-1f, 1f)));
            Assert.That(positions[1], Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(positions[2], Is.EqualTo(new Vector2(-1f, -1f)));
            Assert.That(positions[3], Is.EqualTo(new Vector2(1f, -1f)));
        }

        [Test]
        public void Plan_Grid_WithPartialLastRowKeepsSpacing()
        {
            IReadOnlyList<Vector2> positions =
                SandboxSpawnPlanner.Plan(Vector2.zero, 5, SandboxSpawnPattern.Grid, 1.5f, 10);

            Assert.That(positions, Has.Count.EqualTo(5));
            Assert.That(MinPairDistance(positions), Is.GreaterThanOrEqualTo(1.5f - Tolerance));
        }

        [TestCase(2, 1f)]
        [TestCase(8, 1f)]
        [TestCase(24, 2.5f)]
        public void Plan_Ring_PutsEntriesOnOneCircleWithoutOvercrowding(int count, float spacing)
        {
            var center = new Vector2(1f, 1f);

            IReadOnlyList<Vector2> positions =
                SandboxSpawnPlanner.Plan(center, count, SandboxSpawnPattern.Ring, spacing, 50);

            float radius = Vector2.Distance(center, positions[0]);
            foreach (Vector2 position in positions)
            {
                Assert.That(Vector2.Distance(center, position), Is.EqualTo(radius).Within(Tolerance));
            }

            Assert.That(radius, Is.GreaterThanOrEqualTo(spacing - Tolerance));
            Assert.That(MinPairDistance(positions), Is.GreaterThanOrEqualTo(spacing - Tolerance));
        }

        [TestCase(0f)]
        [TestCase(-3f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Plan_InvalidSpacing_FallsBackToMinimumAndStaysFinite(float spacing)
        {
            foreach (SandboxSpawnPattern pattern in System.Enum.GetValues(typeof(SandboxSpawnPattern)))
            {
                IReadOnlyList<Vector2> positions = SandboxSpawnPlanner.Plan(Vector2.zero, 6, pattern, spacing, 10);

                Assert.That(MinPairDistance(positions), Is.GreaterThanOrEqualTo(SandboxSpawnPlanner.MinimumSpacing - Tolerance), pattern.ToString());
                foreach (Vector2 position in positions)
                {
                    Assert.That(float.IsFinite(position.x) && float.IsFinite(position.y), Is.True, pattern.ToString());
                }
            }
        }

        private static float MinPairDistance(IReadOnlyList<Vector2> positions)
        {
            float min = float.MaxValue;
            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = i + 1; j < positions.Count; j++)
                {
                    min = Mathf.Min(min, Vector2.Distance(positions[i], positions[j]));
                }
            }

            return min;
        }
    }
}
