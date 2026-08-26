using System;

/// <summary>
/// Durable identity and declared outcome of one locally confirmed raid progression result.
/// </summary>
public readonly struct ProgressionReceipt : IEquatable<ProgressionReceipt>
{
    public string RaidId { get; }
    public ProfileId ProfileId { get; }
    public int ResultSequence { get; }
    public long ConsolidatedExperience { get; }
    public int ResultingLevel { get; }

    public ProgressionReceipt(
        string raidId,
        ProfileId profileId,
        int resultSequence,
        long consolidatedExperience,
        int resultingLevel)
    {
        RaidId = raidId;
        ProfileId = profileId;
        ResultSequence = resultSequence;
        ConsolidatedExperience = consolidatedExperience;
        ResultingLevel = resultingLevel;
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RaidId) &&
        ProfileId.IsValid &&
        ResultSequence > 0 &&
        ConsolidatedExperience >= 0 &&
        ResultingLevel >= ExperienceCurve.InitialLevel &&
        ResultingLevel <= ProgressionBalanceDefaults.InitialExperienceCurve.MaximumLevel;

    public bool Equals(ProgressionReceipt other) =>
        string.Equals(RaidId, other.RaidId, StringComparison.Ordinal) &&
        ProfileId == other.ProfileId &&
        ResultSequence == other.ResultSequence &&
        ConsolidatedExperience == other.ConsolidatedExperience &&
        ResultingLevel == other.ResultingLevel;

    public override bool Equals(object obj) => obj is ProgressionReceipt other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(
        RaidId,
        ProfileId,
        ResultSequence,
        ConsolidatedExperience,
        ResultingLevel);

    public static bool operator ==(ProgressionReceipt left, ProgressionReceipt right) => left.Equals(right);
    public static bool operator !=(ProgressionReceipt left, ProgressionReceipt right) => !left.Equals(right);
}
