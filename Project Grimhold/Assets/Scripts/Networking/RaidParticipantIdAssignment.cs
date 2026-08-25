using System.Collections.Generic;

/// <summary>Deterministic authority-side assignment from the immutable frozen Raid cohort.</summary>
public static class RaidParticipantIdAssignment
{
    public static bool TryResolve(
        IReadOnlyList<ProfileId> frozenProfiles,
        ProfileId profileId,
        out RaidParticipantId participantId)
    {
        participantId = default;
        if (!profileId.IsValid || frozenProfiles == null ||
            frozenProfiles.Count < 1 || frozenProfiles.Count > RaidSessionRules.MaxParticipants)
        {
            return false;
        }

        int matchCount = 0;
        int ordinal = 1;
        var seenProfiles = new HashSet<ProfileId>();
        for (int index = 0; index < frozenProfiles.Count; index++)
        {
            ProfileId candidate = frozenProfiles[index];
            if (!candidate.IsValid || !seenProfiles.Add(candidate))
            {
                return false;
            }

            if (candidate == profileId)
            {
                matchCount++;
            }
            else if (string.CompareOrdinal(candidate.Value, profileId.Value) < 0)
            {
                ordinal++;
            }
        }

        return matchCount == 1 && RaidParticipantId.TryCreate(ordinal, out participantId);
    }
}
