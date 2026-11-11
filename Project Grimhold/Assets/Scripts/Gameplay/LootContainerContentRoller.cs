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

    public const int ChaosCoinLootChanceBasisPoints = 2000;
    public const ulong ChaosCoinSalt = 0xC4A05C0192A7E11DUL;

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
        return TryRoll(
            snapshot,
            seed,
            additionalLootChanceBasisPoints,
            hasChaosCoin: false,
            out result,
            out error);
    }

    /// <summary>
    /// Produces the base roll plus optional independent Luck and Chaos Coin bonus stacks.
    /// The bonuses use independent deterministic streams so changing Luck or carrying Chaos Coin
    /// never changes base content, and both rolls are mutually independent.
    /// </summary>
    public static bool TryRoll(
        ValidatedLootContainerContentSnapshot snapshot,
        ulong seed,
        int additionalLootChanceBasisPoints,
        bool hasChaosCoin,
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
                remainingWeight = checked(remainingWeight - selected.Weight);
                candidates.RemoveAt(selectedIndex);
            }
        }

        if (hasChaosCoin && !TryApplyChaosCoin(snapshot, seed, rolled, out error))
        {
            return false;
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

    /// <summary>
    /// Applies the independent Chaos Coin roll. The 20% trigger never depends on remaining
    /// candidates or capacity. With free capacity the item is drawn from table entries not yet in the
    /// result; only when every entry is already present is it drawn from the full table and merged into
    /// its stack (clamped to StackMax). With a full container the amount is merged into a result entry
    /// that still has room below its StackMax. Without any such entry the Coin adds nothing.
    /// </summary>
    private static bool TryApplyChaosCoin(
        ValidatedLootContainerContentSnapshot snapshot,
        ulong seed,
        List<RolledEntry> rolled,
        out string error)
    {
        error = null;
        var chaosRandom = new DeterministicRandom(seed ^ ChaosCoinSalt);
        ulong chanceRoll = chaosRandom.NextBelow(
            CharacterDerivedStatisticsCalculator.BasisPointsDenominator);
        if (chanceRoll >= (ulong)ChaosCoinLootChanceBasisPoints)
        {
            return true;
        }

        bool hasFreeSlot = rolled.Count < snapshot.SlotCapacity && rolled.Count < snapshot.NetworkCapacity;
        var pool = new List<ValidatedLootContainerContentSnapshot.Entry>(snapshot.EntryCount);
        ulong poolWeight = 0;
        for (int i = 0; i < snapshot.EntryCount; i++)
        {
            ValidatedLootContainerContentSnapshot.Entry entry = snapshot.GetEntry(i);
            bool include = hasFreeSlot
                ? IndexOfRolled(rolled, entry.CatalogIndex) < 0
                : CanTakeMore(rolled, entry);
            if (!include)
            {
                continue;
            }

            pool.Add(entry);
            poolWeight = checked(poolWeight + entry.Weight);
        }

        if (pool.Count == 0 && hasFreeSlot)
        {
            // Every table entry is already present: draw from the full table and merge.
            for (int i = 0; i < snapshot.EntryCount; i++)
            {
                ValidatedLootContainerContentSnapshot.Entry entry = snapshot.GetEntry(i);
                pool.Add(entry);
                poolWeight = checked(poolWeight + entry.Weight);
            }
        }

        if (pool.Count == 0)
        {
            return true;
        }

        int selectedIndex = SelectWeightedIndex(pool, poolWeight, ref chaosRandom);
        if (selectedIndex < 0)
        {
            error = "Chaos coin weighted candidate selection failed.";
            return false;
        }

        ValidatedLootContainerContentSnapshot.Entry selected = pool[selectedIndex];
        int amount = NextInclusive(ref chaosRandom, selected.MinimumAmount, selected.MaximumAmount);
        int existing = IndexOfRolled(rolled, selected.CatalogIndex);
        if (existing < 0)
        {
            rolled.Add(new RolledEntry(selected.CatalogIndex, new LootEntry(selected.LootId, amount)));
            return true;
        }

        int current = rolled[existing].Loot.Amount;
        int merged = (int)Math.Min((long)current + amount, selected.StackMax);
        if (merged > current)
        {
            rolled[existing] = new RolledEntry(selected.CatalogIndex, new LootEntry(selected.LootId, merged));
        }

        return true;
    }

    private static bool CanTakeMore(
        List<RolledEntry> rolled,
        ValidatedLootContainerContentSnapshot.Entry entry)
    {
        int index = IndexOfRolled(rolled, entry.CatalogIndex);
        return index >= 0 && entry.StackMax > 1 && rolled[index].Loot.Amount < entry.StackMax;
    }

    private static int IndexOfRolled(List<RolledEntry> rolled, int catalogIndex)
    {
        for (int i = 0; i < rolled.Count; i++)
        {
            if (rolled[i].CatalogIndex == catalogIndex)
            {
                return i;
            }
        }

        return -1;
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
