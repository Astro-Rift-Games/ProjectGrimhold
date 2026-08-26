using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Progression
{
    public sealed class RaidLootValueCatalogTests
    {
        private const string ValueCatalogPath = "Assets/Scriptable Objects/RaidLootValueCatalog.asset";
        private const string LootCatalogPath = "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        [Test]
        public void ProductiveCatalog_CoversEveryLootIdWithExplicitPlaceholderValue()
        {
            RaidLootValueCatalog values = AssetDatabase.LoadAssetAtPath<RaidLootValueCatalog>(ValueCatalogPath);
            LootDefinitionCatalog loot = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(LootCatalogPath);

            Assert.That(values, Is.Not.Null);
            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.DefinitionCount, Is.EqualTo(14));
            Assert.That(values.TryValidate(loot, out string error), Is.True, error);
            for (int index = 0; index < loot.DefinitionCount; index++)
            {
                Assert.That(loot.TryGetByIndex(index, out LootDefinition definition), Is.True);
                Assert.That(values.TryGetValuePerUnit(definition.LootId, out long value), Is.True);
                Assert.That(value, Is.EqualTo(100), definition.Id);
            }
        }

        [Test]
        public void ProductivePlayer_UsesNonNetworkedStaticProducerConfiguration()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            ExtractedLootExperienceProducer producer =
                prefab.GetComponent<ExtractedLootExperienceProducer>();
            Assert.That(producer, Is.Not.Null);
            Assert.That(producer, Is.Not.InstanceOf<NetworkBehaviour>());

            var serialized = new SerializedObject(producer);
            Assert.That(
                serialized.FindProperty("_valueCatalog").objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<RaidLootValueCatalog>(ValueCatalogPath)));
            Assert.That(
                serialized.FindProperty("_experienceRateBasisPoints").intValue,
                Is.EqualTo(ExtractedLootExperienceProducer.DefaultExperienceRateBasisPoints));
        }
    }
}
