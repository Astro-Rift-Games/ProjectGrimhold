using System;

public readonly struct RaidConnectionRequest : IEquatable<RaidConnectionRequest>
{
    public string RaidId { get; }
    public RaidConnectionRole Role { get; }
    public string SessionName { get; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RaidId) &&
        !string.IsNullOrWhiteSpace(SessionName) &&
        (Role == RaidConnectionRole.Host || Role == RaidConnectionRole.Client);

    public RaidConnectionRequest(string raidId, RaidConnectionRole role, string sessionName)
    {
        RaidId = raidId;
        Role = role;
        SessionName = sessionName;
    }

    public bool Equals(RaidConnectionRequest other)
    {
        return string.Equals(RaidId, other.RaidId, StringComparison.Ordinal) &&
               Role == other.Role &&
               string.Equals(SessionName, other.SessionName, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is RaidConnectionRequest other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RaidId, Role, SessionName);
    }
}
