using System;
using System.Collections.Generic;

/// <summary>
/// A snapshot of an extraction commit that has been saved locally but not yet confirmed by the authoritative backend.
/// </summary>
public sealed class PendingExtractionCommit
{
    public ExtractionReceipt Receipt { get; }
    public IReadOnlyList<StashItem> Items { get; }
    public long ConsolidatedExperience { get; }
    public int ResultingLevel { get; }

    public PendingExtractionCommit(
        ExtractionReceipt receipt,
        IReadOnlyList<StashItem> items,
        long consolidatedExperience,
        int resultingLevel)
    {
        Receipt = receipt;
        Items = items ?? Array.Empty<StashItem>();
        ConsolidatedExperience = consolidatedExperience;
        ResultingLevel = resultingLevel;
    }
}
