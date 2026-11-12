/// <summary>
/// Pure preparation state for one stable player profile.
/// </summary>
public readonly struct TownRaidPreparationMember
{
    public ProfileId ProfileId { get; }
    public bool IsReady { get; }

    /// <summary>
    /// Zero while membership is mutable; positive only when this member belongs to a frozen cohort.
    /// </summary>
    public int LaunchRevision { get; }
    public bool LaunchAcknowledged { get; }

    /// <summary>
    /// Initial Raid team ordinal captured by the State Authority at freeze time: 1..N by first
    /// appearance in the frozen cohort order, shared by the members of one Town party. Zero while
    /// membership is mutable or when no valid team was captured.
    /// </summary>
    public int TeamOrdinal { get; }

    public bool IsValid => ProfileId.IsValid && LaunchRevision >= 0 && TeamOrdinal >= 0;

    /// <summary>Creates a new preparation member in the required NotReady state.</summary>
    public TownRaidPreparationMember(ProfileId profileId)
        : this(profileId, false, 0, false)
    {
    }

    public TownRaidPreparationMember(
        ProfileId profileId,
        bool isReady,
        int launchRevision = 0,
        bool launchAcknowledged = false,
        int teamOrdinal = 0)
    {
        ProfileId = profileId;
        IsReady = isReady;
        LaunchRevision = launchRevision;
        LaunchAcknowledged = launchAcknowledged;
        TeamOrdinal = teamOrdinal;
    }

    /// <summary>Copies this member with a different Ready value.</summary>
    public TownRaidPreparationMember WithReady(bool isReady)
    {
        return new TownRaidPreparationMember(ProfileId, isReady, LaunchRevision, LaunchAcknowledged, TeamOrdinal);
    }

    /// <summary>Copies this member into one frozen launch revision.</summary>
    public TownRaidPreparationMember WithLaunchRevision(int launchRevision)
    {
        return new TownRaidPreparationMember(ProfileId, IsReady, launchRevision, false, TeamOrdinal);
    }

    /// <summary>Copies this member with the initial Raid team ordinal captured at freeze.</summary>
    public TownRaidPreparationMember WithTeamOrdinal(int teamOrdinal)
    {
        return new TownRaidPreparationMember(ProfileId, IsReady, LaunchRevision, LaunchAcknowledged, teamOrdinal);
    }

    /// <summary>Copies this frozen member with its replicated launch acknowledgement.</summary>
    public TownRaidPreparationMember WithLaunchAcknowledged(bool acknowledged)
    {
        return new TownRaidPreparationMember(ProfileId, IsReady, LaunchRevision, acknowledged, TeamOrdinal);
    }
}
