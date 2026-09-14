using System;
using System.Collections.Generic;

/// <summary>
/// Runner-independent claim that one Solo or Duo preparation should continue after returning to Town.
/// It contains stable identities only and never owns the authoritative Town roster.
/// </summary>
public sealed class TownPartyContinuationContext : IEquatable<TownPartyContinuationContext>
{
    private readonly ProfileId[] _members;
    private readonly IReadOnlyList<ProfileId> _readOnlyMembers;

    public RaidCode OriginRaidCode { get; }
    public int OriginLaunchRevision { get; }
    public ProfileId HostProfileId { get; }
    public IReadOnlyList<ProfileId> Members => _readOnlyMembers;

    private TownPartyContinuationContext(
        RaidCode originRaidCode,
        int originLaunchRevision,
        ProfileId hostProfileId,
        IReadOnlyList<ProfileId> members)
    {
        OriginRaidCode = originRaidCode;
        OriginLaunchRevision = originLaunchRevision;
        HostProfileId = hostProfileId;
        _members = Copy(members);
        _readOnlyMembers = Array.AsReadOnly(_members);
    }

    public static bool TryCreate(
        RaidCode originRaidCode,
        int originLaunchRevision,
        ProfileId hostProfileId,
        IReadOnlyList<ProfileId> members,
        out TownPartyContinuationContext context)
    {
        if (!originRaidCode.IsValid || !RaidSessionRules.IsValidLaunchRevision(originLaunchRevision) ||
            !TownRaidPreparationRules.IsValidProfileRoster(hostProfileId, members))
        {
            context = null;
            return false;
        }

        context = new TownPartyContinuationContext(
            originRaidCode,
            originLaunchRevision,
            hostProfileId,
            members);
        return true;
    }

    public static bool TryCreate(
        RaidLaunchContext launchContext,
        out TownPartyContinuationContext context)
    {
        if (launchContext == null)
        {
            context = null;
            return false;
        }

        return TryCreate(
            launchContext.RaidCode,
            launchContext.LaunchRevision,
            launchContext.HostProfileId,
            launchContext.ParticipantProfileIds,
            out context);
    }

    public bool Contains(ProfileId profileId)
    {
        if (!profileId.IsValid)
        {
            return false;
        }

        for (int index = 0; index < _members.Length; index++)
        {
            if (_members[index] == profileId)
            {
                return true;
            }
        }

        return false;
    }

    public bool MatchesRoster(ProfileId hostProfileId, IReadOnlyList<TownRaidPreparationMember> members)
    {
        if (HostProfileId != hostProfileId || members == null || members.Count != _members.Length)
        {
            return false;
        }

        for (int index = 0; index < _members.Length; index++)
        {
            if (_members[index] != members[index].ProfileId)
            {
                return false;
            }
        }

        return true;
    }

    public bool Equals(TownPartyContinuationContext other)
    {
        if (ReferenceEquals(other, null) || OriginRaidCode != other.OriginRaidCode ||
            OriginLaunchRevision != other.OriginLaunchRevision || HostProfileId != other.HostProfileId ||
            _members.Length != other._members.Length)
        {
            return false;
        }

        for (int index = 0; index < _members.Length; index++)
        {
            if (_members[index] != other._members[index])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object obj) => Equals(obj as TownPartyContinuationContext);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(OriginRaidCode);
        hash.Add(OriginLaunchRevision);
        hash.Add(HostProfileId);
        for (int index = 0; index < _members.Length; index++)
        {
            hash.Add(_members[index]);
        }

        return hash.ToHashCode();
    }

    private static ProfileId[] Copy(IReadOnlyList<ProfileId> members)
    {
        var copy = new ProfileId[members.Count];
        for (int index = 0; index < members.Count; index++)
        {
            copy[index] = members[index];
        }

        return copy;
    }
}
