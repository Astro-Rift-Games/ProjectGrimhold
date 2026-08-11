using System;

public readonly struct RaidConnectionRequest : IEquatable<RaidConnectionRequest>
{
    private readonly string _legacyRaidId;
    private readonly string _legacySessionName;

    public RaidCode RaidCode { get; }
    public RaidConnectionRole Role { get; }
    public string RaidId => RaidCode.IsValid ? RaidCode.RaidId : _legacyRaidId;
    public string SessionName => RaidCode.IsValid ? RaidCode.SessionName : _legacySessionName;

    public bool IsValid =>
        (RaidCode.IsValid || (!string.IsNullOrWhiteSpace(_legacyRaidId) &&
                              !string.IsNullOrWhiteSpace(_legacySessionName))) &&
        (Role == RaidConnectionRole.Host || Role == RaidConnectionRole.Client);

    public RaidConnectionRequest(RaidCode raidCode, RaidConnectionRole role)
    {
        RaidCode = raidCode;
        Role = role;
        _legacyRaidId = null;
        _legacySessionName = null;
    }

    /// <summary>
    /// Compatibility path for the still-existing frozen-manifest workflow.
    /// Coded Raid access must use the RaidCode constructor.
    /// </summary>
    [Obsolete("Use the RaidCode constructor for coded Raid access.")]
    public RaidConnectionRequest(string raidId, RaidConnectionRole role, string sessionName)
    {
        RaidCode = default;
        Role = role;
        _legacyRaidId = raidId;
        _legacySessionName = sessionName;
    }

    public bool Equals(RaidConnectionRequest other)
    {
        return RaidCode == other.RaidCode &&
               string.Equals(_legacyRaidId, other._legacyRaidId, StringComparison.Ordinal) &&
               Role == other.Role &&
               string.Equals(_legacySessionName, other._legacySessionName, StringComparison.Ordinal);
    }

    public override bool Equals(object obj) => obj is RaidConnectionRequest other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(RaidCode, Role, _legacyRaidId, _legacySessionName);
}
