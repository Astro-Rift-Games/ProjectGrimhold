using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NetworkObject = Fusion.NetworkObject;

namespace Tests.EditMode.Scenario
{
    public sealed class ExtractionProgressPrefabTests
    {
        [TestCase("Assets/Prefabs/NetworkPlayer.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerMelee.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayerRanged.prefab")]
        public void PlayerPrefabs_HaveBakedIndividualProgressAndDefeatReward(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            PlayerExtractionProgressController progress = prefab.GetComponent<PlayerExtractionProgressController>();
            ExtractionProgressDefeatSource defeat = prefab.GetComponent<ExtractionProgressDefeatSource>();

            Assert.That(progress, Is.Not.Null);
            Assert.That(defeat, Is.Not.Null);
            Assert.That(defeat.DefeatProgressReward, Is.EqualTo(30));
            Assert.That(networkObject.NetworkedBehaviours, Does.Contain(progress));
            Assert.That(networkObject.NetworkedBehaviours, Does.Contain(defeat));
            Assert.That(prefab.GetComponent<NetworkLootContainerInteractable>().FirstOpenProgressReward, Is.Zero);

            var serialized = new SerializedObject(progress);
            Assert.That(serialized.FindProperty("_config").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_characterSource").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_extractionController").objectReferenceValue, Is.Not.Null);
        }

        [TestCase("Assets/Prefabs/Enemies/NetworkEnemy.prefab", 0)]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab", 15)]
        [TestCase("Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab", 10)]
        [TestCase("Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab", 10)]
        [TestCase("Assets/Prefabs/Enemies/Slimes/RedSlime.prefab", 10)]
        public void EnemyPrefabs_HaveExpectedBakedDefeatReward(string path, int expectedReward)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            ExtractionProgressDefeatSource defeat = prefab.GetComponent<ExtractionProgressDefeatSource>();

            Assert.That(defeat, Is.Not.Null);
            Assert.That(defeat.DefeatProgressReward, Is.EqualTo(expectedReward));
            Assert.That(networkObject.NetworkedBehaviours, Does.Contain(defeat));
            Assert.That(prefab.GetComponent<NetworkLootContainerInteractable>().FirstOpenProgressReward, Is.Zero);
        }

        [Test]
        public void ChestAndConfiguration_HaveExpectedQuotaAndFirstOpenReward()
        {
            ExtractionConfig config = AssetDatabase.LoadAssetAtPath<ExtractionConfig>(
                "Assets/Scriptable Objects/ExtractionConfig.asset");
            GameObject chest = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LootContainer.prefab");

            Assert.That(config.ProgressQuota, Is.EqualTo(100));
            Assert.That(chest.GetComponent<NetworkLootContainerInteractable>().FirstOpenProgressReward, Is.EqualTo(5));
        }

        [TestCase("Coins", 1)]
        [TestCase("Bone", 5)]
        [TestCase("HealthPotion", 10)]
        public void LootDefinitions_HaveExpectedSellValue(string assetName, int expectedSellValue)
        {
            LootDefinition definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                $"Assets/Scriptable Objects/Loot/Definitions/{assetName}.asset");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.SellValuePerUnit, Is.EqualTo(expectedSellValue));
            Assert.That(definition.ExtractionValuePerUnit, Is.GreaterThanOrEqualTo(0));
        }
    }
}
