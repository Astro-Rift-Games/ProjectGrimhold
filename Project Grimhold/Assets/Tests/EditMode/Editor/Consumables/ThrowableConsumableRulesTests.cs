#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Consumables
{
    [TestFixture]
    public sealed class ThrowableConsumableRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            SmokeZoneRegistry.Clear();
        }

        [Test]
        public void SmokeZoneRegistry_RegistrationAndClear_LifecycleWorks()
        {
            SmokeZoneRegistry.Clear();
            Assert.That(SmokeZoneRegistry.ActiveZones.Count, Is.EqualTo(0));

            var zone = new TestSmokeZone(new Vector2(0f, 0f), 3.0f, true);
            SmokeZoneRegistry.Register(zone);
            Assert.That(SmokeZoneRegistry.ActiveZones.Count, Is.EqualTo(1));

            // Idempotent registration
            SmokeZoneRegistry.Register(zone);
            Assert.That(SmokeZoneRegistry.ActiveZones.Count, Is.EqualTo(1));

            SmokeZoneRegistry.Unregister(zone);
            Assert.That(SmokeZoneRegistry.ActiveZones.Count, Is.EqualTo(0));

            SmokeZoneRegistry.Register(zone);
            SmokeZoneRegistry.Clear();
            Assert.That(SmokeZoneRegistry.ActiveZones.Count, Is.EqualTo(0));
        }

        [Test]
        public void IsPointInSmoke_ReturnsTrueOnlyWithinRadius()
        {
            var zone = new TestSmokeZone(new Vector2(5f, 5f), 3.0f, true);
            SmokeZoneRegistry.Register(zone);

            Assert.That(SmokeZoneRegistry.IsPointInSmoke(new Vector2(5f, 5f)), Is.True, "Center is inside.");
            Assert.That(SmokeZoneRegistry.IsPointInSmoke(new Vector2(5f, 7.9f)), Is.True, "Distance 2.9m is inside.");
            Assert.That(SmokeZoneRegistry.IsPointInSmoke(new Vector2(5f, 8.0f)), Is.True, "Distance 3.0m is on boundary.");
            Assert.That(SmokeZoneRegistry.IsPointInSmoke(new Vector2(5f, 8.1f)), Is.False, "Distance 3.1m is outside.");
            Assert.That(SmokeZoneRegistry.IsPointInSmoke(new Vector2(0f, 0f)), Is.False, "Distant point is outside.");
        }

        [Test]
        public void IsLineOccluded_CrossingSegment_IsBlocked()
        {
            // Smoke zone centered at (5, 0) with radius 3m (spans x: 2..8, y: -3..3)
            var zone = new TestSmokeZone(new Vector2(5f, 0f), 3.0f, true);
            SmokeZoneRegistry.Register(zone);

            // Segment from (0, 0) to (10, 0) passes directly through center
            Assert.That(SmokeZoneRegistry.IsLineOccluded(new Vector2(0f, 0f), new Vector2(10f, 0f)), Is.True);

            // Segment tangential/close to edge: from (0, 2.5) to (10, 2.5) passes through at y=2.5 <= 3.0
            Assert.That(SmokeZoneRegistry.IsLineOccluded(new Vector2(0f, 2.5f), new Vector2(10f, 2.5f)), Is.True);

            // Segment passing above the smoke: from (0, 4) to (10, 4) passes at y=4 > 3.0
            Assert.That(SmokeZoneRegistry.IsLineOccluded(new Vector2(0f, 4f), new Vector2(10f, 4f)), Is.False);
        }

        [Test]
        public void IsLineOccluded_LookingIntoOrOutOfSmoke_IsBlocked()
        {
            var zone = new TestSmokeZone(new Vector2(5f, 0f), 3.0f, true);
            SmokeZoneRegistry.Register(zone);

            // Viewer outside (0, 0), target inside (5, 0)
            Assert.That(SmokeZoneRegistry.IsLineOccluded(new Vector2(0f, 0f), new Vector2(5f, 0f)), Is.True);

            // Viewer inside (5, 0), target outside (10, 0)
            Assert.That(SmokeZoneRegistry.IsLineOccluded(new Vector2(5f, 0f), new Vector2(10f, 0f)), Is.True);
        }

        [Test]
        public void IsLineOccluded_BothInsideSmoke_PreservesPointBlankVisionOnly()
        {
            var zone = new TestSmokeZone(new Vector2(5f, 0f), 3.0f, true);
            SmokeZoneRegistry.Register(zone);

            // Both inside, 0.8m apart (<= 1.2m close vision distance)
            Assert.That(SmokeZoneRegistry.IsLineOccluded(new Vector2(5f, 0f), new Vector2(5.8f, 0f)), Is.False);

            // Both inside, 1.2m apart (<= 1.2m)
            Assert.That(SmokeZoneRegistry.IsLineOccluded(new Vector2(5f, 0f), new Vector2(6.2f, 0f)), Is.False);

            // Both inside, 2.0m apart (> 1.2m close vision distance): blocked by thick smoke
            Assert.That(SmokeZoneRegistry.IsLineOccluded(new Vector2(4f, 0f), new Vector2(6f, 0f)), Is.True);
        }

        [Test]
        public void CastOcclusion_ClipsRayDistanceAtSmokePerimeter()
        {
            // Smoke at (5, 0), radius 3m => edge facing x=0 is at x=2.0
            var zone = new TestSmokeZone(new Vector2(5f, 0f), 3.0f, true);
            SmokeZoneRegistry.Register(zone);

            // Ray from (0, 0) towards right (1, 0) with max distance 10
            float hitDist = SmokeZoneRegistry.CastOcclusion(new Vector2(0f, 0f), Vector2.right, 10f);
            Assert.That(hitDist, Is.EqualTo(2.0f).Within(0.001f));

            // Ray missing smoke: direction up (0, 1)
            float missDist = SmokeZoneRegistry.CastOcclusion(new Vector2(0f, 0f), Vector2.up, 10f);
            Assert.That(missDist, Is.EqualTo(10f));

            // Ray originating inside smoke: capped to 1.2m
            float insideDist = SmokeZoneRegistry.CastOcclusion(new Vector2(5f, 0f), Vector2.right, 10f);
            Assert.That(insideDist, Is.EqualTo(1.2f).Within(0.001f));
        }

        private sealed class TestSmokeZone : ISmokeZone
        {
            public Vector2 Position { get; }
            public float Radius { get; }
            public bool IsActive { get; }

            public TestSmokeZone(Vector2 position, float radius, bool isActive)
            {
                Position = position;
                Radius = radius;
                IsActive = isActive;
            }

            public bool ContainsPoint(Vector2 point)
            {
                return (point - Position).sqrMagnitude <= Radius * Radius;
            }

            public bool BlocksLine(Vector2 start, Vector2 end, float closeVisionDistance = 1.2f)
            {
                return SmokeZoneRegistry.CircleBlocksLine(Position, Radius, start, end, closeVisionDistance);
            }
        }
    }
}
#endif
