using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Editor.Scenario
{
    [TestFixture]
    public sealed class ExtractionZoneTests
    {
        private GameObject _zoneObject;
        private BoxCollider2D _collider;
        private ExtractionZone _zone;

        [SetUp]
        public void SetUp()
        {
            _zoneObject = new GameObject("TestExtractionZone");
            _collider = _zoneObject.AddComponent<BoxCollider2D>();
            _collider.offset = Vector2.zero;
            _collider.size = new Vector2(2f, 2f); // Bounds [-1, 1] on x and y
            _zone = _zoneObject.AddComponent<ExtractionZone>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_zoneObject != null)
            {
                Object.DestroyImmediate(_zoneObject);
            }
        }

        [Test]
        public void IsAvailable_BeforeFusionSpawn_UsesConfiguredInitialValue()
        {
            Assert.DoesNotThrow(() => _ = _zone.IsAvailable);
            Assert.IsTrue(_zone.IsAvailable);
        }

        [Test]
        public void ContainsExact_InsidePoint_ReturnsTrue()
        {
            Vector2 insidePoint = new Vector2(0f, 0f);
            Assert.IsTrue(_zone.ContainsExact(insidePoint));
        }

        [Test]
        public void ContainsExact_OutsidePoint_ReturnsFalse()
        {
            Vector2 outsidePoint = new Vector2(5f, 5f);
            Assert.IsFalse(_zone.ContainsExact(outsidePoint));
        }

        [Test]
        public void ContainsExact_NonFinitePoint_ReturnsFalse()
        {
            Vector2 nanPoint = new Vector2(float.NaN, 0f);
            Vector2 infPoint = new Vector2(float.PositiveInfinity, 0f);

            Assert.IsFalse(_zone.ContainsExact(nanPoint));
            Assert.IsFalse(_zone.ContainsExact(infPoint));
        }

        [Test]
        public void ContainsExact_DisabledCollider_ReturnsFalse()
        {
            _collider.enabled = false;
            Vector2 insidePoint = new Vector2(0f, 0f);

            Assert.IsFalse(_zone.ContainsExact(insidePoint));
        }

        [Test]
        public void ContainsWithTolerance_PointWithinBuffer_ReturnsTrue()
        {
            // Boundary is x=1. Point at x=1.3 is within tolerance=0.5
            Vector2 pointWithinTolerance = new Vector2(1.3f, 0f);

            Assert.IsTrue(_zone.ContainsWithTolerance(pointWithinTolerance, 0.5f));
        }

        [Test]
        public void ContainsWithTolerance_PointOutsideBuffer_ReturnsFalse()
        {
            // Boundary is x=1. Point at x=1.7 is outside tolerance=0.5
            Vector2 pointOutsideTolerance = new Vector2(1.7f, 0f);

            Assert.IsFalse(_zone.ContainsWithTolerance(pointOutsideTolerance, 0.5f));
        }

        [Test]
        public void ContainsWithTolerance_InvalidTolerance_ReturnsFalse()
        {
            Vector2 pointWithinTolerance = new Vector2(1.2f, 0f);

            Assert.IsFalse(_zone.ContainsWithTolerance(pointWithinTolerance, -0.5f));
            Assert.IsFalse(_zone.ContainsWithTolerance(pointWithinTolerance, float.NaN));
            Assert.IsFalse(_zone.ContainsWithTolerance(pointWithinTolerance, float.PositiveInfinity));
        }
    }
}
