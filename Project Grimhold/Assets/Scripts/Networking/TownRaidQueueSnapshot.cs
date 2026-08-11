using System;
using System.Collections.Generic;

/// <summary>
/// Immutable queue state consumed by local presentation.
/// </summary>
public readonly struct TownRaidQueueSnapshot
{
    private readonly TownRaidQueueMember[] _members;

    public TownRaidQueueState State { get; }
    public ProfileId HostProfileId { get; }
    public RaidCode RaidCode { get; }
    public int LaunchSequence { get; }
    public IReadOnlyList<TownRaidQueueMember> Members => _members ?? Array.Empty<TownRaidQueueMember>();

    public TownRaidQueueSnapshot(
        TownRaidQueueState state,
        ProfileId hostProfileId,
        RaidCode raidCode,
        int launchSequence,
        IReadOnlyList<TownRaidQueueMember> members)
    {
        State = state;
        HostProfileId = hostProfileId;
        RaidCode = raidCode;
        LaunchSequence = launchSequence;
        _members = members == null ? Array.Empty<TownRaidQueueMember>() : Copy(members);
    }

    private static TownRaidQueueMember[] Copy(IReadOnlyList<TownRaidQueueMember> members)
    {
        var copy = new TownRaidQueueMember[members.Count];
        for (int i = 0; i < members.Count; i++)
        {
            copy[i] = members[i];
        }

        return copy;
    }
}
