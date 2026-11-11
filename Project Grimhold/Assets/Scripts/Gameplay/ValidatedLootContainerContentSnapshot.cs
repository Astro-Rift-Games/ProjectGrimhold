using System;

/// <summary>
/// Immutable runtime copy of a validated loot table and its effective container limits.
/// </summary>
public sealed class ValidatedLootContainerContentSnapshot
{
    public readonly struct Entry
    {
        public LootId LootId { get; }
        public int CatalogIndex { get; }
        public ulong Weight { get; }
        public int MinimumAmount { get; }
        public int MaximumAmount { get; }

        /// <summary>Maximum units of this item in one container stack (weapons are individual units, so 1).</summary>
        public int StackMax { get; }

        internal Entry(
            LootId lootId,
            int catalogIndex,
            ulong weight,
            int minimumAmount,
            int maximumAmount,
            int stackMax)
        {
            LootId = lootId;
            CatalogIndex = catalogIndex;
            Weight = weight;
            MinimumAmount = minimumAmount;
            MaximumAmount = maximumAmount;
            StackMax = stackMax;
        }
    }

    private readonly Entry[] _entries;

    public int EntryCount => _entries.Length;
    public int MinimumDistinctStacks { get; }
    public int MaximumDistinctStacks { get; }
    public bool AllowEmpty { get; }
    public ulong TotalWeight { get; }
    public int SlotCapacity { get; }
    public int NetworkCapacity { get; }

    internal ValidatedLootContainerContentSnapshot(
        Entry[] entries,
        int minimumDistinctStacks,
        int maximumDistinctStacks,
        bool allowEmpty,
        ulong totalWeight,
        int slotCapacity,
        int networkCapacity)
    {
        _entries = entries ?? Array.Empty<Entry>();
        MinimumDistinctStacks = minimumDistinctStacks;
        MaximumDistinctStacks = maximumDistinctStacks;
        AllowEmpty = allowEmpty;
        TotalWeight = totalWeight;
        SlotCapacity = slotCapacity;
        NetworkCapacity = networkCapacity;
    }

    /// <summary>
    /// Returns one copied, validated entry without exposing the snapshot's backing array.
    /// </summary>
    public Entry GetEntry(int index)
    {
        if (index < 0 || index >= _entries.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _entries[index];
    }
}
