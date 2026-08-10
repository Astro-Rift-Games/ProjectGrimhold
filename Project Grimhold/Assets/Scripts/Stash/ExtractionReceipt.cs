using System;

/// <summary>
/// Idempotency key for a locally committed extraction result.
/// </summary>
public readonly struct ExtractionReceipt : IEquatable<ExtractionReceipt>
{
    public string RaidId { get; }
    public ProfileId ProfileId { get; }
    public int ResultSequence { get; }

    public ExtractionReceipt(string raidId, ProfileId profileId, int resultSequence)
    {
        RaidId = raidId;
        ProfileId = profileId;
        ResultSequence = resultSequence;
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(RaidId) && ProfileId.IsValid && ResultSequence > 0;

    public bool Equals(ExtractionReceipt other) =>
        string.Equals(RaidId, other.RaidId, StringComparison.Ordinal) &&
        ProfileId == other.ProfileId && ResultSequence == other.ResultSequence;

    public override bool Equals(object obj) => obj is ExtractionReceipt other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(RaidId, ProfileId, ResultSequence);
}
