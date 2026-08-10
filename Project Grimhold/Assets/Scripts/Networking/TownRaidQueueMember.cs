using Fusion;

/// <summary>
/// Read-only presentation representation of one Town raid queue member.
/// </summary>
public readonly struct TownRaidQueueMember
{
    public ProfileId ProfileId { get; }
    public PlayerRef Player { get; }
    public bool IsReady { get; }

    public TownRaidQueueMember(ProfileId profileId, PlayerRef player, bool isReady)
    {
        ProfileId = profileId;
        Player = player;
        IsReady = isReady;
    }
}
