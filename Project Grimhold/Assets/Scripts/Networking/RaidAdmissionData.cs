/// <summary>
/// Immutable connection payload used only when joining a manifest-backed raid.
/// Town continues to use <see cref="PlayerJoinData"/> and its existing codec.
/// </summary>
public readonly struct RaidAdmissionData
{
    public string RaidId { get; }
    public string AccessSecret { get; }
    public ProfileId ProfileId { get; }
    public PlayerClassId SelectedBuild { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(RaidId) &&
                           !string.IsNullOrWhiteSpace(AccessSecret) &&
                           ProfileId.IsValid &&
                           PlayerJoinDataCodec.IsSupported(SelectedBuild);

    public RaidAdmissionData(string raidId, string accessSecret, ProfileId profileId, PlayerClassId selectedBuild)
    {
        RaidId = raidId;
        AccessSecret = accessSecret;
        ProfileId = profileId;
        SelectedBuild = selectedBuild;
    }

    public PlayerJoinData ToPlayerJoinData()
    {
        return new PlayerJoinData(SelectedBuild, ProfileId);
    }
}
