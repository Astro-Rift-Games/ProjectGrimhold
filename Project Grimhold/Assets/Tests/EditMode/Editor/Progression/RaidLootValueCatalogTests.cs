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
        public void ProductiveCatalog_CoversLootWithPlaceholderValueAndExcludesTrainingWeapons()
        {
            RaidLootValueCatalog values = AssetDatabase.LoadAssetAtPath<RaidLootValueCatalog>(ValueCatalogPath);
            LootDefinitionCatalog loot = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(LootCatalogPath);

            Assert.That(values, Is.Not.Null);
            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.TryValidate(out string lootError), Is.True, lootError);
            Assert.That(loot.DefinitionCount, Is.GreaterThan(0));
            Assert.That(values.TryValidate(loot, out string error), Is.True, error);
            for (int index = 0; index < loot.DefinitionCount; index++)
            {
                Assert.That(loot.TryGetByIndex(index, out LootDefinition definition), Is.True);
                bool covered = values.TryGetValuePerUnit(definition.LootId, out long value);
                if (definition.IsTrainingWeapon)
                {
                    Assert.That(definition.CanBeFoundInDungeon, Is.False, definition.Id);
                    Assert.That(covered, Is.False, definition.Id);
                    Assert.That(value, Is.Zero, definition.Id);
                    continue;
                }
                Assert.That(covered, Is.True, definition.Id);
                Assert.That(value, Is.EqualTo(100), definition.Id);
            }
        }

        [TestCase("training_short_sword")]
        [TestCase("training_dagger")]
        [TestCase("training_wand")]
        public void TrainingWeapon_CannotAcquireProgressionValueThroughAnExplicitEntry(string id)
        {
            var loot = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(LootCatalogPath);
            Assert.That(loot.TryGet(id, out var training), Is.True);
            var values = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RaidLootValueCatalog>(ValueCatalogPath));
            try
            {
                using var serialized = new SerializedObject(values);
                var entries = serialized.FindProperty("_entries");
                int index = entries.arraySize++;
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_lootDefinition").objectReferenceValue = training;
                entry.FindPropertyRelative("_valuePerUnit").longValue = 100;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(values.TryValidate(loot, out string error), Is.False);
                Assert.That(error, Is.EqualTo($"Raid loot Value must not include training weapon '{id}'."));
                Assert.That(values.TryGetValuePerUnit(training.LootId, out long value), Is.False);
                Assert.That(value, Is.Zero);
            }
            finally { Object.DestroyImmediate(values); }
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
