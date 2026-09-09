using System;
using System.Collections.Generic;

/// <summary>
/// A snapshot of an extraction commit that has been saved locally but not yet confirmed by the authoritative backend.
/// </summary>
public sealed class PendingExtractionCommit
{
    public ExtractionReceipt Receipt { get; }
    public IReadOnlyList<StashItem> Items { get; }
    public PreparedEquipmentLoadout PreparedEquipment { get; }
    public long ConsolidatedExperience { get; }
    public int ResultingLevel { get; }

    public PendingExtractionCommit(
        ExtractionReceipt receipt,
        IReadOnlyList<StashItem> items,
        PreparedEquipmentLoadout preparedEquipment,
        long consolidatedExperience,
        int resultingLevel)
    {
        Receipt = receipt;
        Items = items ?? Array.Empty<StashItem>();
        PreparedEquipment = preparedEquipment;
        ConsolidatedExperience = consolidatedExperience;
        ResultingLevel = resultingLevel;
    }
}
