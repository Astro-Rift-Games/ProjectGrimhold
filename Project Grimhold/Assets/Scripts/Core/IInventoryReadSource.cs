using System;
using System.Collections.Generic;

/// <summary>
/// Neutral read contract for an inventory snapshot whose contents can change over time.
/// Implementations expose no mutation capability and remain responsible for their own
/// change-notification lifecycle.
/// </summary>
public interface IInventoryReadSource
{
    int SlotCapacity { get; }
    int Revision { get; }

    event Action Changed;

    bool TryGetLootContent(out IReadOnlyList<LootEntry> content);
}
