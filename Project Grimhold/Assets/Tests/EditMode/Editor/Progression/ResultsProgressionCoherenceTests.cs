#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

public sealed class ResultsProgressionCoherenceTests
{
    private const string SaverPath =
        "Assets/Scripts/Player/Extraction/PlayerExtractionLootSaver.cs";
    private const string PresenterPath =
        "Assets/Scripts/Player/Presentation/RaidMenuPresenter.cs";

    [Test]
    public void ExpeditionProgressionResult_DeclaresWithPersistedProgression()
    {
        MethodInfo method = typeof(ExpeditionProgressionResult).GetMethod(
            "WithPersistedProgression",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null,
            "ExpeditionProgressionResult must provide WithPersistedProgression() to update presentation when backend or store persists a corrected level/experience.");
    }

    [Test]
    public void ExpeditionProgressionResult_WithPersistedProgression_CalculatesLevelsGainedAndUpdatesProjection()
    {
        var resolution = new ExpeditionExperienceResolution(
            ExpeditionExperienceResolutionOutcome.Extracted,
            new ExpeditionExperienceSnapshot(100, 50, 20, 30),
            10000,
            200);

        var application = new ExperienceApplicationResult(
            previousLevel: 1,
            previousExperience: 0,
            resultingLevel: 2,
            resultingExperience: 100,
            levelsGained: 1);

        var initial = new ExpeditionProgressionResult(
            resolution,
            application,
            pveKillCount: 5,
            pvpKillCount: 1,
            pveAssistCount: 2,
            pvpAssistCount: 0,
            firstOpenChestCount: 3,
            eligibleExtractedLootValue: 500,
            nextLevelExperienceRequirement: 200,
            isMaxLevel: false);

        ExperienceCurve curve = ProgressionBalanceDefaults.InitialExperienceCurve;

        // When backend/store persists level 3 with 50 XP
        ExpeditionProgressionResult adjusted = initial.WithPersistedProgression(3, 50, curve);

        Assert.That(adjusted.ResultingLevel, Is.EqualTo(3));
        Assert.That(adjusted.ResultingExperience, Is.EqualTo(50));
        Assert.That(adjusted.LevelsGained, Is.EqualTo(2), "LevelsGained must reflect difference from PreviousLevel (3 - 1 = 2).");
        Assert.That(adjusted.PreviousLevel, Is.EqualTo(1));
        Assert.That(adjusted.PveKillCount, Is.EqualTo(5), "Activity counts must be preserved.");
        Assert.That(adjusted.PvpKillCount, Is.EqualTo(1));
        Assert.That(adjusted.FirstOpenChestCount, Is.EqualTo(3));
        Assert.That(adjusted.EligibleExtractedLootValue, Is.EqualTo(500));

        // When values are identical, returns unchanged instance
        ExpeditionProgressionResult identical = adjusted.WithPersistedProgression(3, 50, curve);
        Assert.That(identical, Is.EqualTo(adjusted));
    }

    [Test]
    public void ExtractionLootSaver_CalculatesConsolidatedExperienceWithFullExpeditionLedger()
    {
        string source = File.ReadAllText(SaverPath);

        Assert.That(source, Does.Contain("ExpeditionExperienceRules"),
            "PlayerExtractionLootSaver must use ExpeditionExperienceRules to combine ledger experience with extracted loot.");
        Assert.That(source, Does.Not.Contain("new ExpeditionExperienceResolution(\n                ExpeditionExperienceResolutionOutcome.Extracted,\n                default,\n                10000,"),
            "PlayerExtractionLootSaver must not hardcode 10000 basis points or ignore the expedition ledger.");
    }

    [Test]
    public void RaidMenuPresenter_DetectsProgressionCorrectionAndDisplaysNotice()
    {
        string source = File.ReadAllText(PresenterPath);

        Assert.That(source, Does.Contain("WithPersistedProgression"),
            "RaidMenuPresenter must update the displayed snapshot with persisted progression when it differs.");
        Assert.That(source, Does.Contain("Progresión ajustada") | Does.Contain("ajustada por el servidor"),
            "RaidMenuPresenter must display a correction notice when the persisted values diverge from the simulation.");
    }
}
#endif
