using System;

/// <summary>
/// Identifies one explicitly authorized participant return within one raid generation.
/// Profile identity is durable across runner-local routing changes; the generation prevents
/// an authorization from leaking into a later raid.
/// </summary>
public readonly struct ControlledReturnKey : IEquatable<ControlledReturnKey>
{
    public string ProfileId { get; }
    public string RaidGenerationId { get; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ProfileId) &&
        !string.IsNullOrWhiteSpace(RaidGenerationId);

    public ControlledReturnKey(string profileId, string raidGenerationId)
    {
        ProfileId = profileId;
        RaidGenerationId = raidGenerationId;
    }

    public bool Equals(ControlledReturnKey other) =>
        string.Equals(ProfileId, other.ProfileId, StringComparison.Ordinal) &&
        string.Equals(RaidGenerationId, other.RaidGenerationId, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is ControlledReturnKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int profileHash = ProfileId != null ? StringComparer.Ordinal.GetHashCode(ProfileId) : 0;
            int generationHash = RaidGenerationId != null
                ? StringComparer.Ordinal.GetHashCode(RaidGenerationId)
                : 0;
            return (profileHash * 397) ^ generationHash;
        }
    }
}

