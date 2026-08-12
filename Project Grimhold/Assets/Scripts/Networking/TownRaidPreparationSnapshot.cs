using System;
using System.Collections.Generic;

/// <summary>
/// Immutable observation of one Town Raid preparation.
/// The member order is the explicit authoritative roster order and is preserved when frozen.
/// </summary>
public readonly struct TownRaidPreparationSnapshot
{
    private readonly TownRaidPreparationMember[] _members;
    private readonly IReadOnlyList<TownRaidPreparationMember> _readOnlyMembers;

    public RaidCode RaidCode { get; }
    public ProfileId HostProfileId { get; }
    public TownRaidPreparationState State { get; }

    /// <summary>Changes whenever any observable preparation field changes.</summary>
    public int SnapshotRevision { get; }

    /// <summary>Identifies one frozen cohort and remains zero before the first Start.</summary>
    public int LaunchRevision { get; }

    /// <summary>Expected number of member entries tagged with <see cref="LaunchRevision"/>.</summary>
    public int FrozenMemberCount { get; }
    public IReadOnlyList<TownRaidPreparationMember> Members =>
        _readOnlyMembers ?? Array.Empty<TownRaidPreparationMember>();

    public TownRaidPreparationSnapshot(
        RaidCode raidCode,
        ProfileId hostProfileId,
        TownRaidPreparationState state,
        IReadOnlyList<TownRaidPreparationMember> members,
        int snapshotRevision,
        int launchRevision = 0,
        int frozenMemberCount = 0)
    {
        RaidCode = raidCode;
        HostProfileId = hostProfileId;
        State = state;
        SnapshotRevision = snapshotRevision;
        LaunchRevision = launchRevision;
        FrozenMemberCount = frozenMemberCount;
        _members = Copy(members);
        _readOnlyMembers = Array.AsReadOnly(_members);
    }

    private static TownRaidPreparationMember[] Copy(IReadOnlyList<TownRaidPreparationMember> members)
    {
        if (members == null)
        {
            return Array.Empty<TownRaidPreparationMember>();
        }

        var copy = new TownRaidPreparationMember[members.Count];
        for (int index = 0; index < members.Count; index++)
        {
            copy[index] = members[index];
        }

        return copy;
    }
}
