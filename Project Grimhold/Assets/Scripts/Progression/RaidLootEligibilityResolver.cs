using System;
using System.Collections.Generic;

/// <summary>Pure classification of extracted quantities by immutable Raid origin and initial team.</summary>
public static class RaidLootEligibilityResolver
{
    public static bool TryResolve(
        RaidParticipantId extractorId,
        PlayerExpeditionLootSnapshot lootSnapshot,
        RaidInitialAffiliationSnapshot affiliations,
        out RaidLootEligibilitySnapshot eligibility,
        out string error)
    {
        if (lootSnapshot == null)
        {
            eligibility = null;
            error = "Extracted loot snapshot is required.";
            return false;
        }

        return TryResolve(
            extractorId,
            lootSnapshot.Combined,
            lootSnapshot.CombinedOrigins,
            affiliations,
            out eligibility,
            out error);
    }

    public static bool TryResolve(
        RaidParticipantId extractorId,
        IReadOnlyList<LootEntry> totalLoot,
        IReadOnlyList<RaidLootOriginEntry> originBuckets,
        RaidInitialAffiliationSnapshot affiliations,
        out RaidLootEligibilitySnapshot eligibility,
        out string error)
    {
        eligibility = null;
        error = null;
        if (!extractorId.IsValid || affiliations == null ||
            !affiliations.TryGetTeam(extractorId, out RaidTeamId extractorTeam))
        {
            error = "Extractor identity or initial affiliation is invalid.";
            return false;
        }

        if (!PlayerExpeditionLootSnapshot.TryValidateOriginTotals(
                totalLoot,
                originBuckets,
                out error))
        {
            return false;
        }

        var eligibleByLoot = new Dictionary<LootId, int>(totalLoot.Count);
        try
        {
            for (int index = 0; index < originBuckets.Count; index++)
            {
                RaidLootOriginEntry bucket = originBuckets[index];
                bool isEligible = bucket.Origin.IsDungeon;
                if (bucket.Origin.IsPlayer)
                {
                    RaidParticipantId originParticipantId = bucket.Origin.PlayerParticipantId;
                    if (!affiliations.TryGetTeam(originParticipantId, out RaidTeamId originTeam))
                    {
                        error = "Player loot origin has no initial affiliation.";
                        return false;
                    }

                    isEligible = originParticipantId != extractorId && originTeam != extractorTeam;
                }

                if (!isEligible)
                {
                    continue;
                }

                eligibleByLoot.TryGetValue(bucket.LootId, out int currentEligible);
                eligibleByLoot[bucket.LootId] = checked(currentEligible + bucket.Amount);
            }

            var orderedLoot = new List<LootEntry>(totalLoot.Count);
            for (int index = 0; index < totalLoot.Count; index++)
            {
                orderedLoot.Add(totalLoot[index]);
            }
            orderedLoot.Sort((left, right) =>
                string.CompareOrdinal(left.LootId.Value, right.LootId.Value));

            var entries = new RaidLootEligibilityEntry[orderedLoot.Count];
            long totalAmount = 0;
            long eligibleAmount = 0;
            for (int index = 0; index < orderedLoot.Count; index++)
            {
                LootEntry loot = orderedLoot[index];
                eligibleByLoot.TryGetValue(loot.LootId, out int lootEligible);
                var entry = new RaidLootEligibilityEntry(loot.LootId, loot.Amount, lootEligible);
                if (!entry.IsValid)
                {
                    error = "Resolved eligibility is outside the extracted quantity.";
                    return false;
                }

                entries[index] = entry;
                totalAmount = checked(totalAmount + entry.TotalAmount);
                eligibleAmount = checked(eligibleAmount + entry.EligibleAmount);
            }

            eligibility = new RaidLootEligibilitySnapshot(entries, totalAmount, eligibleAmount);
            if (eligibility.EligibleAmount > eligibility.TotalAmount ||
                eligibility.IneligibleAmount < 0)
            {
                eligibility = null;
                error = "Resolved eligibility totals are inconsistent.";
                return false;
            }

            return true;
        }
        catch (OverflowException)
        {
            eligibility = null;
            error = "Raid loot eligibility quantity overflowed.";
            return false;
        }
    }
}
