using System;
using System.Collections.Generic;

/// <summary>
/// Runner-independent snapshot copied from the Town preparation before its
/// NetworkRunner is shut down. It contains only stable profile identities and
/// deterministic Raid identity data required by the next runner.
/// </summary>
public sealed class RaidLaunchContext
{
    private readonly RaidLaunchParticipant[] _participants;
    private readonly IReadOnlyList<RaidLaunchParticipant> _readOnlyParticipants;
    private readonly ProfileId[] _participantProfileIds;
    private readonly IReadOnlyList<ProfileId> _readOnlyParticipantProfileIds;

    public RaidCode RaidCode { get; }
    public ProfileId HostProfileId { get; }
    public ProfileId LocalProfileId { get; }
    public int LaunchRevision { get; }
    public IReadOnlyList<RaidLaunchParticipant> Participants => _readOnlyParticipants;
    public IReadOnlyList<ProfileId> ParticipantProfileIds => _readOnlyParticipantProfileIds;

    private RaidLaunchContext(
        RaidCode raidCode,
        ProfileId hostProfileId,
        IReadOnlyList<RaidLaunchParticipant> participants,
        ProfileId localProfileId,
        int launchRevision)
    {
        RaidCode = raidCode;
        HostProfileId = hostProfileId;
        LocalProfileId = localProfileId;
        LaunchRevision = launchRevision;
        _participants = Copy(participants);
        _readOnlyParticipants = Array.AsReadOnly(_participants);
        _participantProfileIds = CopyProfiles(_participants);
        _readOnlyParticipantProfileIds = Array.AsReadOnly(_participantProfileIds);
    }

    /// <summary>
    /// Creates a runner-independent context only when every stable Raid identity belongs to
    /// the same valid frozen cohort and launch revision.
    /// </summary>
    public static bool TryCreate(
        RaidCode raidCode,
        ProfileId hostProfileId,
        IReadOnlyList<RaidLaunchParticipant> participants,
        ProfileId localProfileId,
        int launchRevision,
        out RaidLaunchContext context)
    {
        ProfileId[] participantProfileIds = CopyProfiles(participants);
        if (!raidCode.IsValid || !localProfileId.IsValid ||
            !RaidSessionRules.IsValidLaunchRevision(launchRevision) ||
            !AreValidParticipants(participants) ||
            !RaidSessionRules.IsValidParticipantCohort(hostProfileId, participantProfileIds) ||
            !RaidSessionRules.ContainsProfile(participantProfileIds, localProfileId))
        {
            context = null;
            return false;
        }

        context = new RaidLaunchContext(
            raidCode,
            hostProfileId,
            participants,
            localProfileId,
            launchRevision);
        return true;
    }

    private static bool AreValidParticipants(IReadOnlyList<RaidLaunchParticipant> participants)
    {
        if (participants == null || participants.Count < 1 ||
            participants.Count > RaidSessionRules.MaxParticipants)
        {
            return false;
        }

        for (int index = 0; index < participants.Count; index++)
        {
            if (!participants[index].IsValid)
            {
                return false;
            }
        }

        return true;
    }

    private static RaidLaunchParticipant[] Copy(IReadOnlyList<RaidLaunchParticipant> participants)
    {
        if (participants == null)
        {
            return Array.Empty<RaidLaunchParticipant>();
        }

        var copy = new RaidLaunchParticipant[participants.Count];
        for (int index = 0; index < participants.Count; index++)
        {
            copy[index] = participants[index];
        }

        return copy;
    }

    private static ProfileId[] CopyProfiles(IReadOnlyList<RaidLaunchParticipant> participants)
    {
        if (participants == null)
        {
            return Array.Empty<ProfileId>();
        }

        var profiles = new ProfileId[participants.Count];
        for (int index = 0; index < participants.Count; index++)
        {
            profiles[index] = participants[index].ProfileId;
        }

        return profiles;
    }
}
