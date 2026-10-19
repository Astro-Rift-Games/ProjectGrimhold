using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class RaidResultsLootSummaryBuilderTests
    {
        private static readonly LootId Coin = new("coin");
        private static readonly LootId Sword = new("sword");
        private static readonly LootId Herb = new("herb");

        [Test]
        public void TryBuildExtracted_KeepsOnlyEligibleQuantitiesAndSumsTheirValue()
        {
            var eligibility = CreateEligibility(
                new RaidLootEligibilityEntry(Coin, 10, 4),
                new RaidLootEligibilityEntry(Sword, 1, 0),
                new RaidLootEligibilityEntry(Herb, 3, 3));
            var values = new FakeValueSource { [Coin] = 5, [Sword] = 100, [Herb] = 7 };

            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildExtracted(
                    eligibility, values, out RaidResultsLootSummary summary, out string error),
                Is.True,
                error);

            Assert.That(summary.Kind, Is.EqualTo(RaidResultsLootKind.Extracted));
            Assert.That(summary.Entries.Count, Is.EqualTo(2));
            Assert.That(summary.Entries[0], Is.EqualTo(new RaidResultsLootEntry(Coin, 4)));
            Assert.That(summary.Entries[1], Is.EqualTo(new RaidResultsLootEntry(Herb, 3)));
            Assert.That(summary.TotalValue, Is.EqualTo(4 * 5 + 3 * 7));
        }

        [Test]
        public void TryBuildExtracted_OmitsEverythingBroughtFromTown()
        {
            var eligibility = CreateEligibility(new RaidLootEligibilityEntry(Sword, 1, 0));

            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildExtracted(
                    eligibility, new FakeValueSource { [Sword] = 100 },
                    out RaidResultsLootSummary summary, out _),
                Is.True);

            Assert.That(summary.Entries, Is.Empty);
            Assert.That(summary.TotalValue, Is.Zero);
        }

        [Test]
        public void TryBuildExtracted_FailsWhenAnEligibleEntryHasNoValue()
        {
            var eligibility = CreateEligibility(new RaidLootEligibilityEntry(Coin, 2, 2));

            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildExtracted(
                    eligibility, new FakeValueSource(), out _, out string error),
                Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void TryBuildLost_ReportsEveryCarriedUnitIncludingTownItemsWithoutNeedingValues()
        {
            LootEntry[] carried = { new(Coin, 10), new(Sword, 1) };

            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildLost(
                    carried, out RaidResultsLootSummary summary, out string error),
                Is.True,
                error);

            Assert.That(summary.Kind, Is.EqualTo(RaidResultsLootKind.Lost));
            Assert.That(summary.Entries.Count, Is.EqualTo(2));
            Assert.That(summary.Entries[0], Is.EqualTo(new RaidResultsLootEntry(Coin, 10)));
            Assert.That(summary.Entries[1], Is.EqualTo(new RaidResultsLootEntry(Sword, 1)));
            Assert.That(summary.TotalValue, Is.Zero);
        }

        [Test]
        public void TryBuildLost_AcceptsAnEmptyInventory()
        {
            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildLost(
                    new List<LootEntry>(), out RaidResultsLootSummary summary, out _),
                Is.True);

            Assert.That(summary.Entries, Is.Empty);
        }

        [Test]
        public void TryBuildLost_RejectsDuplicateOrInvalidEntries()
        {
            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildLost(
                    new[] { new LootEntry(Coin, 1), new LootEntry(Coin, 2) }, out _, out _),
                Is.False);
            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildLost(
                    new[] { new LootEntry(Coin, 0) }, out _, out _),
                Is.False);
            Assert.That(RaidResultsLootSummaryBuilder.TryBuildLost(null, out _, out _), Is.False);
        }

        [Test]
        public void TryBuildExtracted_RejectsMissingValueSource()
        {
            var eligibility = CreateEligibility(new RaidLootEligibilityEntry(Coin, 1, 1));

            Assert.That(
                RaidResultsLootSummaryBuilder.TryBuildExtracted(eligibility, null, out _, out _),
                Is.False);
        }

        private static RaidLootEligibilitySnapshot CreateEligibility(
            params RaidLootEligibilityEntry[] entries)
        {
            long total = 0;
            long eligible = 0;
            foreach (RaidLootEligibilityEntry entry in entries)
            {
                total += entry.TotalAmount;
                eligible += entry.EligibleAmount;
            }

            return new RaidLootEligibilitySnapshot(entries, total, eligible);
        }

        private sealed class FakeValueSource : IRaidLootValueSource
        {
            private readonly Dictionary<LootId, long> _values = new();

            public long this[LootId lootId]
            {
                set => _values[lootId] = value;
            }

            public bool TryGetValuePerUnit(LootId lootId, out long valuePerUnit) =>
                _values.TryGetValue(lootId, out valuePerUnit);
        }
    }
}
