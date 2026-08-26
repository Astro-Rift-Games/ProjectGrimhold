using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class ExtractedLootExperienceCalculatorTests
    {
        private sealed class StubValueSource : IRaidLootValueSource
        {
            private readonly Dictionary<LootId, long> _values = new();

            public StubValueSource Add(string lootId, long value)
            {
                _values[new LootId(lootId)] = value;
                return this;
            }

            public bool TryGetValuePerUnit(LootId lootId, out long valuePerUnit) =>
                _values.TryGetValue(lootId, out valuePerUnit);
        }

        [Test]
        public void Calculate_CoversDungeonEnemyOwnTeammateMixedAndMultipleLootValues()
        {
            AssertCalculation(Entry("dungeon", 5, 5), new StubValueSource().Add("dungeon", 20), 100, 10);
            AssertCalculation(Entry("enemy", 5, 5), new StubValueSource().Add("enemy", 20), 100, 10);
            AssertCalculation(Entry("own", 5, 0), new StubValueSource(), 0, 0);
            AssertCalculation(Entry("teammate", 5, 0), new StubValueSource(), 0, 0);

            RaidLootEligibilitySnapshot mixed = Snapshot(
                Entry("mixed", 11, 6));
            AssertCalculation(mixed, new StubValueSource().Add("mixed", 50), 300, 30);

            RaidLootEligibilitySnapshot multiple = Snapshot(
                Entry("loot_a", 2, 2),
                Entry("loot_b", 3, 3));
            AssertCalculation(
                multiple,
                new StubValueSource().Add("loot_a", 20).Add("loot_b", 50),
                190,
                19);
        }

        [Test]
        public void Calculate_FloorsAndUsesConfigurableBasisPoints()
        {
            RaidLootEligibilitySnapshot snapshot = Snapshot(Entry("loot", 1, 1));
            var values = new StubValueSource().Add("loot", 99);

            AssertCalculation(snapshot, values, 99, 9);
            AssertCalculation(snapshot, values, 99, 24, 2_500);
        }

        [Test]
        public void Calculate_RejectsInvalidPercentageMissingValueAndInvalidValueAtomically()
        {
            RaidLootEligibilitySnapshot snapshot = Snapshot(Entry("loot", 1, 1));
            AssertRejected(snapshot, new StubValueSource().Add("loot", 10), 0,
                ExtractedLootExperienceCalculationFailure.InvalidPercentage);
            AssertRejected(snapshot, new StubValueSource().Add("loot", 10), 10_001,
                ExtractedLootExperienceCalculationFailure.InvalidPercentage);
            AssertRejected(snapshot, new StubValueSource(), 1_000,
                ExtractedLootExperienceCalculationFailure.MissingOrInvalidValue);
            AssertRejected(snapshot, new StubValueSource().Add("loot", 0), 1_000,
                ExtractedLootExperienceCalculationFailure.MissingOrInvalidValue);
        }

        [Test]
        public void Calculate_DoesNotRequireValueForFullyIneligibleLoot()
        {
            RaidLootEligibilitySnapshot snapshot = Snapshot(
                Entry("eligible", 1, 1),
                Entry("ineligible", 3, 0));

            AssertCalculation(snapshot, new StubValueSource().Add("eligible", 100), 100, 10);
        }

        [Test]
        public void Calculate_RejectsProductAndTotalOverflowWithoutPartialResult()
        {
            AssertRejected(
                Snapshot(Entry("loot", int.MaxValue, int.MaxValue)),
                new StubValueSource().Add("loot", long.MaxValue),
                1_000,
                ExtractedLootExperienceCalculationFailure.ValueMultiplicationOverflow);

            AssertRejected(
                Snapshot(Entry("a", 1, 1), Entry("b", 1, 1)),
                new StubValueSource().Add("a", long.MaxValue).Add("b", 1),
                1_000,
                ExtractedLootExperienceCalculationFailure.EligibleValueOverflow);
        }

        [Test]
        public void Calculate_RejectsInvalidDuplicateAndInconsistentEntries()
        {
            RaidLootEligibilityEntry valid = Entry("loot", 1, 1);
            AssertRejected(
                SnapshotWithTotals(new[] { valid, valid }, 2, 2),
                new StubValueSource().Add("loot", 1),
                1_000,
                ExtractedLootExperienceCalculationFailure.DuplicateLootId);
            AssertRejected(
                SnapshotWithTotals(new[] { valid }, 2, 1),
                new StubValueSource().Add("loot", 1),
                1_000,
                ExtractedLootExperienceCalculationFailure.InconsistentEligibilityTotals);
            AssertRejected(
                SnapshotWithTotals(new[] { new RaidLootEligibilityEntry(new LootId("loot"), 0, 0) }, 0, 0),
                new StubValueSource(),
                1_000,
                ExtractedLootExperienceCalculationFailure.InvalidEligibilityEntry);
        }

        [Test]
        public void Calculate_IsRepeatableAndDoesNotModifyEligibilitySnapshot()
        {
            RaidLootEligibilitySnapshot snapshot = Snapshot(Entry("loot", 7, 4));
            RaidLootEligibilityEntry original = snapshot.Entries[0];
            var values = new StubValueSource().Add("loot", 125);

            AssertCalculation(snapshot, values, 500, 50);
            AssertCalculation(snapshot, values, 500, 50);
            Assert.That(snapshot.Entries[0], Is.EqualTo(original));
            Assert.That(snapshot.TotalAmount, Is.EqualTo(7));
            Assert.That(snapshot.EligibleAmount, Is.EqualTo(4));
        }

        [Test]
        public void Candidate_IsBoundToExactlyOnePositiveResultSequence()
        {
            var calculation = new ExtractedLootExperienceCalculation(300, 30);
            var candidate = new ExtractedLootExperienceCandidate(4, calculation);

            Assert.That(candidate.IsValid, Is.True);
            Assert.That(candidate.Matches(4), Is.True);
            Assert.That(candidate.Matches(3), Is.False);
            Assert.That(new ExtractedLootExperienceCandidate(0, calculation).IsValid, Is.False);
        }

        private static RaidLootEligibilityEntry Entry(string lootId, int total, int eligible) =>
            new(new LootId(lootId), total, eligible);

        private static RaidLootEligibilitySnapshot Snapshot(params RaidLootEligibilityEntry[] entries)
        {
            long total = 0;
            long eligible = 0;
            for (int index = 0; index < entries.Length; index++)
            {
                total += entries[index].TotalAmount;
                eligible += entries[index].EligibleAmount;
            }

            return SnapshotWithTotals(entries, total, eligible);
        }

        private static RaidLootEligibilitySnapshot SnapshotWithTotals(
            RaidLootEligibilityEntry[] entries,
            long total,
            long eligible)
        {
            ConstructorInfo constructor = typeof(RaidLootEligibilitySnapshot).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(RaidLootEligibilityEntry[]), typeof(long), typeof(long) },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (RaidLootEligibilitySnapshot)constructor.Invoke(new object[] { entries, total, eligible });
        }

        private static void AssertCalculation(
            RaidLootEligibilityEntry entry,
            IRaidLootValueSource values,
            long expectedValue,
            long expectedExperience,
            int basisPoints = 1_000) =>
            AssertCalculation(Snapshot(entry), values, expectedValue, expectedExperience, basisPoints);

        private static void AssertCalculation(
            RaidLootEligibilitySnapshot snapshot,
            IRaidLootValueSource values,
            long expectedValue,
            long expectedExperience,
            int basisPoints = 1_000)
        {
            Assert.That(
                ExtractedLootExperienceCalculator.TryCalculate(
                    snapshot,
                    values,
                    basisPoints,
                    out ExtractedLootExperienceCalculation result,
                    out ExtractedLootExperienceCalculationFailure failure),
                Is.True,
                failure.ToString());
            Assert.That(result.EligibleValue, Is.EqualTo(expectedValue));
            Assert.That(result.AwardedExperience, Is.EqualTo(expectedExperience));
        }

        private static void AssertRejected(
            RaidLootEligibilitySnapshot snapshot,
            IRaidLootValueSource values,
            int basisPoints,
            ExtractedLootExperienceCalculationFailure expectedFailure)
        {
            Assert.That(
                ExtractedLootExperienceCalculator.TryCalculate(
                    snapshot,
                    values,
                    basisPoints,
                    out ExtractedLootExperienceCalculation result,
                    out ExtractedLootExperienceCalculationFailure failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(expectedFailure));
            Assert.That(result, Is.EqualTo(default(ExtractedLootExperienceCalculation)));
        }
    }
}
