using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public class Physics2DAttackTargetQueryTests
    {
        private GameObject _registryHolder;
        private EntityRegistry _registry;
        private GameObject _queryHolder;
        private Physics2DAttackTargetQuery _query;
        private List<GameObject> _spawnedObjects;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
            
            _registryHolder = new GameObject("EntityRegistryHolder");
            _registry = _registryHolder.AddComponent<EntityRegistry>();
            _spawnedObjects.Add(_registryHolder);

            _queryHolder = new GameObject("QueryHolder");
            _query = _queryHolder.AddComponent<Physics2DAttackTargetQuery>();
            _spawnedObjects.Add(_queryHolder);

            // Inject the registry using reflection
            typeof(Physics2DAttackTargetQuery)
                .GetField("_registry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_query, _registry);

            // Instantiate buffer array in Physics2DAttackTargetQuery
            typeof(Physics2DAttackTargetQuery)
                .GetField("_colliderBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_query, new Collider2D[64]);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private (GameObject go, DummyCharacter target) CreateTarget(EntityId id, Vector2 position, int layer, bool isAlive = true, bool canReceiveDamage = true)
        {
            var go = new GameObject($"Target_{id.Value}");
            go.transform.position = position;
            go.layer = layer;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.1f;

            var character = go.AddComponent<DummyCharacter>();
            character.Id = id;
            character.IsAlive = isAlive;
            character.CanReceiveDamage = canReceiveDamage;

            _registry.TryRegister(id, character, new[] { collider });
            _spawnedObjects.Add(go);

            Physics2D.SyncTransforms();

            return (go, character);
        }

        [Test]
        public void FindTargets_ExcludesByLayer()
        {
            int targetLayer = LayerMask.NameToLayer("Default");
            int otherLayer = 2; // Typically ignored layer

            CreateTarget(new EntityId(2), new Vector2(1f, 0f), otherLayer);

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.right,
                1f,
                1f,
                5,
                1 << targetLayer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void FindTargets_ExcludesColliderWithoutEntity()
        {
            int layer = LayerMask.NameToLayer("Default");
            var go = new GameObject("UnregisteredCollider");
            go.transform.position = new Vector2(1f, 0f);
            go.layer = layer;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.1f;
            _spawnedObjects.Add(go);

            Physics2D.SyncTransforms();

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.right,
                1f,
                1f,
                5,
                1 << layer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void FindTargets_ExcludesNonDamageableEntity()
        {
            int layer = LayerMask.NameToLayer("Default");
            CreateTarget(new EntityId(2), new Vector2(1f, 0f), layer, canReceiveDamage: false);

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.right,
                1f,
                1f,
                5,
                1 << layer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void FindTargets_ExcludesDeadEntity()
        {
            int layer = LayerMask.NameToLayer("Default");
            CreateTarget(new EntityId(2), new Vector2(1f, 0f), layer, isAlive: false);

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.right,
                1f,
                1f,
                5,
                1 << layer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void FindTargets_ExcludesAttacker()
        {
            int layer = LayerMask.NameToLayer("Default");
            var attackerId = new EntityId(1);
            CreateTarget(attackerId, new Vector2(1f, 0f), layer);

            var request = new AttackTargetQuery(
                attackerId,
                Vector2.zero,
                Vector2.right,
                1f,
                1f,
                5,
                1 << layer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void FindTargets_MultipleCollidersFromSameTarget_ReturnsClosestHitAndDeduplicates()
        {
            int layer = LayerMask.NameToLayer("Default");
            var targetId = new EntityId(2);

            var go = new GameObject("MultiColliderTarget");
            go.transform.position = Vector2.zero;
            go.layer = layer;
            
            var col1 = go.AddComponent<CircleCollider2D>();
            col1.offset = new Vector2(0.5f, 0f);
            col1.radius = 0.1f;

            var col2 = go.AddComponent<CircleCollider2D>();
            col2.offset = new Vector2(1.2f, 0f);
            col2.radius = 0.1f;

            var character = go.AddComponent<DummyCharacter>();
            character.Id = targetId;
            character.IsAlive = true;
            character.CanReceiveDamage = true;

            _registry.TryRegister(targetId, character, new[] { col1, col2 });
            _spawnedObjects.Add(go);

            Physics2D.SyncTransforms();

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.right,
                1f,
                1.5f,
                5,
                1 << layer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(targetId, targets[0].TargetId);
            Assert.IsTrue(targets[0].HitPoint.x < 0.6f);
        }

        [Test]
        public void FindTargets_AttackOverlapsOnlyBodyHitbox_ReturnsTarget()
        {
            int layer = LayerMask.NameToLayer("Default");
            EntityId targetId = new EntityId(2);
            CreateBodyHitboxTarget(targetId, layer);

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.up,
                1.5f,
                0.2f,
                1,
                1 << layer
            );

            IReadOnlyList<AttackTarget> targets = _query.FindTargets(request);

            Assert.That(targets, Has.Count.EqualTo(1));
            Assert.That(targets[0].TargetId, Is.EqualTo(targetId));
        }

        [Test]
        public void FindTargets_AttackOverlapsFootAndBodyHitboxes_ReturnsSingleTarget()
        {
            int layer = LayerMask.NameToLayer("Default");
            EntityId targetId = new EntityId(2);
            CreateBodyHitboxTarget(targetId, layer);

            var request = new AttackTargetQuery(
                new EntityId(1),
                new Vector2(0f, 0.25f),
                Vector2.zero,
                0f,
                0.3f,
                5,
                1 << layer
            );

            IReadOnlyList<AttackTarget> targets = _query.FindTargets(request);

            Assert.That(targets, Has.Count.EqualTo(1));
            Assert.That(targets[0].TargetId, Is.EqualTo(targetId));
        }

        [TestCase("Assets/Prefabs/NetworkPlayer.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerMelee.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerRanged.prefab")]
        [TestCase("Assets/Prefabs/NetworkEnemy.prefab")]
        [TestCase("Assets/Prefabs/NetworkEnemyMelee.prefab")]
        [TestCase("Assets/Prefabs/NetworkEnemyRanged.prefab")]
        public void CharacterPrefab_SeparatesMovementColliderFromBodyDamageHitbox(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null, $"Missing character prefab at {prefabPath}.");

            Transform body = prefab.transform.Find("Body");
            Transform damageHitboxTransform = prefab.transform.Find("DamageHitbox");
            BoxCollider2D movementCollider = prefab.GetComponent<BoxCollider2D>();
            Kinematic2DMovementMotor movementMotor = prefab.GetComponent<Kinematic2DMovementMotor>();

            Assert.That(body, Is.Not.Null);
            Assert.That(damageHitboxTransform, Is.Not.Null);
            Assert.That(damageHitboxTransform.parent, Is.SameAs(prefab.transform));
            Assert.That(damageHitboxTransform.IsChildOf(body), Is.False);
            Assert.That(damageHitboxTransform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(damageHitboxTransform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(damageHitboxTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(damageHitboxTransform.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Character")));

            BoxCollider2D damageHitbox = damageHitboxTransform.GetComponent<BoxCollider2D>();
            Assert.That(damageHitbox, Is.Not.Null);
            Assert.That(damageHitbox.isTrigger, Is.True);
            Assert.That(damageHitbox.size, Is.EqualTo(new Vector2(1.25f, 2.25f)));
            Assert.That(damageHitbox.offset, Is.EqualTo(new Vector2(0f, 1.125f)));

            Assert.That(movementCollider, Is.Not.Null);
            Assert.That(movementCollider.isTrigger, Is.False);
            Assert.That(movementCollider.size, Is.EqualTo(new Vector2(0.75f, 0.5f)));
            Assert.That(movementCollider.offset, Is.EqualTo(new Vector2(0f, 0.25f)));
            Assert.That(movementMotor, Is.Not.Null);

            var colliderField = typeof(Kinematic2DMovementMotor).GetField(
                "_collider",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(colliderField, Is.Not.Null);
            Assert.That(colliderField.GetValue(movementMotor), Is.SameAs(movementCollider));
        }

        [Test]
        public void FindTargets_OrdersByDistanceToOrigin()
        {
            int layer = LayerMask.NameToLayer("Default");
            
            CreateTarget(new EntityId(3), new Vector2(1.3f, 0f), layer);
            CreateTarget(new EntityId(2), new Vector2(0.8f, 0f), layer);

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.right,
                1f,
                1f,
                5,
                1 << layer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(2, targets.Count);
            Assert.AreEqual(new EntityId(2), targets[0].TargetId);
            Assert.AreEqual(new EntityId(3), targets[1].TargetId);
        }

        [Test]
        public void FindTargets_TieBreakerByEntityId()
        {
            int layer = LayerMask.NameToLayer("Default");
            
            CreateTarget(new EntityId(3), new Vector2(1f, 1f), layer);
            CreateTarget(new EntityId(2), new Vector2(1f, -1f), layer);

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.right,
                1f,
                2f,
                5,
                1 << layer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(2, targets.Count);
            Assert.AreEqual(new EntityId(2), targets[0].TargetId);
            Assert.AreEqual(new EntityId(3), targets[1].TargetId);
        }

        [Test]
        public void FindTargets_AppliesMaximumTargetsAfterDeduplication()
        {
            int layer = LayerMask.NameToLayer("Default");

            var goA = new GameObject("TargetA");
            goA.transform.position = Vector2.zero;
            goA.layer = layer;

            var colA1 = goA.AddComponent<CircleCollider2D>();
            colA1.offset = new Vector2(0.5f, 0f);

            var colA2 = goA.AddComponent<CircleCollider2D>();
            colA2.offset = new Vector2(0.6f, 0f);

            var charA = goA.AddComponent<DummyCharacter>();
            charA.Id = new EntityId(2);
            charA.IsAlive = true;
            charA.CanReceiveDamage = true;

            _registry.TryRegister(charA.Id, charA, new[] { colA1, colA2 });
            _spawnedObjects.Add(goA);

            CreateTarget(new EntityId(3), new Vector2(0.9f, 0f), layer);

            Physics2D.SyncTransforms();

            var request = new AttackTargetQuery(
                new EntityId(1),
                Vector2.zero,
                Vector2.right,
                1f,
                1.5f,
                2,
                1 << layer
            );

            var targets = _query.FindTargets(request);

            Assert.AreEqual(2, targets.Count);
            Assert.AreEqual(new EntityId(2), targets[0].TargetId);
            Assert.AreEqual(new EntityId(3), targets[1].TargetId);
        }

        private (GameObject go, DummyCharacter target) CreateBodyHitboxTarget(EntityId id, int layer)
        {
            var go = new GameObject($"BodyHitboxTarget_{id.Value}");
            go.layer = layer;

            var movementCollider = go.AddComponent<BoxCollider2D>();
            movementCollider.size = new Vector2(0.75f, 0.5f);
            movementCollider.offset = new Vector2(0f, 0.25f);

            var damageHitboxObject = new GameObject("DamageHitbox");
            damageHitboxObject.layer = layer;
            damageHitboxObject.transform.SetParent(go.transform, false);

            var damageHitbox = damageHitboxObject.AddComponent<BoxCollider2D>();
            damageHitbox.isTrigger = true;
            damageHitbox.size = new Vector2(1.25f, 2.25f);
            damageHitbox.offset = new Vector2(0f, 1.125f);

            var character = go.AddComponent<DummyCharacter>();
            character.Id = id;
            character.IsAlive = true;
            character.CanReceiveDamage = true;

            _registry.TryRegister(id, character, new Collider2D[] { movementCollider, damageHitbox });
            _spawnedObjects.Add(go);
            Physics2D.SyncTransforms();

            return (go, character);
        }

        private sealed class DummyCharacter : MonoBehaviour, IDamageable, ICharacter
        {
            public EntityId Id { get; set; }
            public bool IsAlive { get; set; }
            public bool CanReceiveDamage { get; set; }

            public DamageResult ApplyDamage(in DamageRequest request)
            {
                return new DamageResult(Id, true, request.Amount, 100f, false, DamageFailureReason.None);
            }
        }
    }
}
