using System;
using System.Collections.Generic;

/// <summary>
/// Immutable, presentation-only loot summary of one participant's Results screen.
/// It is captured by State Authority when the loot is resolved and never recalculated by the UI.
/// </summary>
public sealed class RaidResultsLootSummary
{
    private readonly RaidResultsLootEntry[] _entries;

    internal RaidResultsLootSummary(
        RaidResultsLootKind kind,
        RaidResultsLootEntry[] entries,
        long totalValue)
    {
        Kind = kind;
        _entries = entries ?? Array.Empty<RaidResultsLootEntry>();
        Entries = Array.AsReadOnly(_entries);
        TotalValue = totalValue;
    }

    public RaidResultsLootKind Kind { get; }
    public IReadOnlyList<RaidResultsLootEntry> Entries { get; }
    public long TotalValue { get; }
}
