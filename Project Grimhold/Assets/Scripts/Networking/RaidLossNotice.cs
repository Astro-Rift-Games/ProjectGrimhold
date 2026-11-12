using System.Collections.Generic;

/// <summary>
/// Immutable record of a technical Raid failure that consumed the local loadout. It is captured from
/// the application-owned reservation before the loss is cleared, so it never depends on the runner.
/// </summary>
public sealed class RaidLossNotice
{
    private RaidLossNotice(RaidLossNoticeReason reason, RaidResultsLootSummary lost)
    {
        Reason = reason;
        Lost = lost;
    }

    public RaidLossNoticeReason Reason { get; }
    public RaidResultsLootSummary Lost { get; }

    /// <summary>
    /// Describes everything the reservation carried into the Raid: reserved items plus prepared
    /// Equipment. Returns false when nothing was carried or the snapshot is inconsistent.
    /// </summary>
    public static bool TryCreate(
        RaidLossNoticeReason reason,
        PendingLoadoutReservation reservation,
        out RaidLossNotice notice,
        out string error)
    {
        notice = null;
        error = null;
        if (reservation == null)
        {
            error = "A loadout reservation is required.";
            return false;
        }

        if (!ProfileInventoryRules.TryAggregate(reservation.Items, out List<LootEntry> carried) ||
            !RaidAdmissionData.TryAppendPreparedEquipment(carried, reservation.PreparedEquipment, out _))
        {
            error = "The reservation snapshot is inconsistent.";
            return false;
        }

        List<LootEntry> folded = FoldByLootId(carried);
        if (folded.Count == 0)
        {
            error = "The reservation carried nothing.";
            return false;
        }

        if (!RaidResultsLootSummaryBuilder.TryBuildLost(folded, out RaidResultsLootSummary lost, out error))
        {
            return false;
        }

        notice = new RaidLossNotice(reason, lost);
        return true;
    }

    // Equipment units stay separate entries after aggregation; the Results list shows one line per definition.
    private static List<LootEntry> FoldByLootId(List<LootEntry> entries)
    {
        var folded = new List<LootEntry>(entries.Count);
        var indices = new Dictionary<LootId, int>();
        for (int index = 0; index < entries.Count; index++)
        {
            LootEntry entry = entries[index];
            if (indices.TryGetValue(entry.LootId, out int existing))
            {
                folded[existing] = new LootEntry(entry.LootId, folded[existing].Amount + entry.Amount);
                continue;
            }

            indices.Add(entry.LootId, folded.Count);
            folded.Add(new LootEntry(entry.LootId, entry.Amount));
        }

        return folded;
    }
}
