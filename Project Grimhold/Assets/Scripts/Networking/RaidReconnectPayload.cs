using System;

/// <summary>
/// Payload with pure identity, used for recovering a disconnected session.
/// Must never contain loadout or progression baseline to prevent state overwrites.
/// </summary>
public readonly struct RaidReconnectPayload : IEquatable<RaidReconnectPayload>
{
    public readonly RaidCode RaidCode;
    public readonly ProfileId ProfileId;
    public readonly string RaidGenerationId;

    public bool IsValid => RaidCode.IsValid && ProfileId.IsValid && !string.IsNullOrEmpty(RaidGenerationId);

    public RaidReconnectPayload(RaidCode raidCode, ProfileId profileId, string raidGenerationId)
    {
        RaidCode = raidCode;
        ProfileId = profileId;
        RaidGenerationId = raidGenerationId ?? string.Empty;
    }

    public bool Equals(RaidReconnectPayload other)
    {
        return RaidCode == other.RaidCode &&
               ProfileId == other.ProfileId &&
               RaidGenerationId == other.RaidGenerationId;
    }

    public override bool Equals(object obj)
    {
        return obj is RaidReconnectPayload other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RaidCode, ProfileId, RaidGenerationId);
    }
}
