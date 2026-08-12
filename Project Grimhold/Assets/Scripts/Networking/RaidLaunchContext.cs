using System;
using System.Collections.Generic;

/// <summary>
/// Runner-independent snapshot copied from the Town preparation before its
/// NetworkRunner is shut down. It contains only stable profile identities and
/// deterministic Raid identity data required by the next runner.
/// </summary>
public sealed class RaidLaunchContext
{
    private readonly ProfileId[] _participantProfileIds;
    private readonly IReadOnlyList<ProfileId> _readOnlyParticipantProfileIds;

    public RaidCode RaidCode { get; }
    public ProfileId HostProfileId { get; }
    public ProfileId LocalProfileId { get; }
    public int LaunchRevision { get; }
    public IReadOnlyList<ProfileId> ParticipantProfileIds => _readOnlyParticipantProfileIds;

    private RaidLaunchContext(
        RaidCode raidCode,
        ProfileId hostProfileId,
        IReadOnlyList<ProfileId> participantProfileIds,
        ProfileId localProfileId,
        int launchRevision)
    {
        RaidCode = raidCode;
        HostProfileId = hostProfileId;
        LocalProfileId = localProfileId;
        LaunchRevision = launchRevision;
        _participantProfileIds = Copy(participantProfileIds);
        _readOnlyParticipantProfileIds = Array.AsReadOnly(_participantProfileIds);
    }

    /// <summary>
    /// Creates a runner-independent context only when every stable Raid identity belongs to
    /// the same valid frozen cohort and launch revision.
    /// </summary>
    public static bool TryCreate(
        RaidCode raidCode,
        ProfileId hostProfileId,
        IReadOnlyList<ProfileId> participantProfileIds,
        ProfileId localProfileId,
        int launchRevision,
        out RaidLaunchContext context)
    {
        if (!raidCode.IsValid || !localProfileId.IsValid ||
            !RaidSessionRules.IsValidLaunchRevision(launchRevision) ||
            !RaidSessionRules.IsValidParticipantCohort(hostProfileId, participantProfileIds) ||
            !RaidSessionRules.ContainsProfile(participantProfileIds, localProfileId))
        {
            context = null;
            return false;
        }

        context = new RaidLaunchContext(
            raidCode,
            hostProfileId,
            participantProfileIds,
            localProfileId,
            launchRevision);
        return true;
    }

    private static ProfileId[] Copy(IReadOnlyList<ProfileId> profiles)
    {
        if (profiles == null)
        {
            return Array.Empty<ProfileId>();
        }

        var copy = new ProfileId[profiles.Count];
        for (int index = 0; index < profiles.Count; index++)
        {
            copy[index] = profiles[index];
        }

        return copy;
    }
}
