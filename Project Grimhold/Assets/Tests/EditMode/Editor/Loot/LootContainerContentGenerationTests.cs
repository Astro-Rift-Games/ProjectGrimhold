using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class LootContainerContentGenerationTests
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
        public void Snapshot_ValidatesUInt64WeightBoundaryAndRejectsOverflow()
        {
            LootDefinition first = CreateDefinition("first");
            LootDefinition second = CreateDefinition("second");
            SetCatalog(first, second);

            LootContainerContentTable nearLimit = CreateTable(
                1,
                2,
                false,
                new LootContainerContentTableEntry(first, ulong.MaxValue - 1, 1, 1),
                new LootContainerContentTableEntry(second, 1, 1, 1));
            Assert.That(TrySnapshot(nearLimit, out ValidatedLootContainerContentSnapshot snapshot, out string error), Is.True, error);
            Assert.That(snapshot.TotalWeight, Is.EqualTo(ulong.MaxValue));

            LootContainerContentTable overflow = CreateTable(
                1,
                2,
                false,
                new LootContainerContentTableEntry(first, ulong.MaxValue, 1, 1),
                new LootContainerContentTableEntry(second, 1, 1, 1));
            Assert.That(TrySnapshot(overflow, out snapshot, out error), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(error, Does.Contain("UInt64"));
        }

        [Test]
        public void Snapshot_RejectsInvalidAmountsDuplicatesAndImpossibleMinimum()
        {
            LootDefinition first = CreateDefinition("first");
            SetCatalog(first);

            LootContainerContentTable invalidAmount = CreateTable(
                1,
                1,
                false,
                new LootContainerContentTableEntry(first, 1, 0, 1));
            Assert.That(TrySnapshot(invalidAmount, out _, out _), Is.False);

            LootContainerContentTable duplicate = CreateTable(
                1,
                2,
                false,
                new LootContainerContentTableEntry(first, 1, 1, 1),
                new LootContainerContentTableEntry(first, 1, 1, 1));
            Assert.That(TrySnapshot(duplicate, out _, out _), Is.False);

            LootContainerContentTable impossible = CreateTable(
                2,
                2,
                false,
                new LootContainerContentTableEntry(first, 1, 1, 1));
            Assert.That(TrySnapshot(impossible, out _, out _), Is.False);
        }

        [Test]
        public void Snapshot_AllowsOnlyExplicitEmptyTableAndCopiesAssetValues()
        {
            LootDefinition first = CreateDefinition("first");
            SetCatalog(first);
            LootContainerContentTable empty = CreateTable(0, 0, true);

            Assert.That(TrySnapshot(empty, out ValidatedLootContainerContentSnapshot emptySnapshot, out string error), Is.True, error);
            Assert.That(emptySnapshot.EntryCount, Is.Zero);

            LootContainerContentTable table = CreateTable(
                1,
                1,
                false,
                new LootContainerContentTableEntry(first, 7, 2, 4));
            Assert.That(TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out error), Is.True, error);

            ConfigureTable(
                table,
                1,
                1,
                false,
                new LootContainerContentTableEntry(first, 99, 8, 9));

            Assert.That(snapshot.GetEntry(0).Weight, Is.EqualTo(7UL));
            Assert.That(snapshot.GetEntry(0).MinimumAmount, Is.EqualTo(2));
            Assert.That(snapshot.GetEntry(0).MaximumAmount, Is.EqualTo(4));
        }

        [Test]
        public void Snapshot_AllowsCatalogBeyondDistinctStackCapacity_WhenTableFits()
        {
            var definitions = new LootDefinition[22];
            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index] = CreateDefinition($"item_{index:00}");
            }

            SetCatalog(definitions);
            LootContainerContentTable table = CreateTable(
                1,
                1,
                false,
                new LootContainerContentTableEntry(definitions[21], 1, 1, 1));

            Assert.That(
                TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out string error),
                Is.True,
                error);
            Assert.That(snapshot.EntryCount, Is.EqualTo(1));
            Assert.That(snapshot.GetEntry(0).CatalogIndex, Is.EqualTo(21));
            Assert.That(snapshot.NetworkCapacity, Is.EqualTo(NetworkLootContainer.MaxDistinctLootTypes));
        }

        [Test]
        public void Roller_IsReproducibleUniqueOrderedAndSupportsIntMaxValue()
        {
            LootDefinition third = CreateDefinition("third");
            LootDefinition first = CreateDefinition("first");
            LootDefinition second = CreateDefinition("second");
            SetCatalog(third, first, second);
            LootContainerContentTable table = CreateTable(
                3,
                3,
                false,
                new LootContainerContentTableEntry(third, ulong.MaxValue - 2, 1, 100),
                new LootContainerContentTableEntry(first, 1, int.MaxValue, int.MaxValue),
                new LootContainerContentTableEntry(second, 1, 1, int.MaxValue));
            Assert.That(TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out string error), Is.True, error);

            Assert.That(LootContainerContentRoller.TryRoll(snapshot, 0, out IReadOnlyList<LootEntry> firstRoll, out error), Is.True, error);
            Assert.That(LootContainerContentRoller.TryRoll(snapshot, 0, out IReadOnlyList<LootEntry> secondRoll, out error), Is.True, error);

            Assert.That(secondRoll, Is.EqualTo(firstRoll));
            Assert.That(firstRoll, Has.Count.EqualTo(3));
            Assert.That(firstRoll[0].LootId.Value, Is.EqualTo("first"));
            Assert.That(firstRoll[0].Amount, Is.EqualTo(int.MaxValue));
            Assert.That(firstRoll[1].LootId.Value, Is.EqualTo("second"));
            Assert.That(firstRoll[1].Amount, Is.InRange(1, int.MaxValue));
            Assert.That(firstRoll[2].LootId.Value, Is.EqualTo("third"));
            Assert.That(new HashSet<LootId> { firstRoll[0].LootId, firstRoll[1].LootId, firstRoll[2].LootId }, Has.Count.EqualTo(3));
        }

        [Test]
        public void Roller_LuckAddsOneDistinctStackWithoutChangingBaseContent()
        {
            LootDefinition first = CreateDefinition("first");
            LootDefinition second = CreateDefinition("second");
            LootDefinition third = CreateDefinition("third");
            SetCatalog(first, second, third);
            LootContainerContentTable table = CreateTable(
                1,
                1,
                false,
                new LootContainerContentTableEntry(first, 1, 1, 3),
                new LootContainerContentTableEntry(second, 2, 2, 4),
                new LootContainerContentTableEntry(third, 3, 3, 5));
            Assert.That(TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out string error),
                Is.True, error);

            Assert.That(LootContainerContentRoller.TryRoll(
                snapshot, 42, 0, out IReadOnlyList<LootEntry> baseRoll, out error), Is.True, error);
            Assert.That(LootContainerContentRoller.TryRoll(
                snapshot, 42, 10_000, out IReadOnlyList<LootEntry> luckyRoll, out error), Is.True, error);
            Assert.That(LootContainerContentRoller.TryRoll(
                snapshot, 42, 10_000, out IReadOnlyList<LootEntry> repeatedLuckyRoll, out error), Is.True, error);

            Assert.That(baseRoll, Has.Count.EqualTo(1));
            Assert.That(luckyRoll, Has.Count.EqualTo(2));
            Assert.That(repeatedLuckyRoll, Is.EqualTo(luckyRoll));
            Assert.That(luckyRoll, Does.Contain(baseRoll[0]));
            Assert.That(new HashSet<LootId> { luckyRoll[0].LootId, luckyRoll[1].LootId },
                Has.Count.EqualTo(2));
            for (int index = 0; index < luckyRoll.Count; index++)
            {
                LootEntry entry = luckyRoll[index];
                if (entry.LootId == first.LootId)
                {
                    Assert.That(entry.Amount, Is.InRange(1, 3));
                }
                else if (entry.LootId == second.LootId)
                {
                    Assert.That(entry.Amount, Is.InRange(2, 4));
                }
                else
                {
                    Assert.That(entry.LootId, Is.EqualTo(third.LootId));
                    Assert.That(entry.Amount, Is.InRange(3, 5));
                }
            }
        }

        [Test]
        public void Roller_InvalidLuckChanceIsRejectedWithoutPartialContent()
        {
            LootDefinition first = CreateDefinition("first");
            SetCatalog(first);
            LootContainerContentTable table = CreateTable(
                1,
                1,
                false,
                new LootContainerContentTableEntry(first, 1, 1, 1));
            Assert.That(TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out string error),
                Is.True, error);

            Assert.That(LootContainerContentRoller.TryRoll(
                snapshot, 1, -1, out IReadOnlyList<LootEntry> result, out error), Is.False);
            Assert.That(result, Is.Empty);
            Assert.That(error, Does.Contain("basis points"));
        }

        [Test]
        public void LuckCompatibility_RequiresOneStackBeyondTheBaseMaximum()
        {
            LootDefinition first = CreateDefinition("first");
            LootDefinition second = CreateDefinition("second");
            SetCatalog(first, second);
            LootContainerContentTable noHeadroom = CreateTable(
                2,
                2,
                false,
                new LootContainerContentTableEntry(first, 1, 1, 1),
                new LootContainerContentTableEntry(second, 1, 1, 1));
            Assert.That(TrySnapshot(noHeadroom, out ValidatedLootContainerContentSnapshot snapshot, out string error),
                Is.True, error);
            Assert.That(
                LootContainerContentTableValidation.HasAdditionalStackCapacity(snapshot, out error),
                Is.False);
            Assert.That(error, Does.Contain("one distinct stack"));

            ConfigureTable(
                noHeadroom,
                1,
                1,
                false,
                new LootContainerContentTableEntry(first, 1, 1, 1),
                new LootContainerContentTableEntry(second, 1, 1, 1));
            Assert.That(TrySnapshot(noHeadroom, out snapshot, out error), Is.True, error);
            Assert.That(
                LootContainerContentTableValidation.HasAdditionalStackCapacity(snapshot, out error),
                Is.True, error);
        }

        [Test]
        public void SeedDerivation_IsStableAndDistinctByPointAndGeneration()
        {
            ulong first = LootContainerSeedRules.Derive(123, 4, 0);

            Assert.That(LootContainerSeedRules.Derive(123, 4, 0), Is.EqualTo(first));
            Assert.That(LootContainerSeedRules.Derive(123, 4, 1), Is.Not.EqualTo(first));
            Assert.That(LootContainerSeedRules.Derive(123, 5, 0), Is.Not.EqualTo(first));
        }

        [Test]
        public void SeedDerivation_SeparatesLootAndBreakableDomains()
        {
            ulong lootSeed = LootContainerSeedRules.Derive(
                123,
                4,
                (int)Spawning.SpawnGroupType.Loot,
                0);
            ulong breakableSeed = LootContainerSeedRules.Derive(
                123,
                4,
                (int)Spawning.SpawnGroupType.Breakables,
                0);

            Assert.That(breakableSeed, Is.Not.EqualTo(lootSeed));
            Assert.That(
                LootContainerSeedRules.Derive(
                    123,
                    4,
                    (int)Spawning.SpawnGroupType.Breakables,
                    0),
                Is.EqualTo(breakableSeed));
        }

        private bool TrySnapshot(
            LootContainerContentTable table,
            out ValidatedLootContainerContentSnapshot snapshot,
            out string error)
        {
            return LootContainerContentTableValidation.TryCreateSnapshot(
                table,
                _catalog,
                16,
                NetworkLootContainer.MaxDistinctLootTypes,
                out snapshot,
                out error);
        }

        private LootContainerContentTable CreateTable(
            int minimum,
            int maximum,
            bool allowEmpty,
            params LootContainerContentTableEntry[] entries)
        {
            LootContainerContentTable table = ScriptableObject.CreateInstance<LootContainerContentTable>();
            _created.Add(table);
            ConfigureTable(table, minimum, maximum, allowEmpty, entries);
            return table;
        }

        private static void ConfigureTable(
            LootContainerContentTable table,
            int minimum,
            int maximum,
            bool allowEmpty,
            params LootContainerContentTableEntry[] entries)
        {
            var serialized = new SerializedObject(table);
            serialized.FindProperty("_minimumDistinctStacks").intValue = minimum;
            serialized.FindProperty("_maximumDistinctStacks").intValue = maximum;
            serialized.FindProperty("_allowEmpty").boolValue = allowEmpty;
            SerializedProperty values = serialized.FindProperty("_entries");
            values.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty value = values.GetArrayElementAtIndex(i);
                value.FindPropertyRelative("_definition").objectReferenceValue = entries[i].Definition;
                value.FindPropertyRelative("_weight").ulongValue = entries[i].Weight;
                value.FindPropertyRelative("_minimumAmount").intValue = entries[i].MinimumAmount;
                value.FindPropertyRelative("_maximumAmount").intValue = entries[i].MaximumAmount;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
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
