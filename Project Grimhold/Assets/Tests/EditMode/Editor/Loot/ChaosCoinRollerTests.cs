using System.Collections.Generic;
using System.Linq;
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
            LootDefinition d1 = CreateDefinition("item_1", 100);
            LootDefinition d2 = CreateDefinition("item_2", 100);
            LootDefinition d3 = CreateDefinition("item_3", 100);
            LootDefinition d4 = CreateDefinition("item_4", 100);
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
                // A Coin merge may raise the base stack's amount, so identity is compared by LootId.
                Assert.That(withCoin.Select(entry => entry.LootId), Does.Contain(withoutCoin[0].LootId));
                Assert.That(Units(withCoin), Is.InRange(Units(withoutCoin), Units(withoutCoin) + 1));
            }
        }

        [Test]
        public void Roller_ChaosCoinAndLuckAreMutuallyIndependent()
        {
            LootDefinition d1 = CreateDefinition("item_1", 100);
            LootDefinition d2 = CreateDefinition("item_2", 100);
            LootDefinition d3 = CreateDefinition("item_3", 100);
            LootDefinition d4 = CreateDefinition("item_4", 100);
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

                bool coinTriggered = Units(coinOnly) == 2;

                // Roll with both Luck (100% chance = 10000 bp) and Chaos Coin
                Assert.That(LootContainerContentRoller.TryRoll(
                    snapshot, seed, 10000, hasChaosCoin: true, out IReadOnlyList<LootEntry> both, out error), Is.True, error);

                if (coinTriggered)
                {
                    // If Chaos coin triggered with Luck = 0, with 100% Luck it must have 3 stacks (base + luck + coin)
                    Assert.That(Units(both), Is.EqualTo(3), $"Seed {seed}: Expected both Luck and Chaos Coin to roll bonus stacks.");
                    bothTriggeredCount++;
                }
                else
                {
                    // If Chaos coin did not trigger, with 100% Luck it must have exactly 2 stacks (base + luck)
                    Assert.That(Units(both), Is.EqualTo(2), $"Seed {seed}: Expected only Luck bonus stack.");
                }
            }

            Assert.That(bothTriggeredCount, Is.GreaterThan(10), "Expected multiple seeds where Chaos Coin triggered.");
        }

        [Test]
        public void Roller_ChaosCoinRateMatchesExpected20PercentBasisPoints()
        {
            LootDefinition d1 = CreateDefinition("item_1", 100);
            LootDefinition d2 = CreateDefinition("item_2", 100);
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

                // Each entry rolls exactly 1 unit, so a Coin trigger is the only way to reach 2 units.
                if (Units(result) == 2)
                {
                    triggers++;
                }
            }

            // Expect approximately 20% (around 200 out of 1000, tolerance 15% to 25%)
            Assert.That(triggers, Is.InRange(150, 250), $"Expected ~200 triggers out of 1000, but got {triggers}.");
        }

        [Test]
        public void Roller_ChaosCoinRateIsIndependentOfLuckWhenLuckFillsTheContainer()
        {
            // 2 entries, capacity 2, base 1: Luck 10000 always fills the container.
            ValidatedLootContainerContentSnapshot snapshot = CreateStackableSnapshot(
                entryCount: 2, stackMax: 100, amount: 1, slotCapacity: 2);

            const int totalRuns = 2000;
            int withoutLuck = CountCoinTriggers(snapshot, totalRuns, luckBasisPoints: 0);
            int withLuck = CountCoinTriggers(snapshot, totalRuns, luckBasisPoints: 10000);

            // Expected 20% of 2000 = 400, sd = sqrt(2000 * 0.2 * 0.8) ~= 17.9; +-90 is ~5 sd.
            Assert.That(withoutLuck, Is.InRange(310, 490));
            Assert.That(withLuck, Is.InRange(310, 490));
            // Same seeds share the Coin stream, so Luck must not change a single trigger.
            Assert.That(withLuck, Is.EqualTo(withoutLuck));
        }

        [Test]
        public void Roller_ChaosCoinAddsNewEntryAtFullRateWithFreeCapacityForNonStackableItems()
        {
            // Free capacity: the Coin prefers table entries not yet in the result, so a repeat draw
            // must not silently swallow the 20% (non-stackable items cannot merge).
            ValidatedLootContainerContentSnapshot snapshot = CreateStackableSnapshot(
                entryCount: 2, stackMax: 1, amount: 1, slotCapacity: 10);

            const int totalRuns = 2000;
            int added = 0;
            for (ulong seed = 1; seed <= totalRuns; seed++)
            {
                IReadOnlyList<LootEntry> result = Roll(snapshot, seed, 0, true);
                if (result.Count == 2)
                {
                    added++;
                }
            }

            // Expected 400, sd ~= 17.9; +-90 is ~5 sd.
            Assert.That(added, Is.InRange(310, 490));
        }

        [Test]
        public void Roller_ChaosCoinAndLuckBothAddUnitsInTheSameResolution()
        {
            ValidatedLootContainerContentSnapshot snapshot = CreateStackableSnapshot(
                entryCount: 4, stackMax: 100, amount: 1, slotCapacity: 10);

            int both = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                IReadOnlyList<LootEntry> baseAndLuck = Roll(snapshot, seed, 10000, false);
                IReadOnlyList<LootEntry> all = Roll(snapshot, seed, 10000, true);

                Assert.That(baseAndLuck.Count, Is.EqualTo(2), $"Seed {seed}: Luck must add its stack.");
                if (Units(all) > Units(baseAndLuck))
                {
                    both++;
                    Assert.That(Units(all), Is.EqualTo(3), $"Seed {seed}: base + Luck + Coin units.");
                    Assert.That(all.Count, Is.InRange(2, 3));
                }
            }

            Assert.That(both, Is.GreaterThan(20));
        }

        [Test]
        public void Roller_ChaosCoinMergesIntoExistingStackWhenContainerIsFullWithoutExceedingStackMax()
        {
            // Base + Luck fill capacity 2 with 5 units each; StackMax 6 leaves room for 1 unit.
            ValidatedLootContainerContentSnapshot snapshot = CreateStackableSnapshot(
                entryCount: 2, stackMax: 6, amount: 5, slotCapacity: 2);

            int merged = 0;
            for (ulong seed = 1; seed <= 500; seed++)
            {
                IReadOnlyList<LootEntry> without = Roll(snapshot, seed, 10000, false);
                IReadOnlyList<LootEntry> with = Roll(snapshot, seed, 10000, true);

                Assert.That(with.Count, Is.EqualTo(2));
                for (int i = 0; i < with.Count; i++)
                {
                    Assert.That(with[i].Amount, Is.LessThanOrEqualTo(6), $"Seed {seed}: StackMax exceeded.");
                }

                if (Units(with) != Units(without))
                {
                    merged++;
                    Assert.That(Units(with), Is.EqualTo(11), $"Seed {seed}: Coin adds the clamped remainder.");
                }
            }

            Assert.That(merged, Is.InRange(60, 140), "Expected ~20% of 500 seeds to merge.");
        }

        [Test]
        public void Roller_ChaosCoinYieldsNothingWhenNoResultEntryCanTakeIt()
        {
            // Documented rule: full container + every entry at StackMax (or non-stackable) => no Coin effect.
            ValidatedLootContainerContentSnapshot atMax = CreateStackableSnapshot(
                entryCount: 2, stackMax: 5, amount: 5, slotCapacity: 2);
            ValidatedLootContainerContentSnapshot nonStackable = CreateStackableSnapshot(
                entryCount: 2, stackMax: 1, amount: 1, slotCapacity: 2);

            for (ulong seed = 1; seed <= 300; seed++)
            {
                Assert.That(Roll(atMax, seed, 10000, true), Is.EqualTo(Roll(atMax, seed, 10000, false)));
                Assert.That(Roll(nonStackable, seed, 10000, true), Is.EqualTo(Roll(nonStackable, seed, 10000, false)));
            }
        }

        [Test]
        public void Roller_ChaosCoinResultAlwaysPassesInitializationRules()
        {
            var layouts = new[]
            {
                (entries: 2, stackMax: 100, amount: 1, capacity: 2),
                (entries: 2, stackMax: 6, amount: 5, capacity: 2),
                (entries: 2, stackMax: 1, amount: 1, capacity: 2),
                (entries: 4, stackMax: 100, amount: 3, capacity: 10),
                (entries: 4, stackMax: 1, amount: 1, capacity: 10),
            };

            foreach (var layout in layouts)
            {
                ValidatedLootContainerContentSnapshot snapshot = CreateStackableSnapshot(
                    layout.entries, layout.stackMax, layout.amount, layout.capacity);
                for (ulong seed = 1; seed <= 300; seed++)
                {
                    foreach (int luck in new[] { 0, 10000 })
                    {
                        IReadOnlyList<LootEntry> result = Roll(snapshot, seed, luck, true);
                        Assert.That(result.Count, Is.LessThanOrEqualTo(layout.capacity));
                        Assert.That(LootContainerInitializationRules.TryBuild(
                            result, _catalog, layout.capacity, 30,
                            out _, out string error), Is.True, $"Seed {seed} luck {luck}: {error}");
                        for (int i = 0; i < result.Count; i++)
                        {
                            Assert.That(result[i].Amount, Is.LessThanOrEqualTo(Mathf.Max(layout.stackMax, layout.amount)));
                        }
                    }
                }
            }
        }

        private LootDefinition CreateWeaponDefinitionWithStackMax(string id, int stackMax)
        {
            // A weapon needs a valid WeaponDefinition, so clone a real one and override identity and StackMax.
            LootDefinition source = null;
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:LootDefinition"))
            {
                var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<LootDefinition>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.Category == LootCategory.Weapon)
                {
                    source = candidate;
                    break;
                }
            }

            Assert.That(source, Is.Not.Null, "No Weapon LootDefinition asset available for the fixture.");
            LootDefinition clone = Object.Instantiate(source);
            _created.Add(clone);
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(LootDefinition).GetField("_id", flags).SetValue(clone, id);
            typeof(LootDefinition).GetField("_stackMax", flags).SetValue(clone, stackMax);
            return clone;
        }

        private int _snapshotFixtureCounter;

        private ValidatedLootContainerContentSnapshot CreateStackableSnapshot(
            int entryCount, int stackMax, int amount, int slotCapacity)
        {
            _snapshotFixtureCounter++;
            // The catalog caches its index lookup, so every fixture gets a fresh catalog.
            _catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _created.Add(_catalog);
            var definitions = new LootDefinition[entryCount];
            var entries = new LootContainerContentTableEntry[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                definitions[i] = CreateDefinition($"stack_{_snapshotFixtureCounter}_{i}", stackMax);
                entries[i] = new LootContainerContentTableEntry(definitions[i], 10, amount, amount);
            }

            SetCatalog(definitions);
            LootContainerContentTable table = CreateTable(1, 1, false, entries);
            Assert.That(LootContainerContentTableValidation.TryCreateSnapshot(
                table, _catalog, slotCapacity, 30, out ValidatedLootContainerContentSnapshot snapshot, out string error),
                Is.True, error);
            return snapshot;
        }

        private static IReadOnlyList<LootEntry> Roll(
            ValidatedLootContainerContentSnapshot snapshot, ulong seed, int luck, bool coin)
        {
            Assert.That(LootContainerContentRoller.TryRoll(
                snapshot, seed, luck, coin, out IReadOnlyList<LootEntry> result, out string error), Is.True, error);
            return result;
        }

        private static int Units(IReadOnlyList<LootEntry> entries)
        {
            int total = 0;
            for (int i = 0; i < entries.Count; i++) total += entries[i].Amount;
            return total;
        }

        private static int CountCoinTriggers(
            ValidatedLootContainerContentSnapshot snapshot, int runs, int luckBasisPoints)
        {
            int triggers = 0;
            for (ulong seed = 1; seed <= (ulong)runs; seed++)
            {
                if (Units(Roll(snapshot, seed, luckBasisPoints, true)) >
                    Units(Roll(snapshot, seed, luckBasisPoints, false)))
                {
                    triggers++;
                }
            }

            return triggers;
        }

        [Test]
        public void Snapshot_EntriesCarryStackMaxAndFoldWeaponsToOne()
        {
            LootDefinition stackable = CreateDefinition("stackable", stackMax: 7);
            LootDefinition single = CreateDefinition("single");
            LootDefinition weapon = CreateWeaponDefinitionWithStackMax("weapon", 5);
            SetCatalog(stackable, single, weapon);
            LootContainerContentTable table = CreateTable(
                1, 1, false,
                new LootContainerContentTableEntry(stackable, 10, 1, 1),
                new LootContainerContentTableEntry(single, 10, 1, 1),
                new LootContainerContentTableEntry(weapon, 10, 1, 1));

            Assert.That(TrySnapshot(table, out ValidatedLootContainerContentSnapshot snapshot, out string error), Is.True, error);

            var byId = new Dictionary<string, int>();
            for (int i = 0; i < snapshot.EntryCount; i++)
            {
                byId[snapshot.GetEntry(i).LootId.Value] = snapshot.GetEntry(i).StackMax;
            }

            Assert.That(byId["stackable"], Is.EqualTo(7));
            Assert.That(byId["single"], Is.EqualTo(1));
            Assert.That(byId["weapon"], Is.EqualTo(1));
        }

        private LootDefinition CreateDefinition(
            string id,
            int stackMax = 1,
            LootCategory category = LootCategory.Resource)
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
                ?.SetValue(def, category);
            typeof(LootDefinition)
                .GetField("_stackMax", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(def, stackMax);
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
                ?.SetValue(table, entries);
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
