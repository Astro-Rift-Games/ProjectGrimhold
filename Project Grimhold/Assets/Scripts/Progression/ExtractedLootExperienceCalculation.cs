using System;

/// <summary>Immutable result of converting eligible Raid loot into provisional experience.</summary>
public readonly struct ExtractedLootExperienceCalculation : IEquatable<ExtractedLootExperienceCalculation>
{
    public ExtractedLootExperienceCalculation(long eligibleValue, long awardedExperience)
    {
        EligibleValue = eligibleValue;
        AwardedExperience = awardedExperience;
    }

    public long EligibleValue { get; }
    public long AwardedExperience { get; }
    public bool IsValid => EligibleValue >= 0 && AwardedExperience >= 0 &&
                           AwardedExperience <= EligibleValue;

    public bool Equals(ExtractedLootExperienceCalculation other) =>
        EligibleValue == other.EligibleValue && AwardedExperience == other.AwardedExperience;

    public override bool Equals(object obj) =>
        obj is ExtractedLootExperienceCalculation other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(EligibleValue, AwardedExperience);
}
