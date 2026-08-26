using System;

/// <summary>Prepared reward bound to the exact pending extraction result that produced it.</summary>
public readonly struct ExtractedLootExperienceCandidate : IEquatable<ExtractedLootExperienceCandidate>
{
    public ExtractedLootExperienceCandidate(
        int resultSequence,
        in ExtractedLootExperienceCalculation calculation)
    {
        ResultSequence = resultSequence;
        EligibleValue = calculation.EligibleValue;
        AwardedExperience = calculation.AwardedExperience;
    }

    public int ResultSequence { get; }
    public long EligibleValue { get; }
    public long AwardedExperience { get; }
    public bool IsValid => ResultSequence > 0 && EligibleValue >= 0 && AwardedExperience >= 0 &&
                           AwardedExperience <= EligibleValue;

    public bool Matches(int resultSequence) => IsValid && ResultSequence == resultSequence;

    public bool Equals(ExtractedLootExperienceCandidate other) =>
        ResultSequence == other.ResultSequence && EligibleValue == other.EligibleValue &&
        AwardedExperience == other.AwardedExperience;

    public override bool Equals(object obj) =>
        obj is ExtractedLootExperienceCandidate other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ResultSequence, EligibleValue, AwardedExperience);
}
