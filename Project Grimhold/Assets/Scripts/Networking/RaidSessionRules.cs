using System.Collections.Generic;

/// <summary>
/// Defines immutable rules shared by every Raid session boundary.
/// </summary>
public static class RaidSessionRules
{
    public const int MaxParticipants = 16;

    public static bool IsValidParticipantCount(int participantCount) =>
        participantCount >= 1 && participantCount <= MaxParticipants;

    /// <summary>Launch revision zero is reserved for a cohort that has not been frozen.</summary>
    public static bool IsValidLaunchRevision(int launchRevision) => launchRevision > 0;

    /// <summary>Validates the stable identities and capacity of one Raid cohort.</summary>
    public static bool IsValidParticipantCohort(
        ProfileId hostProfileId,
        IReadOnlyList<ProfileId> participantProfileIds)
    {
        if (!hostProfileId.IsValid || participantProfileIds == null ||
            participantProfileIds.Count < 1 || participantProfileIds.Count > MaxParticipants)
        {
            return false;
        }

        bool containsHost = false;
        for (int index = 0; index < participantProfileIds.Count; index++)
        {
            ProfileId profileId = participantProfileIds[index];
            if (!profileId.IsValid)
            {
                return false;
            }

            if (profileId == hostProfileId)
            {
                containsHost = true;
            }

            for (int other = index + 1; other < participantProfileIds.Count; other++)
            {
                if (profileId == participantProfileIds[other])
                {
                    return false;
                }
            }
        }

        return containsHost;
    }

    /// <summary>Checks membership without relying on runner-specific identity.</summary>
    public static bool ContainsProfile(
        IReadOnlyList<ProfileId> participantProfileIds,
        ProfileId profileId)
    {
        if (!profileId.IsValid || participantProfileIds == null)
        {
            return false;
        }

        for (int index = 0; index < participantProfileIds.Count; index++)
        {
            if (participantProfileIds[index] == profileId)
            {
                return true;
            }
        }

        return false;
    }
}
