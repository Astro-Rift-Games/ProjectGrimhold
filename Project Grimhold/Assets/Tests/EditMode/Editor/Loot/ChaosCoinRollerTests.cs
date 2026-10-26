using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class ChaosCoinRollerTests
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
        public void Roller_ChaosCoinDoesNotAlterBaseContent()
        {
            LootDefinition d1 = CreateDefinition("item_1");
            LootDefinition d2 = CreateDefinition("item_2");
            LootDefinition d3 = CreateDefinition("item_3");
            LootDefinition d4 = CreateDefinition("item_4");
            SetCatalog(d1, d2, d3, d4);

            LootContainerContentTable table = CreateTable(
                1,
                1,
                false,
                new LootContainerContentTableEntry(d1, 10, 1, 1),
                new LootContainerContentTableEntry(d2, 10, 1, 1),
                new LootContainerContentTableEntry(d3, 10, 1, 1),
                new LootContainerContentTableEntry(d4, 10, 1, 1));

            Assert.That(TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out string error), Is.True, error);

            for (ulong seed = 1; seed <= 50; seed++)
            {
                Assert.That(LootContainerContentRoller.TryRoll(
                    snapshot, seed, 0, hasChaosCoin: false, out IReadOnlyList<LootEntry> withoutCoin, out error), Is.True, error);

                Assert.That(LootContainerContentRoller.TryRoll(
                    snapshot, seed, 0, hasChaosCoin: true, out IReadOnlyList<LootEntry> withCoin, out error), Is.True, error);

                // Base entry must always be identical and present in withCoin
                Assert.That(withoutCoin.Count, Is.EqualTo(1));
                Assert.That(withCoin.Count, Is.InRange(1, 2));
                Assert.That(withCoin, Does.Contain(withoutCoin[0]));
            }
        }

        [Test]
        public void Roller_ChaosCoinAndLuckAreMutuallyIndependent()
        {
            LootDefinition d1 = CreateDefinition("item_1");
            LootDefinition d2 = CreateDefinition("item_2");
            LootDefinition d3 = CreateDefinition("item_3");
            LootDefinition d4 = CreateDefinition("item_4");
            SetCatalog(d1, d2, d3, d4);

            LootContainerContentTable table = CreateTable(
                1,
                1,
                false,
                new LootContainerContentTableEntry(d1, 10, 1, 1),
                new LootContainerContentTableEntry(d2, 10, 1, 1),
                new LootContainerContentTableEntry(d3, 10, 1, 1),
                new LootContainerContentTableEntry(d4, 10, 1, 1));

            Assert.That(TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out string error), Is.True, error);

            // Test across multiple seeds: Chaos Coin result should be completely identical regardless of luck basis points
            int bothTriggeredCount = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                // Roll with only Chaos Coin (Luck = 0)
                Assert.That(LootContainerContentRoller.TryRoll(
                    snapshot, seed, 0, hasChaosCoin: true, out IReadOnlyList<LootEntry> coinOnly, out error), Is.True, error);

                bool coinTriggered = coinOnly.Count == 2;

                // Roll with both Luck (100% chance = 10000 bp) and Chaos Coin
                Assert.That(LootContainerContentRoller.TryRoll(
                    snapshot, seed, 10000, hasChaosCoin: true, out IReadOnlyList<LootEntry> both, out error), Is.True, error);

                if (coinTriggered)
                {
                    // If Chaos coin triggered with Luck = 0, with 100% Luck it must have 3 stacks (base + luck + coin)
                    Assert.That(both.Count, Is.EqualTo(3), $"Seed {seed}: Expected both Luck and Chaos Coin to roll bonus stacks.");
                    bothTriggeredCount++;
                }
                else
                {
                    // If Chaos coin did not trigger, with 100% Luck it must have exactly 2 stacks (base + luck)
                    Assert.That(both.Count, Is.EqualTo(2), $"Seed {seed}: Expected only Luck bonus stack.");
                }
            }

            Assert.That(bothTriggeredCount, Is.GreaterThan(10), "Expected multiple seeds where Chaos Coin triggered.");
        }

        [Test]
        public void Roller_ChaosCoinRateMatchesExpected20PercentBasisPoints()
        {
            LootDefinition d1 = CreateDefinition("item_1");
            LootDefinition d2 = CreateDefinition("item_2");
            SetCatalog(d1, d2);

            LootContainerContentTable table = CreateTable(
                1,
                1,
                false,
                new LootContainerContentTableEntry(d1, 10, 1, 1),
                new LootContainerContentTableEntry(d2, 10, 1, 1));

            Assert.That(TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out string error), Is.True, error);

            int triggers = 0;
            const int totalRuns = 1000;
            for (ulong seed = 1; seed <= totalRuns; seed++)
            {
                Assert.That(LootContainerContentRoller.TryRoll(
                    snapshot, seed, 0, hasChaosCoin: true, out IReadOnlyList<LootEntry> result, out error), Is.True, error);

                if (result.Count == 2)
                {
                    triggers++;
                }
            }

            // Expect approximately 20% (around 200 out of 1000, tolerance 15% to 25%)
            Assert.That(triggers, Is.InRange(150, 250), $"Expected ~200 triggers out of 1000, but got {triggers}.");
        }

        private LootDefinition CreateDefinition(string id)
        {
            var def = ScriptableObject.CreateInstance<LootDefinition>();
            _created.Add(def);
            typeof(LootDefinition)
                .GetField("_id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(def, id);
            typeof(LootDefinition)
                .GetField("_displayName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(def, id);
            typeof(LootDefinition)
                .GetField("_icon", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(def, _sprite);
            typeof(LootDefinition)
                .GetField("_worldSprite", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(def, _sprite);
            typeof(LootDefinition)
                .GetField("_category", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(def, LootCategory.Resource);
            return def;
        }

        private void SetCatalog(params LootDefinition[] definitions)
        {
            typeof(LootDefinitionCatalog)
                .GetField("_definitions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_catalog, new List<LootDefinition>(definitions));
        }

        private LootContainerContentTable CreateTable(
            int minStacks,
            int maxStacks,
            bool allowEmpty,
            params LootContainerContentTableEntry[] entries)
        {
            var table = ScriptableObject.CreateInstance<LootContainerContentTable>();
            _created.Add(table);
            typeof(LootContainerContentTable)
                .GetField("_minimumDistinctStacks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(table, minStacks);
            typeof(LootContainerContentTable)
                .GetField("_maximumDistinctStacks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(table, maxStacks);
            typeof(LootContainerContentTable)
                .GetField("_allowEmpty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(table, allowEmpty);
            typeof(LootContainerContentTable)
                .GetField("_entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(table, new List<LootContainerContentTableEntry>(entries));
            return table;
        }

        private bool TrySnapshot(
            LootContainerContentTable table,
            out ValidatedLootContainerContentSnapshot snapshot,
            out string error)
        {
            return LootContainerContentTableValidation.TryCreateSnapshot(
                table,
                _catalog,
                10,
                30,
                out snapshot,
                out error);
        }
    }
}
