using System;

/// <summary>
/// Process-local, in-memory ticket representing an eligible raid reconnection opportunity.
/// Discarded on successful reconnection, explicit refusal, or application quit.
/// Contains strictly identity values without loadout or progression baselines.
/// </summary>
public readonly struct RaidReconnectTicket : IEquatable<RaidReconnectTicket>
{
    public RaidCode RaidCode { get; }
    public ProfileId ProfileId { get; }
    public string RaidGenerationId { get; }
    public string SessionName { get; }
    public DateTime CreatedAtUtc { get; }

    public bool IsValid =>
        RaidCode.IsValid &&
        ProfileId.IsValid &&
        !string.IsNullOrEmpty(RaidGenerationId) &&
        !string.IsNullOrEmpty(SessionName);

    public RaidReconnectTicket(
        RaidCode raidCode,
        ProfileId profileId,
        string raidGenerationId,
        string sessionName)
        : this(raidCode, profileId, raidGenerationId, sessionName, DateTime.UtcNow)
    {
    }

    public RaidReconnectTicket(
        RaidCode raidCode,
        ProfileId profileId,
        string raidGenerationId,
        string sessionName,
        DateTime createdAtUtc)
    {
        RaidCode = raidCode;
        ProfileId = profileId;
        RaidGenerationId = raidGenerationId ?? string.Empty;
        SessionName = sessionName ?? string.Empty;
        CreatedAtUtc = createdAtUtc;
    }

    public RaidReconnectPayload ToPayload()
    {
        return new RaidReconnectPayload(RaidCode, ProfileId, RaidGenerationId);
    }

    public bool Equals(RaidReconnectTicket other)
    {
        return RaidCode == other.RaidCode &&
               ProfileId == other.ProfileId &&
               string.Equals(RaidGenerationId, other.RaidGenerationId, StringComparison.Ordinal) &&
               string.Equals(SessionName, other.SessionName, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is RaidReconnectTicket other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RaidCode, ProfileId, RaidGenerationId, SessionName);
    }

    public static bool operator ==(RaidReconnectTicket left, RaidReconnectTicket right) => left.Equals(right);
    public static bool operator !=(RaidReconnectTicket left, RaidReconnectTicket right) => !left.Equals(right);
}
