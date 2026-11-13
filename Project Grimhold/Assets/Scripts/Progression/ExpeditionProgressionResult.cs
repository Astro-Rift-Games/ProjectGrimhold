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

    private ExpeditionProgressionResult(
        ExpeditionExperienceResolutionOutcome outcome,
        int pveKillCount,
        int pvpKillCount,
        int pveAssistCount,
        int pvpAssistCount,
        int firstOpenChestCount,
        long combatExperience,
        long explorationExperience,
        long lootExperience,
        long eligibleExtractedLootValue,
        long provisionalExperienceTotal,
        int retentionBasisPoints,
        long consolidatedExperience,
        int previousLevel,
        long previousExperience,
        int resultingLevel,
        long resultingExperience,
        int levelsGained,
        long nextLevelExperienceRequirement,
        bool isMaxLevel)
    {
        Outcome = outcome;
        PveKillCount = pveKillCount;
        PvpKillCount = pvpKillCount;
        PveAssistCount = pveAssistCount;
        PvpAssistCount = pvpAssistCount;
        FirstOpenChestCount = firstOpenChestCount;
        CombatExperience = combatExperience;
        ExplorationExperience = explorationExperience;
        LootExperience = lootExperience;
        EligibleExtractedLootValue = eligibleExtractedLootValue;
        ProvisionalExperienceTotal = provisionalExperienceTotal;
        RetentionBasisPoints = retentionBasisPoints;
        ConsolidatedExperience = consolidatedExperience;
        PreviousLevel = previousLevel;
        PreviousExperience = previousExperience;
        ResultingLevel = resultingLevel;
        ResultingExperience = resultingExperience;
        LevelsGained = levelsGained;
        NextLevelExperienceRequirement = nextLevelExperienceRequirement;
        IsMaxLevel = isMaxLevel;
    }

    /// <summary>
    /// Returns a new projection updated with backend or store authoritative persisted progression.
    /// Preserves recorded activity counts and bases while keeping displayed results coherent with persistence.
    /// </summary>
    public ExpeditionProgressionResult WithPersistedProgression(
        int persistedLevel,
        long persistedExperience,
        ExperienceCurve curve)
    {
        if (persistedLevel == ResultingLevel && persistedExperience == ResultingExperience)
        {
            return this;
        }

        bool isMaxLevel = curve != null && persistedLevel >= curve.MaximumLevel;
        long nextLevelReq = 0;
        if (!isMaxLevel && curve != null)
        {
            curve.TryGetRequiredExperience(persistedLevel, out nextLevelReq);
        }

        int levelsGained = Math.Max(0, persistedLevel - PreviousLevel);

        return new ExpeditionProgressionResult(
            Outcome,
            PveKillCount,
            PvpKillCount,
            PveAssistCount,
            PvpAssistCount,
            FirstOpenChestCount,
            CombatExperience,
            ExplorationExperience,
            LootExperience,
            EligibleExtractedLootValue,
            ProvisionalExperienceTotal,
            RetentionBasisPoints,
            ConsolidatedExperience,
            PreviousLevel,
            PreviousExperience,
            persistedLevel,
            persistedExperience,
            levelsGained,
            nextLevelReq,
            isMaxLevel);
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
