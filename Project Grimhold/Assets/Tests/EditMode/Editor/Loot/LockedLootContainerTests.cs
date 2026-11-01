using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class LockedLootContainerTests
    {
        [TestCase("Assets/Prefabs/LootContainer.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayer.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemy.prefab")]
        public void ExistingPrefabs_DefaultToUnlocked(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            NetworkLootContainer container = prefab.GetComponent<NetworkLootContainer>();
            Assert.That(container, Is.Not.Null);
            SerializedProperty startsLocked = new SerializedObject(container).FindProperty("_startsLocked");
            Assert.That(startsLocked, Is.Not.Null);
            Assert.That(startsLocked.boolValue, Is.False);
        }

        [Test]
        public void LockState_IsNetworkedWithPrivateSetterAndReadOnlyConfiguration()
        {
            PropertyInfo state = typeof(NetworkLootContainer).GetProperty("IsLocked");
            Assert.That(state, Is.Not.Null);
            Assert.That(state.PropertyType, Is.EqualTo(typeof(NetworkBool)));
            Assert.That(state.GetCustomAttribute<NetworkedAttribute>(), Is.Not.Null);
            Assert.That(state.GetSetMethod(true).IsPrivate, Is.True);
            PropertyInfo config = typeof(NetworkLootContainer).GetProperty("StartsLocked");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.CanWrite, Is.False);
        }

        [Test]
        public void UnspawnedContainer_CannotUnlock()
        {
            var holder = new GameObject(nameof(UnspawnedContainer_CannotUnlock));
            try
            {
                NetworkLootContainer container = holder.AddComponent<NetworkLootContainer>();
                Assert.That(container.TryUnlock(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
