using System;
using System.Collections.Generic;

/// <summary>Immutable observation of one authoritative Solo or Duo Party in Town.</summary>
public readonly struct TownPartySnapshot
{
    private readonly ProfileId[] _members;
    private readonly IReadOnlyList<ProfileId> _readOnlyMembers;

    public int PartyId { get; }
    public ProfileId HostProfileId { get; }
    public int Revision { get; }
    public IReadOnlyList<ProfileId> Members => _readOnlyMembers ?? Array.Empty<ProfileId>();

    public TownPartySnapshot(int partyId, ProfileId hostProfileId, IReadOnlyList<ProfileId> members, int revision)
    {
        PartyId = partyId;
        HostProfileId = hostProfileId;
        Revision = revision;
        _members = members == null ? Array.Empty<ProfileId>() : Copy(members);
        _readOnlyMembers = Array.AsReadOnly(_members);
    }

    public bool Contains(ProfileId profileId)
    {
        for (int index = 0; index < Members.Count; index++)
        {
            if (Members[index] == profileId) return true;
        }
        return false;
    }

    private static ProfileId[] Copy(IReadOnlyList<ProfileId> source)
    {
        var copy = new ProfileId[source.Count];
        for (int index = 0; index < source.Count; index++) copy[index] = source[index];
        return copy;
    }
}
