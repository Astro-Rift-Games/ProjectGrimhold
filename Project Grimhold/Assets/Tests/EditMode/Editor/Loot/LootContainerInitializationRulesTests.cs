using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class LootContainerInitializationRulesTests
    {
        private readonly List<Object> _created = new();
        private LootDefinitionCatalog _catalog;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            var texture = new Texture2D(2, 2);
            _created.Add(texture);
            _sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _created.Add(_sprite);
            _catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _created.Add(_catalog);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_created[i]);
            }
        }

        [Test]
        public void EmptyInitialContent_IsValidWithValidCatalog()
        {
            LootDefinition coin = CreateDefinition("coin");
            SetCatalog(coin);

            bool valid = LootContainerInitializationRules.TryBuild(
                (IReadOnlyList<LootContainerInitialEntry>)null,
                _catalog,
                4,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> entries,
                out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void CatalogMayExceedDistinctStackCapacity_WhenInitialStacksFit()
        {
            var definitions = new LootDefinition[22];
            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index] = CreateDefinition($"item_{index:00}");
            }

            SetCatalog(definitions);

            bool valid = LootContainerInitializationRules.TryBuild(
                new[]
                {
                    new LootContainerInitialEntry(definitions[20], 1),
                    new LootContainerInitialEntry(definitions[21], 1)
                },
                _catalog,
                16,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> entries,
                out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].Key, Is.EqualTo(20));
            Assert.That(entries[1].Key, Is.EqualTo(21));
        }

        [Test]
        public void InitialContent_IsResolvedInCatalogIndexOrder()
        {
            LootDefinition gem = CreateDefinition("gem");
            LootDefinition coin = CreateDefinition("coin");
            SetCatalog(gem, coin);

            bool valid = LootContainerInitializationRules.TryBuild(
                new[]
                {
                    new LootContainerInitialEntry(gem, 2),
                    new LootContainerInitialEntry(coin, 5)
                },
                _catalog,
                4,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> entries,
                out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(entries[0].Key, Is.EqualTo(0));
            Assert.That(entries[0].Value, Is.EqualTo(5));
            Assert.That(entries[1].Key, Is.EqualTo(1));
            Assert.That(entries[1].Value, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateOrInvalidEntries_ReturnNoPartialOutput()
        {
            LootDefinition coin = CreateDefinition("coin");
            SetCatalog(coin);

            bool duplicateValid = LootContainerInitializationRules.TryBuild(
                new[]
                {
                    new LootContainerInitialEntry(coin, 2),
                    new LootContainerInitialEntry(coin, 3)
                },
                _catalog,
                4,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> duplicateOutput,
                out _);
            bool amountValid = LootContainerInitializationRules.TryBuild(
                new[] { new LootContainerInitialEntry(coin, 0) },
                _catalog,
                4,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> amountOutput,
                out _);

            Assert.That(duplicateValid, Is.False);
            Assert.That(duplicateOutput, Is.Empty);
            Assert.That(amountValid, Is.False);
            Assert.That(amountOutput, Is.Empty);
        }

        [Test]
        public void InitialContentBeyondSlotCapacity_IsRejectedWithoutOutput()
        {
            LootDefinition coin = CreateDefinition("coin");
            LootDefinition gem = CreateDefinition("gem");
            SetCatalog(coin, gem);

            bool valid = LootContainerInitializationRules.TryBuild(
                new[]
                {
                    new LootContainerInitialEntry(coin, 1),
                    new LootContainerInitialEntry(gem, 1)
                },
                _catalog,
                1,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> entries,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void RuntimeEntries_AcceptIntMaxValueAndReturnNoPartialOutputOnFailure()
        {
            LootDefinition coin = CreateDefinition("coin");
            SetCatalog(coin);

            bool valid = LootContainerInitializationRules.TryBuild(
                new[] { new LootEntry(coin.LootId, int.MaxValue) },
                _catalog,
                1,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> entries,
                out string error);
            bool invalid = LootContainerInitializationRules.TryBuild(
                new[] { new LootEntry(coin.LootId, 0) },
                _catalog,
                1,
                NetworkLootContainer.MaxDistinctLootTypes,
                out IReadOnlyList<KeyValuePair<int, int>> invalidEntries,
                out _);

            Assert.That(valid, Is.True, error);
            Assert.That(entries[0].Value, Is.EqualTo(int.MaxValue));
            Assert.That(invalid, Is.False);
            Assert.That(invalidEntries, Is.Empty);
        }

        private LootDefinition CreateDefinition(string id)
        {
            LootDefinition definition = ScriptableObject.CreateInstance<LootDefinition>();
            _created.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = id;
            serialized.FindProperty("_icon").objectReferenceValue = _sprite;
            serialized.FindProperty("_worldSprite").objectReferenceValue = _sprite;
            serialized.FindProperty("_category").enumValueIndex = (int)LootCategory.Valuable;
            serialized.FindProperty("_rarity").enumValueIndex = (int)LootRarity.Common;
            serialized.FindProperty("_extractionValuePerUnit").intValue = 1;
            serialized.FindProperty("_defaultPickupQuantity").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private void SetCatalog(params LootDefinition[] definitions)
        {
            var serialized = new SerializedObject(_catalog);
            SerializedProperty values = serialized.FindProperty("_definitions");
            values.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++)
            {
                values.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
