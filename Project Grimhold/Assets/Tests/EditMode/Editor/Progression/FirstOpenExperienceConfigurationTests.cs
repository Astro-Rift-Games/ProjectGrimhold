using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Progression
{
    public sealed class FirstOpenExperienceConfigurationTests
    {
        private static readonly FieldInfo ExperienceRewardField =
            typeof(NetworkLootContainerInteractable).GetField(
                "_firstOpenExperienceReward",
                BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void ProductivePrefabs_HaveExpectedFirstOpenExperienceRewards()
        {
            AssertReward("Assets/Prefabs/LootContainer.prefab", 5);

            AssertReward("Assets/Prefabs/NetworkPlayer.prefab", 0);
            AssertReward("Assets/Prefabs/NetworkPlayerMelee.prefab", 0);
            AssertReward("Assets/Prefabs/NetworkPlayerRanged.prefab", 0);

            AssertReward("Assets/Prefabs/Enemies/NetworkEnemy.prefab", 0);
            AssertReward("Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab", 0);
            AssertReward("Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab", 0);
            AssertReward("Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab", 0);
            AssertReward("Assets/Prefabs/Enemies/Slimes/RedSlime.prefab", 0);
        }

        [Test]
        public void NegativeExperienceReward_IsRejectedByRuntimeValidation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/LootContainer.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                NetworkLootContainerInteractable interactable =
                    instance.GetComponent<NetworkLootContainerInteractable>();
                Assert.That(ExperienceRewardField, Is.Not.Null);
                ExperienceRewardField.SetValue(interactable, -1L);

                Assert.That(interactable.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("Experience"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void AssertReward(string path, long expected)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            NetworkLootContainerInteractable interactable =
                prefab.GetComponent<NetworkLootContainerInteractable>();
            Assert.That(interactable, Is.Not.Null, path);
            Assert.That(interactable.FirstOpenExperienceReward, Is.EqualTo(expected), path);
        }
    }
}
