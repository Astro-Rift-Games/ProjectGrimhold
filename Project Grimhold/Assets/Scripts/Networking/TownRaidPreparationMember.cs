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

    public bool IsValid => ProfileId.IsValid && LaunchRevision >= 0;

    /// <summary>Creates a new preparation member in the required NotReady state.</summary>
    public TownRaidPreparationMember(ProfileId profileId)
        : this(profileId, false, 0, false)
    {
    }

    public TownRaidPreparationMember(
        ProfileId profileId,
        bool isReady,
        int launchRevision = 0,
        bool launchAcknowledged = false)
    {
        ProfileId = profileId;
        IsReady = isReady;
        LaunchRevision = launchRevision;
        LaunchAcknowledged = launchAcknowledged;
    }

    /// <summary>Copies this member with a different Ready value.</summary>
    public TownRaidPreparationMember WithReady(bool isReady)
    {
        return new TownRaidPreparationMember(ProfileId, isReady, LaunchRevision, LaunchAcknowledged);
    }

    /// <summary>Copies this member into one frozen launch revision.</summary>
    public TownRaidPreparationMember WithLaunchRevision(int launchRevision)
    {
        return new TownRaidPreparationMember(ProfileId, IsReady, launchRevision, false);
    }

    /// <summary>Copies this frozen member with its replicated launch acknowledgement.</summary>
    public TownRaidPreparationMember WithLaunchAcknowledged(bool acknowledged)
    {
        return new TownRaidPreparationMember(ProfileId, IsReady, LaunchRevision, acknowledged);
    }
}
