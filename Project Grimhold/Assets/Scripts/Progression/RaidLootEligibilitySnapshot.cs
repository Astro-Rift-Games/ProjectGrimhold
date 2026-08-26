using System;
using System.Collections.Generic;

/// <summary>Immutable eligibility projection of one extracted Raid loot snapshot.</summary>
public sealed class RaidLootEligibilitySnapshot
{
    private readonly RaidLootEligibilityEntry[] _entries;
    private readonly IReadOnlyList<RaidLootEligibilityEntry> _readOnlyEntries;

    internal RaidLootEligibilitySnapshot(
        RaidLootEligibilityEntry[] entries,
        long totalAmount,
        long eligibleAmount)
    {
        _entries = entries ?? Array.Empty<RaidLootEligibilityEntry>();
        _readOnlyEntries = Array.AsReadOnly(_entries);
        TotalAmount = totalAmount;
        EligibleAmount = eligibleAmount;
    }

    public IReadOnlyList<RaidLootEligibilityEntry> Entries => _readOnlyEntries;
    public long TotalAmount { get; }
    public long EligibleAmount { get; }
    public long IneligibleAmount => TotalAmount - EligibleAmount;

    public bool TryGetEntry(LootId lootId, out RaidLootEligibilityEntry entry)
    {
        entry = default;
        if (!lootId.IsValid)
        {
            return false;
        }

        for (int index = 0; index < _entries.Length; index++)
        {
            if (_entries[index].LootId == lootId)
            {
                entry = _entries[index];
                return true;
            }
        }

        return false;
    }
}
