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
    public IReadOnlyList<ProfileId> ParticipantProfileIds => _readOnlyParticipantProfileIds;

    public RaidLaunchContext(
        RaidCode raidCode,
        ProfileId hostProfileId,
        IReadOnlyList<ProfileId> participantProfileIds,
        ProfileId localProfileId)
    {
        RaidCode = raidCode;
        HostProfileId = hostProfileId;
        LocalProfileId = localProfileId;
        _participantProfileIds = Copy(participantProfileIds);
        _readOnlyParticipantProfileIds = Array.AsReadOnly(_participantProfileIds);
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
