using System;
using System.Collections.Generic;

/// <summary>Runner-independent Party roster preserved across Town/Raid runner replacement.</summary>
public sealed class TownPartyContinuationContext : IEquatable<TownPartyContinuationContext>
{
    private readonly ProfileId[] _members;
    private readonly IReadOnlyList<ProfileId> _readOnlyMembers;

    public ProfileId HostProfileId { get; }
    public IReadOnlyList<ProfileId> Members => _readOnlyMembers;

    private TownPartyContinuationContext(ProfileId hostProfileId, IReadOnlyList<ProfileId> members)
    {
        HostProfileId = hostProfileId;
        _members = new ProfileId[members.Count];
        for (int index = 0; index < members.Count; index++) _members[index] = members[index];
        _readOnlyMembers = Array.AsReadOnly(_members);
    }

    public static bool TryCreate(ProfileId hostProfileId, IReadOnlyList<ProfileId> members, out TownPartyContinuationContext context)
    {
        var snapshot = new TownPartySnapshot(1, hostProfileId, members, 1);
        if (!TownPartyRules.IsValid(snapshot))
        {
            context = null;
            return false;
        }
        context = new TownPartyContinuationContext(hostProfileId, members);
        return true;
    }

    public static bool TryCreate(in TownPartySnapshot snapshot, out TownPartyContinuationContext context)
    {
        if (!TownPartyRules.IsValid(snapshot))
        {
            context = null;
            return false;
        }
        return TryCreate(snapshot.HostProfileId, snapshot.Members, out context);
    }

    public bool Contains(ProfileId profileId)
    {
        for (int index = 0; index < _members.Length; index++) if (_members[index] == profileId) return true;
        return false;
    }

    public bool Matches(in TownPartySnapshot snapshot)
    {
        if (!TownPartyRules.IsValid(snapshot) || snapshot.HostProfileId != HostProfileId || snapshot.Members.Count != _members.Length)
            return false;
        for (int index = 0; index < _members.Length; index++) if (snapshot.Members[index] != _members[index]) return false;
        return true;
    }

    public bool Equals(TownPartyContinuationContext other)
    {
        if (ReferenceEquals(other, null) || other.HostProfileId != HostProfileId || other._members.Length != _members.Length)
            return false;
        for (int index = 0; index < _members.Length; index++) if (other._members[index] != _members[index]) return false;
        return true;
    }

    public override bool Equals(object obj) => Equals(obj as TownPartyContinuationContext);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(HostProfileId);
        for (int index = 0; index < _members.Length; index++) hash.Add(_members[index]);
        return hash.ToHashCode();
    }
}
