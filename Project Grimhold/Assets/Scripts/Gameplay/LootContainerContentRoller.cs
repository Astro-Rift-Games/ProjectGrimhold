using System;
using System.Collections.Generic;

/// <summary>
/// Produces deterministic weighted loot selections from an immutable validated snapshot.
/// </summary>
public static class LootContainerContentRoller
{
    private struct DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong value = _state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        public ulong NextBelow(ulong exclusiveUpperBound)
        {
            if (exclusiveUpperBound == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
            }

            ulong threshold = unchecked(0UL - exclusiveUpperBound) % exclusiveUpperBound;
            while (true)
            {
                ulong value = NextUInt64();
                if (value >= threshold)
                {
                    return value % exclusiveUpperBound;
                }
            }
        }
    }

    private readonly struct RolledEntry
    {
        public int CatalogIndex { get; }
        public LootEntry Loot { get; }

        public RolledEntry(int catalogIndex, LootEntry loot)
        {
            CatalogIndex = catalogIndex;
            Loot = loot;
        }
    }

    /// <summary>
    /// Produces a complete deterministic roll without mutating the validated snapshot.
    /// Failure returns an empty result and no partial selection.
    /// </summary>
    public static bool TryRoll(
        ValidatedLootContainerContentSnapshot snapshot,
        ulong seed,
        out IReadOnlyList<LootEntry> result,
        out string error)
    {
        return TryRoll(
            snapshot,
            seed,
            additionalLootChanceBasisPoints: 0,
            out result,
            out error);
    }

    /// <summary>
    /// Produces the base roll plus at most one distinct bonus stack. The bonus uses
    /// an independent deterministic stream so changing Luck never changes base content.
    /// </summary>
    public static bool TryRoll(
        ValidatedLootContainerContentSnapshot snapshot,
        ulong seed,
        int additionalLootChanceBasisPoints,
        out IReadOnlyList<LootEntry> result,
        out string error)
    {
        result = Array.Empty<LootEntry>();
        error = null;

        if (snapshot == null)
        {
            error = "Validated loot snapshot is missing.";
            return false;
        }

        if (additionalLootChanceBasisPoints < 0 ||
            additionalLootChanceBasisPoints > CharacterDerivedStatisticsCalculator.BasisPointsDenominator)
        {
            error = "Additional Loot chance must be between 0 and 10000 basis points.";
            return false;
        }

        var random = new DeterministicRandom(seed);
        int stackCount = NextInclusive(
            ref random,
            snapshot.MinimumDistinctStacks,
            snapshot.MaximumDistinctStacks);

        if (stackCount == 0 && !snapshot.AllowEmpty)
        {
            error = "The roller produced an empty result for a table that disallows empty content.";
            return false;
        }

        if (stackCount > snapshot.EntryCount || stackCount > snapshot.SlotCapacity || stackCount > snapshot.NetworkCapacity)
        {
            error = "Requested stack count exceeds the validated snapshot capacity.";
            return false;
        }

        var candidates = new List<ValidatedLootContainerContentSnapshot.Entry>(snapshot.EntryCount);
        for (int i = 0; i < snapshot.EntryCount; i++)
        {
            candidates.Add(snapshot.GetEntry(i));
        }

        ulong remainingWeight = snapshot.TotalWeight;
        var rolled = new List<RolledEntry>(stackCount);
        for (int selection = 0; selection < stackCount; selection++)
        {
            if (candidates.Count == 0 || remainingWeight == 0)
            {
                error = "Validated candidates were exhausted before the roll completed.";
                return false;
            }

            int selectedIndex = SelectWeightedIndex(candidates, remainingWeight, ref random);
            if (selectedIndex < 0)
            {
                error = "Weighted candidate selection failed.";
                return false;
            }

            ValidatedLootContainerContentSnapshot.Entry selected = candidates[selectedIndex];
            int amount = NextInclusive(ref random, selected.MinimumAmount, selected.MaximumAmount);
            rolled.Add(new RolledEntry(
                selected.CatalogIndex,
                new LootEntry(selected.LootId, amount)));

            remainingWeight = checked(remainingWeight - selected.Weight);
            candidates.RemoveAt(selectedIndex);
        }

        if (additionalLootChanceBasisPoints > 0 && candidates.Count > 0 &&
            rolled.Count < snapshot.SlotCapacity && rolled.Count < snapshot.NetworkCapacity)
        {
            var bonusRandom = new DeterministicRandom(
                seed ^ 0xD1B54A32D192ED03UL);
            ulong chanceRoll = bonusRandom.NextBelow(
                CharacterDerivedStatisticsCalculator.BasisPointsDenominator);
            if (chanceRoll < (ulong)additionalLootChanceBasisPoints)
            {
                int selectedIndex = SelectWeightedIndex(
                    candidates,
                    remainingWeight,
                    ref bonusRandom);
                if (selectedIndex < 0)
                {
                    error = "Additional weighted candidate selection failed.";
                    return false;
                }

                ValidatedLootContainerContentSnapshot.Entry selected =
                    candidates[selectedIndex];
                int amount = NextInclusive(
                    ref bonusRandom,
                    selected.MinimumAmount,
                    selected.MaximumAmount);
                rolled.Add(new RolledEntry(
                    selected.CatalogIndex,
                    new LootEntry(selected.LootId, amount)));
            }
        }

        rolled.Sort((left, right) => left.CatalogIndex.CompareTo(right.CatalogIndex));
        var entries = new List<LootEntry>(rolled.Count);
        for (int i = 0; i < rolled.Count; i++)
        {
            entries.Add(rolled[i].Loot);
        }

        result = entries.AsReadOnly();
        return true;
    }

    private static int SelectWeightedIndex(
        IReadOnlyList<ValidatedLootContainerContentSnapshot.Entry> candidates,
        ulong totalWeight,
        ref DeterministicRandom random)
    {
        if (candidates.Count == 1)
        {
            return 0;
        }

        ulong target = random.NextBelow(totalWeight);
        ulong cumulative = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative = checked(cumulative + candidates[i].Weight);
            if (target < cumulative)
            {
                return i;
            }
        }

        return -1;
    }

    private static int NextInclusive(
        ref DeterministicRandom random,
        int minimum,
        int maximum)
    {
        if (minimum == maximum)
        {
            return minimum;
        }

        ulong width = (ulong)((long)maximum - minimum) + 1UL;
        ulong offset = random.NextBelow(width);
        long value = (long)minimum + (long)offset;
        return checked((int)value);
    }
}
