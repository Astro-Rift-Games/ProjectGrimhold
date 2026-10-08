using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Tests.EditMode.Editor.Player.Movement
{
    [TestFixture]
    public sealed class Kinematic2DMovementMotorTests
    {
        private const string WorldCollisionLayerName = "WorldCollision";

        private GameObject _wallObject;
        private BoxCollider2D _wallCollider;
        private GameObject _moverObject;
        private BoxCollider2D _moverCollider;
        private Kinematic2DMovementMotor _motor;

        [SetUp]
        public void SetUp()
        {
            int worldLayer = LayerMask.NameToLayer(WorldCollisionLayerName);
            Assert.GreaterOrEqual(worldLayer, 0, "WorldCollision layer must exist.");

            _wallObject = new GameObject("Wall") { layer = worldLayer };
            Rigidbody2D wallBody = _wallObject.AddComponent<Rigidbody2D>();
            wallBody.bodyType = RigidbodyType2D.Static;
            _wallCollider = _wallObject.AddComponent<BoxCollider2D>();
            _wallCollider.size = new Vector2(2f, 2f);
            _wallObject.transform.position = Vector3.zero;

            _moverObject = new GameObject("Mover");
            Rigidbody2D moverBody = _moverObject.AddComponent<Rigidbody2D>();
            moverBody.bodyType = RigidbodyType2D.Kinematic;
            moverBody.gravityScale = 0f;
            _moverCollider = _moverObject.AddComponent<BoxCollider2D>();
            _moverCollider.size = Vector2.one;
            _motor = _moverObject.AddComponent<Kinematic2DMovementMotor>();

            SetField("_rigidbody", moverBody);
            SetField("_collider", _moverCollider);
            SetField("_collisionMask", (LayerMask)(1 << worldLayer));
        }

        [TearDown]
        public void TearDown()
        {
            if (_moverObject != null)
            {
                Object.DestroyImmediate(_moverObject);
            }

            if (_wallObject != null)
            {
                Object.DestroyImmediate(_wallObject);
            }
        }

        [Test]
        public void Move_StartingOverlappingWall_EndsOutsideWall()
        {
            PlaceMover(new Vector2(0.8f, 0f));
            Assume.That(IsOverlappingWall(), Is.True);

            _motor.Move(new Vector2(0.1f, 0f));
            Physics2D.SyncTransforms();

            Assert.IsFalse(IsOverlappingWall());
        }

        [Test]
        public void Move_ZeroDisplacementWhileOverlapping_StillDepenetrates()
        {
            PlaceMover(new Vector2(0.8f, 0f));
            Assume.That(IsOverlappingWall(), Is.True);

            _motor.Move(Vector2.zero);
            Physics2D.SyncTransforms();

            Assert.IsFalse(IsOverlappingWall());
            // Shallowest exit is to the right: wall right edge x = 1, mover half width 0.5.
            Assert.GreaterOrEqual(_moverObject.transform.position.x, 1.5f);
        }

        [Test]
        public void Move_StartingOverlapping_ReturnedDisplacementExcludesDepenetration()
        {
            Vector2 start = new Vector2(0.8f, 0f);
            PlaceMover(start);

            Vector2 returned = _motor.Move(Vector2.zero);

            Assert.AreEqual(Vector2.zero, returned);
            Assert.AreNotEqual(start.x, _moverObject.transform.position.x);
        }

        [Test]
        public void Move_TowardWall_StopsBeforeContact()
        {
            PlaceMover(new Vector2(3f, 0f));
            Assume.That(IsOverlappingWall(), Is.False);

            _motor.Move(new Vector2(-3f, 0f));
            Physics2D.SyncTransforms();

            Assert.IsFalse(IsOverlappingWall());
            Assert.Less(_moverObject.transform.position.x, 3f);
            // Wall right edge is x = 1; mover half width is 0.5.
            Assert.GreaterOrEqual(_moverObject.transform.position.x, 1.5f);
        }

        private void PlaceMover(Vector2 position)
        {
            _moverObject.transform.position = position;
            Physics2D.SyncTransforms();
        }

        private bool IsOverlappingWall()
        {
            Physics2D.SyncTransforms();
            return _moverCollider.Distance(_wallCollider).isOverlapped;
        }

        private void SetField(string name, object value)
        {
            FieldInfo field = typeof(Kinematic2DMovementMotor).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"Field {name} not found.");
            field.SetValue(_motor, value);
        }
    }
}
