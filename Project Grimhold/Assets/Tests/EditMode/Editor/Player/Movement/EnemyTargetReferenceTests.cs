using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Tests.EditMode.Editor.Player.Movement
{
    [TestFixture]
    public sealed class EnemyTargetReferenceTests
    {
        private GameObject _enemyObject;
        private EnemyMovementAIController _controller;
        private GameObject _targetObject;

        [SetUp]
        public void SetUp()
        {
            _enemyObject = new GameObject("Enemy");
            _enemyObject.AddComponent<BoxCollider2D>();
            _enemyObject.AddComponent<Rigidbody2D>();
            _enemyObject.AddComponent<Kinematic2DMovementMotor>();
            _controller = _enemyObject.AddComponent<EnemyMovementAIController>();

            _targetObject = new GameObject("Target");
        }

        [TearDown]
        public void TearDown()
        {
            if (_enemyObject != null)
            {
                Object.DestroyImmediate(_enemyObject);
            }
            if (_targetObject != null)
            {
                Object.DestroyImmediate(_targetObject);
            }
        }

        [Test]
        public void DefaultController_HasNoTarget()
        {
            bool hasTarget = _controller.TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform);

            Assert.IsFalse(hasTarget);
            Assert.AreEqual(0, targetId.Value);
            Assert.IsNull(targetTransform);
        }

        [Test]
        public void SetTarget_AssignsIdAndTransformTogether()
        {
            EntityId id = new EntityId(42);
            SetTarget(id, _targetObject.transform);

            bool hasTarget = _controller.TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform);

            Assert.IsTrue(hasTarget);
            Assert.AreEqual(id, targetId);
            Assert.AreEqual(_targetObject.transform, targetTransform);
        }

        [Test]
        public void InvalidIdentity_ReturnsFalseEvenWhenTransformIsCached()
        {
            SetTarget(default, _targetObject.transform);

            bool hasTarget = _controller.TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform);

            Assert.IsFalse(hasTarget);
            Assert.AreEqual(0, targetId.Value);
            Assert.AreEqual(_targetObject.transform, targetTransform);
        }

        [Test]
        public void DestroyedTargetTransform_ReturnsFalseHasTargetWithValidStoredId()
        {
            EntityId id = new EntityId(42);
            SetTarget(id, _targetObject.transform);

            Object.DestroyImmediate(_targetObject);
            _targetObject = null;

            bool hasTarget = _controller.TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform);

            Assert.IsFalse(hasTarget);
            Assert.AreEqual(id, targetId); // ID remains stored as expected for structural invalidation
            Assert.IsNull(targetTransform);
        }

        [Test]
        public void TryInvalidateCurrentTarget_MatchingId_ClearsTarget()
        {
            EntityId id = new EntityId(42);
            SetTarget(id, _targetObject.transform);

            bool invalidated = _controller.TryInvalidateCurrentTarget(id);

            Assert.IsTrue(invalidated);

            bool hasTarget = _controller.TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform);
            Assert.IsFalse(hasTarget);
            Assert.AreEqual(0, targetId.Value);
            Assert.IsNull(targetTransform);
        }

        [Test]
        public void TryInvalidateCurrentTarget_MismatchedId_DoesNotClearTarget()
        {
            EntityId id = new EntityId(42);
            EntityId otherId = new EntityId(99);
            SetTarget(id, _targetObject.transform);

            bool invalidated = _controller.TryInvalidateCurrentTarget(otherId);

            Assert.IsFalse(invalidated);

            bool hasTarget = _controller.TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform);
            Assert.IsTrue(hasTarget);
            Assert.AreEqual(id, targetId);
        }

        [Test]
        public void LateInvalidation_DoesNotClearReplacementTarget()
        {
            EntityId oldId = new EntityId(42);
            EntityId replacementId = new EntityId(99);
            GameObject replacementObject = new GameObject("Replacement");

            try
            {
                SetTarget(oldId, _targetObject.transform);
                SetTarget(replacementId, replacementObject.transform);

                Assert.IsFalse(_controller.TryInvalidateCurrentTarget(oldId));
                Assert.IsTrue(_controller.TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform));
                Assert.AreEqual(replacementId, targetId);
                Assert.AreEqual(replacementObject.transform, targetTransform);
            }
            finally
            {
                Object.DestroyImmediate(replacementObject);
            }
        }

        private void SetTarget(EntityId id, Transform targetTransform)
        {
            var type = typeof(EnemyMovementAIController);
            var structType = type.GetNestedType("EnemyTargetReference", BindingFlags.NonPublic);
            Assert.IsNotNull(structType, "Could not find EnemyTargetReference struct.");

            object targetRef = System.Activator.CreateInstance(structType, id, targetTransform);

            var field = type.GetField("_currentTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Could not find _currentTarget field.");
            field.SetValue(_controller, targetRef);
        }
    }
}
