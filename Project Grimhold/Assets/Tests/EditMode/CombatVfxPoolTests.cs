using NUnit.Framework;
using UnityEngine;
using Grimhold.Combat.Presentation;

namespace Grimhold.Tests.Combat.Presentation
{
    [TestFixture]
    public class CombatVfxPoolTests
    {
        private GameObject _poolGo;
        private CombatVfxPool _pool;
        private CombatVfxDefinition _definition;

        [SetUp]
        public void Setup()
        {
            _poolGo = new GameObject("VfxPool");
            _pool = _poolGo.AddComponent<CombatVfxPool>();

            _definition = ScriptableObject.CreateInstance<CombatVfxDefinition>();
            // Since we can't easily set the private fields of _definition in a pure unit test without Reflection or Editor tools,
            // we will just skip pre-warm assertion if prefab is null, OR we can use Reflection to set it up.
            var prefabGo = new GameObject("Prefab");
            prefabGo.AddComponent<SpriteRenderer>();
            var instance = prefabGo.AddComponent<CombatVfxInstance>();
            
            var so = new UnityEditor.SerializedObject(_definition);
            so.FindProperty("_prefab").objectReferenceValue = instance;
            so.FindProperty("_poolCapacity").intValue = 2;
            so.ApplyModifiedProperties();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_poolGo);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void Prewarm_CreatesInstancesUpToCapacity()
        {
            _pool.Prewarm(_definition);
            
            // Should have 2 children created from prefab
            Assert.AreEqual(2, _pool.transform.childCount);
        }
        
        [Test]
        public void Play_WithoutPrewarm_CreatesInstancesAndPlays()
        {
            _pool.Play(_definition, Vector3.zero, Quaternion.identity, false, Color.white);
            
            // Pool initializes, creates capacity, and one is dequeued and played
            // Since pool capacity is 2, it should create 2 instances. One is active, one is inactive.
            Assert.AreEqual(2, _pool.transform.childCount);
            
            bool anyActive = false;
            foreach (Transform child in _pool.transform)
            {
                if (child.gameObject.activeSelf) anyActive = true;
            }
            Assert.IsTrue(anyActive);
        }
        
        [Test]
        public void Play_ExhaustedPool_DoesNotGrowDynamically()
        {
            _pool.Prewarm(_definition);
            
            // Capacity is 2
            _pool.Play(_definition, Vector3.zero, Quaternion.identity, false, Color.white);
            _pool.Play(_definition, Vector3.zero, Quaternion.identity, false, Color.white);
            _pool.Play(_definition, Vector3.zero, Quaternion.identity, false, Color.white);
            
            // Should still only have 2 children (no dynamic growth during hot path)
            Assert.AreEqual(2, _pool.transform.childCount);
        }
    }
}
