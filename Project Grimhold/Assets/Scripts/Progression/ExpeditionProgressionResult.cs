using System;

/// <summary>
/// Immutable presentation snapshot of one completely committed Expedition progression result.
/// </summary>
public readonly struct ExpeditionProgressionResult : IEquatable<ExpeditionProgressionResult>
{
    public ExpeditionExperienceResolutionOutcome Outcome { get; }
    public int PveKillCount { get; }
    public int PvpKillCount { get; }
    public int PveAssistCount { get; }
    public int PvpAssistCount { get; }
    public int FirstOpenChestCount { get; }
    public long CombatExperience { get; }
    public long ExplorationExperience { get; }
    public long LootExperience { get; }
    public long EligibleExtractedLootValue { get; }
    public long ProvisionalExperienceTotal { get; }
    public int RetentionBasisPoints { get; }
    public long ConsolidatedExperience { get; }
    public int PreviousLevel { get; }
    public long PreviousExperience { get; }
    public int ResultingLevel { get; }
    public long ResultingExperience { get; }
    public int LevelsGained { get; }
    public long NextLevelExperienceRequirement { get; }
    public bool IsMaxLevel { get; }

    internal ExpeditionProgressionResult(
        in ExpeditionExperienceResolution resolution,
        in ExperienceApplicationResult application,
        int pveKillCount,
        int pvpKillCount,
        int pveAssistCount,
        int pvpAssistCount,
        int firstOpenChestCount,
        long eligibleExtractedLootValue,
        long nextLevelExperienceRequirement,
        bool isMaxLevel)
    {
        Outcome = resolution.Outcome;
        PveKillCount = pveKillCount;
        PvpKillCount = pvpKillCount;
        PveAssistCount = pveAssistCount;
        PvpAssistCount = pvpAssistCount;
        FirstOpenChestCount = firstOpenChestCount;
        CombatExperience = resolution.ProvisionalExperience.KillExperience +
            resolution.ProvisionalExperience.AssistExperience;
        ExplorationExperience = resolution.ProvisionalExperience.ExplorationExperience;
        LootExperience = resolution.ProvisionalExperience.ExtractedLootExperience;
        EligibleExtractedLootValue = eligibleExtractedLootValue;
        ProvisionalExperienceTotal = resolution.ProvisionalExperienceTotal;
        RetentionBasisPoints = resolution.RetentionBasisPoints;
        ConsolidatedExperience = resolution.ConsolidatedExperience;
        PreviousLevel = application.PreviousLevel;
        PreviousExperience = application.PreviousExperience;
        ResultingLevel = application.ResultingLevel;
        ResultingExperience = application.ResultingExperience;
        LevelsGained = application.LevelsGained;
        NextLevelExperienceRequirement = nextLevelExperienceRequirement;
        IsMaxLevel = isMaxLevel;
    }

    public bool Equals(ExpeditionProgressionResult other) =>
        Outcome == other.Outcome &&
        PveKillCount == other.PveKillCount &&
        PvpKillCount == other.PvpKillCount &&
        PveAssistCount == other.PveAssistCount &&
        PvpAssistCount == other.PvpAssistCount &&
        FirstOpenChestCount == other.FirstOpenChestCount &&
        CombatExperience == other.CombatExperience &&
        ExplorationExperience == other.ExplorationExperience &&
        LootExperience == other.LootExperience &&
        EligibleExtractedLootValue == other.EligibleExtractedLootValue &&
        ProvisionalExperienceTotal == other.ProvisionalExperienceTotal &&
        RetentionBasisPoints == other.RetentionBasisPoints &&
        ConsolidatedExperience == other.ConsolidatedExperience &&
        PreviousLevel == other.PreviousLevel &&
        PreviousExperience == other.PreviousExperience &&
        ResultingLevel == other.ResultingLevel &&
        ResultingExperience == other.ResultingExperience &&
        LevelsGained == other.LevelsGained &&
        NextLevelExperienceRequirement == other.NextLevelExperienceRequirement &&
        IsMaxLevel == other.IsMaxLevel;

    public override bool Equals(object obj) =>
        obj is ExpeditionProgressionResult other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Outcome;
            hash = (hash * 397) ^ PveKillCount;
            hash = (hash * 397) ^ PvpKillCount;
            hash = (hash * 397) ^ PveAssistCount;
            hash = (hash * 397) ^ PvpAssistCount;
            hash = (hash * 397) ^ FirstOpenChestCount;
            hash = (hash * 397) ^ CombatExperience.GetHashCode();
            hash = (hash * 397) ^ ExplorationExperience.GetHashCode();
            hash = (hash * 397) ^ LootExperience.GetHashCode();
            hash = (hash * 397) ^ EligibleExtractedLootValue.GetHashCode();
            hash = (hash * 397) ^ ProvisionalExperienceTotal.GetHashCode();
            hash = (hash * 397) ^ RetentionBasisPoints;
            hash = (hash * 397) ^ ConsolidatedExperience.GetHashCode();
            hash = (hash * 397) ^ PreviousLevel;
            hash = (hash * 397) ^ PreviousExperience.GetHashCode();
            hash = (hash * 397) ^ ResultingLevel;
            hash = (hash * 397) ^ ResultingExperience.GetHashCode();
            hash = (hash * 397) ^ LevelsGained;
            hash = (hash * 397) ^ NextLevelExperienceRequirement.GetHashCode();
            return (hash * 397) ^ IsMaxLevel.GetHashCode();
        }
    }

    public static bool operator ==(
        ExpeditionProgressionResult left,
        ExpeditionProgressionResult right) => left.Equals(right);

    public static bool operator !=(
        ExpeditionProgressionResult left,
        ExpeditionProgressionResult right) => !left.Equals(right);
}
