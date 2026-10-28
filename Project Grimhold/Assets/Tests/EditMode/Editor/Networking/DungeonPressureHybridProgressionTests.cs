using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Spawning;

public class DungeonPressureHybridProgressionTests
{
    private const float DeltaTime = 1f / 60f; // 60 Hz tick rate

    private DungeonPressureConfig CreateTestConfig()
    {
        DungeonPressureConfig config;
        try
        {
            config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        }
        catch
        {
            config = (DungeonPressureConfig)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(DungeonPressureConfig));
        }
        var type = typeof(DungeonPressureConfig);

        type.GetField("_totalDurationSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 900); // 15 min
        type.GetField("_reinforcementsThresholdSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 300); // 5 min
        type.GetField("_criticalPressureThresholdSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 120); // 2 min
        type.GetField("_collapseDurationSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 120); // 2 min
        type.GetField("_maxGlobalEnemies", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 40);

        type.GetField("_e0InstabilityThreshold", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 100);
        type.GetField("_e0MinIntervalSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 60);
        type.GetField("_e1InstabilityThreshold", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 150);
        type.GetField("_e1MinIntervalSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 60);
        type.GetField("_e2InstabilityThreshold", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 200);
        type.GetField("_e2MinIntervalSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 60);
        type.GetField("_minCollapseStartElapsedSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 480); // 8 min

        type.GetField("_ordinaryEnemyPoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 1);
        type.GetField("_eliteEnemyPoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 5);
        type.GetField("_bossEnemyPoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 15);
        type.GetField("_reinforcementEnemyPoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 2);

        type.GetField("_conventionalTreasurePoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 10);
        type.GetField("_divineTreasurePoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 20);
        type.GetField("_cursedTreasurePoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 25);
        type.GetField("_chaosTreasurePoints", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 30);

        type.GetField("_normalPolicy", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, new ReinforcementPolicy { PopulationBudget = 0 });
        type.GetField("_reinforcementsPolicy", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, new ReinforcementPolicy { PopulationBudget = 5, EvaluationIntervalSeconds = 15f, MaxSpawnsPerAttempt = 2 });
        type.GetField("_criticalPressurePolicy", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, new ReinforcementPolicy { PopulationBudget = 10, EvaluationIntervalSeconds = 5f, MaxSpawnsPerAttempt = 5 });
        type.GetField("_collapsePolicy", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, new ReinforcementPolicy { PopulationBudget = 15, EvaluationIntervalSeconds = 3f, MaxSpawnsPerAttempt = 5 });

        return config;
    }

    [Test]
    public void Step_TimeOnlyProgression_AdvancesPhasesAndClosesAt17Min()
    {
        int remainingTicks = 900 * 60;
        int collapseRemainingTicks = 0;
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.Normal;
        int currentInstability = 0;
        int phaseElapsedTicks = 0;
        int totalElapsedTicks = 0;

        // Step 600 seconds + 1 tick (10 min elapsed) -> remaining ticks <= 300s -> Reinforcements phase
        for (int i = 0; i <= 600 * 60; i++)
        {
            DungeonPressureClock.Step(
                ref remainingTicks,
                ref collapseRemainingTicks,
                ref state,
                ref phase,
                ref currentInstability,
                ref phaseElapsedTicks,
                ref totalElapsedTicks,
                isRaidInProgress: true,
                isRaidClosingOrFinished: false,
                reinforcementsThresholdSecs: 300,
                criticalPressureThresholdSecs: 120,
                collapseDurationSecs: 120,
                totalDurationSecs: 900,
                instabilityThreshold: 100,
                minIntervalSecs: 60,
                minCollapseStartElapsedSecs: 480,
                deltaTime: DeltaTime);
        }

        Assert.AreEqual(DungeonPressurePhase.Reinforcements, phase);

        // Step another 180 seconds (13 min total elapsed) -> remaining <= 120s -> CriticalPressure phase
        for (int i = 0; i < 180 * 60; i++)
        {
            DungeonPressureClock.Step(
                ref remainingTicks,
                ref collapseRemainingTicks,
                ref state,
                ref phase,
                ref currentInstability,
                ref phaseElapsedTicks,
                ref totalElapsedTicks,
                isRaidInProgress: true,
                isRaidClosingOrFinished: false,
                reinforcementsThresholdSecs: 300,
                criticalPressureThresholdSecs: 120,
                collapseDurationSecs: 120,
                totalDurationSecs: 900,
                instabilityThreshold: 150,
                minIntervalSecs: 60,
                minCollapseStartElapsedSecs: 480,
                deltaTime: DeltaTime);
        }

        Assert.AreEqual(DungeonPressurePhase.CriticalPressure, phase);

        // Step remaining ticks until 15 min total elapsed -> remaining = 0 -> Collapse phase
        while (remainingTicks > 0)
        {
            DungeonPressureClock.Step(
                ref remainingTicks,
                ref collapseRemainingTicks,
                ref state,
                ref phase,
                ref currentInstability,
                ref phaseElapsedTicks,
                ref totalElapsedTicks,
                isRaidInProgress: true,
                isRaidClosingOrFinished: false,
                reinforcementsThresholdSecs: 300,
                criticalPressureThresholdSecs: 120,
                collapseDurationSecs: 120,
                totalDurationSecs: 900,
                instabilityThreshold: 200,
                minIntervalSecs: 60,
                minCollapseStartElapsedSecs: 480,
                deltaTime: DeltaTime);
        }

        Assert.AreEqual(DungeonPressurePhase.Collapse, phase);
        Assert.AreEqual(120 * 60, collapseRemainingTicks);

        // Step remaining collapse ticks -> Stopped at exactly 17 min
        while (collapseRemainingTicks > 0)
        {
            DungeonPressureClock.Step(
                ref remainingTicks,
                ref collapseRemainingTicks,
                ref state,
                ref phase,
                ref currentInstability,
                ref phaseElapsedTicks,
                ref totalElapsedTicks,
                isRaidInProgress: true,
                isRaidClosingOrFinished: false,
                reinforcementsThresholdSecs: 300,
                criticalPressureThresholdSecs: 120,
                collapseDurationSecs: 120,
                totalDurationSecs: 900,
                instabilityThreshold: 200,
                minIntervalSecs: 60,
                minCollapseStartElapsedSecs: 480,
                deltaTime: DeltaTime);
        }

        Assert.AreEqual(DungeonPressureState.Stopped, state);
        Assert.AreEqual(1020 * 60, totalElapsedTicks); // Exactly 17 minutes
    }

    [Test]
    public void Step_Instability_AdvancesNormalToReinforcements_WhenThresholdReachedAfterMinInterval()
    {
        int remainingTicks = 900 * 60;
        int collapseRemainingTicks = 0;
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.Normal;
        int currentInstability = 0;
        int phaseElapsedTicks = 0;
        int totalElapsedTicks = 0;

        // Step 60 seconds (min interval requirement = 60s)
        for (int i = 0; i < 60 * 60; i++)
        {
            DungeonPressureClock.Step(
                ref remainingTicks,
                ref collapseRemainingTicks,
                ref state,
                ref phase,
                ref currentInstability,
                ref phaseElapsedTicks,
                ref totalElapsedTicks,
                isRaidInProgress: true,
                isRaidClosingOrFinished: false,
                reinforcementsThresholdSecs: 300,
                criticalPressureThresholdSecs: 120,
                collapseDurationSecs: 120,
                totalDurationSecs: 900,
                instabilityThreshold: 100,
                minIntervalSecs: 60,
                minCollapseStartElapsedSecs: 480,
                deltaTime: DeltaTime);
        }

        Assert.AreEqual(DungeonPressurePhase.Normal, phase);

        // Instability threshold reached with overflow (100 threshold, but 125 accumulated)
        currentInstability = 125;

        // One step should trigger transition and discard overflow
        DungeonPressureClock.Step(
            ref remainingTicks,
            ref collapseRemainingTicks,
            ref state,
            ref phase,
            ref currentInstability,
            ref phaseElapsedTicks,
            ref totalElapsedTicks,
            isRaidInProgress: true,
            isRaidClosingOrFinished: false,
            reinforcementsThresholdSecs: 300,
            criticalPressureThresholdSecs: 120,
            collapseDurationSecs: 120,
            totalDurationSecs: 900,
            instabilityThreshold: 100,
            minIntervalSecs: 60,
            minCollapseStartElapsedSecs: 480,
            deltaTime: DeltaTime);

        Assert.AreEqual(DungeonPressurePhase.Reinforcements, phase);
        Assert.AreEqual(0, currentInstability, "Overflow points must be completely discarded and reset to 0.");
        Assert.AreEqual(0, phaseElapsedTicks, "Phase elapsed ticks must reset to 0 upon transition.");
        Assert.AreEqual(300 * 60, remainingTicks, "Remaining ticks should be initialized to reinforcements threshold.");
    }

    [Test]
    public void Step_Instability_SaturatesAtCap_WhenThresholdReachedBeforeMinInterval()
    {
        int remainingTicks = 900 * 60;
        int collapseRemainingTicks = 0;
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.Normal;
        int currentInstability = 0;
        int phaseElapsedTicks = 0;
        int totalElapsedTicks = 0;

        // Step only 30 seconds (< 60s min interval)
        for (int i = 0; i < 30 * 60; i++)
        {
            DungeonPressureClock.Step(
                ref remainingTicks,
                ref collapseRemainingTicks,
                ref state,
                ref phase,
                ref currentInstability,
                ref phaseElapsedTicks,
                ref totalElapsedTicks,
                isRaidInProgress: true,
                isRaidClosingOrFinished: false,
                reinforcementsThresholdSecs: 300,
                criticalPressureThresholdSecs: 120,
                collapseDurationSecs: 120,
                totalDurationSecs: 900,
                instabilityThreshold: 100,
                minIntervalSecs: 60,
                minCollapseStartElapsedSecs: 480,
                deltaTime: DeltaTime);
        }

        // Instability reaches and exceeds threshold
        currentInstability = 150;

        DungeonPressureClock.Step(
            ref remainingTicks,
            ref collapseRemainingTicks,
            ref state,
            ref phase,
            ref currentInstability,
            ref phaseElapsedTicks,
            ref totalElapsedTicks,
            isRaidInProgress: true,
            isRaidClosingOrFinished: false,
            reinforcementsThresholdSecs: 300,
            criticalPressureThresholdSecs: 120,
            collapseDurationSecs: 120,
            totalDurationSecs: 900,
            instabilityThreshold: 100,
            minIntervalSecs: 60,
            minCollapseStartElapsedSecs: 480,
            deltaTime: DeltaTime);

        // Min interval not met -> cannot advance, saturates at threshold cap
        Assert.AreEqual(DungeonPressurePhase.Normal, phase);
        Assert.AreEqual(100, currentInstability, "Instability must saturate at cap while awaiting minimum interval.");

        // Step until min interval is satisfied (remaining 29 seconds and 59 ticks)
        int ticksToReach60s = (60 * 60) - phaseElapsedTicks;
        for (int i = 0; i < ticksToReach60s; i++)
        {
            DungeonPressureClock.Step(
                ref remainingTicks,
                ref collapseRemainingTicks,
                ref state,
                ref phase,
                ref currentInstability,
                ref phaseElapsedTicks,
                ref totalElapsedTicks,
                isRaidInProgress: true,
                isRaidClosingOrFinished: false,
                reinforcementsThresholdSecs: 300,
                criticalPressureThresholdSecs: 120,
                collapseDurationSecs: 120,
                totalDurationSecs: 900,
                instabilityThreshold: 100,
                minIntervalSecs: 60,
                minCollapseStartElapsedSecs: 480,
                deltaTime: DeltaTime);
        }

        // Once min interval is reached with instability at cap, it advances!
        Assert.AreEqual(DungeonPressurePhase.Reinforcements, phase);
        Assert.AreEqual(0, currentInstability);
        Assert.AreEqual(0, phaseElapsedTicks);
    }

    [Test]
    public void Step_CollapsePhase_BlockedBeforeEightMinutes_EvenIfInstabilityThresholdSaturated()
    {
        int remainingTicks = 120 * 60;
        int collapseRemainingTicks = 0;
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.CriticalPressure;
        int currentInstability = 200;
        int phaseElapsedTicks = 70 * 60; // 70s (> 60s min interval)
        int totalElapsedTicks = 300 * 60; // 5 min (< 480s / 8 min minimum collapse start)

        DungeonPressureClock.Step(
            ref remainingTicks,
            ref collapseRemainingTicks,
            ref state,
            ref phase,
            ref currentInstability,
            ref phaseElapsedTicks,
            ref totalElapsedTicks,
            isRaidInProgress: true,
            isRaidClosingOrFinished: false,
            reinforcementsThresholdSecs: 300,
            criticalPressureThresholdSecs: 120,
            collapseDurationSecs: 120,
            totalDurationSecs: 900,
            instabilityThreshold: 200,
            minIntervalSecs: 60,
            minCollapseStartElapsedSecs: 480, // 8:00
            deltaTime: DeltaTime);

        // Phase must NOT advance to Collapse before 8 minutes!
        Assert.AreEqual(DungeonPressurePhase.CriticalPressure, phase);
        Assert.AreEqual(200, currentInstability, "Instability should remain saturated at cap.");

        // Advance totalElapsedTicks to 480s (8:00)
        totalElapsedTicks = 480 * 60;

        DungeonPressureClock.Step(
            ref remainingTicks,
            ref collapseRemainingTicks,
            ref state,
            ref phase,
            ref currentInstability,
            ref phaseElapsedTicks,
            ref totalElapsedTicks,
            isRaidInProgress: true,
            isRaidClosingOrFinished: false,
            reinforcementsThresholdSecs: 300,
            criticalPressureThresholdSecs: 120,
            collapseDurationSecs: 120,
            totalDurationSecs: 900,
            instabilityThreshold: 200,
            minIntervalSecs: 60,
            minCollapseStartElapsedSecs: 480,
            deltaTime: DeltaTime);

        // Now that 8 minutes have elapsed, Collapse is triggered
        Assert.AreEqual(DungeonPressurePhase.Collapse, phase);
        Assert.AreEqual(0, currentInstability);
    }

    [Test]
    public void Step_AbsoluteClosure_NeverAdvanced_WhenCollapseTriggeredEarly()
    {
        int remainingTicks = 120 * 60;
        int collapseRemainingTicks = 0;
        DungeonPressureState state = DungeonPressureState.Running;
        DungeonPressurePhase phase = DungeonPressurePhase.CriticalPressure;
        int currentInstability = 200;
        int phaseElapsedTicks = 60 * 60;
        int totalElapsedTicks = 480 * 60; // Exactly 8:00

        // Total duration = 900s, Collapse duration = 120s -> Total raid closure = 1020s (17:00).
        DungeonPressureClock.Step(
            ref remainingTicks,
            ref collapseRemainingTicks,
            ref state,
            ref phase,
            ref currentInstability,
            ref phaseElapsedTicks,
            ref totalElapsedTicks,
            isRaidInProgress: true,
            isRaidClosingOrFinished: false,
            reinforcementsThresholdSecs: 300,
            criticalPressureThresholdSecs: 120,
            collapseDurationSecs: 120,
            totalDurationSecs: 900,
            instabilityThreshold: 200,
            minIntervalSecs: 60,
            minCollapseStartElapsedSecs: 480,
            deltaTime: DeltaTime);

        Assert.AreEqual(DungeonPressurePhase.Collapse, phase);
        // Absolute closure at 1020s. Elapsed is 480s + 1 tick.
        // Expected remaining collapse seconds = 1020 - (480 + 1/60) = 539.9833s (~32399 ticks).
        int expectedRemainingTicks = (1020 * 60) - totalElapsedTicks;
        Assert.AreEqual(expectedRemainingTicks, collapseRemainingTicks);

        // Simulate through the entire extended collapse period
        while (collapseRemainingTicks > 1)
        {
            DungeonPressureClock.Step(
                ref remainingTicks,
                ref collapseRemainingTicks,
                ref state,
                ref phase,
                ref currentInstability,
                ref phaseElapsedTicks,
                ref totalElapsedTicks,
                isRaidInProgress: true,
                isRaidClosingOrFinished: false,
                reinforcementsThresholdSecs: 300,
                criticalPressureThresholdSecs: 120,
                collapseDurationSecs: 120,
                totalDurationSecs: 900,
                instabilityThreshold: 200,
                minIntervalSecs: 60,
                minCollapseStartElapsedSecs: 480,
                deltaTime: DeltaTime);
        }

        Assert.AreEqual(1, collapseRemainingTicks);
        Assert.AreEqual(DungeonPressureState.Running, state);

        // Final tick reaches absolute closure
        DungeonPressureClock.Step(
            ref remainingTicks,
            ref collapseRemainingTicks,
            ref state,
            ref phase,
            ref currentInstability,
            ref phaseElapsedTicks,
            ref totalElapsedTicks,
            isRaidInProgress: true,
            isRaidClosingOrFinished: false,
            reinforcementsThresholdSecs: 300,
            criticalPressureThresholdSecs: 120,
            collapseDurationSecs: 120,
            totalDurationSecs: 900,
            instabilityThreshold: 200,
            minIntervalSecs: 60,
            minCollapseStartElapsedSecs: 480,
            deltaTime: DeltaTime);

        Assert.AreEqual(0, collapseRemainingTicks);
        Assert.AreEqual(DungeonPressureState.Stopped, state);
        Assert.AreEqual(1020 * 60, totalElapsedTicks, "Dungeon must close at exactly 17:00 (1020 seconds) regardless of early collapse.");
    }

    [Test]
    public void DungeonPressureConfig_EnemyDefeatPoints_ConfiguredCorrectly()
    {
        var config = CreateTestConfig();

        Assert.AreEqual(1, config.GetEnemyPoints(CreatureTier.Ordinary, EnemyPopulationOrigin.Bootstrap));
        Assert.AreEqual(5, config.GetEnemyPoints(CreatureTier.Elite, EnemyPopulationOrigin.Bootstrap));
        Assert.AreEqual(15, config.GetEnemyPoints(CreatureTier.Boss, EnemyPopulationOrigin.Bootstrap));

        // Reinforcement origin overrides creature tier
        Assert.AreEqual(2, config.GetEnemyPoints(CreatureTier.Ordinary, EnemyPopulationOrigin.Reinforcement));
        Assert.AreEqual(2, config.GetEnemyPoints(CreatureTier.Elite, EnemyPopulationOrigin.Reinforcement));
        Assert.AreEqual(2, config.GetEnemyPoints(CreatureTier.Boss, EnemyPopulationOrigin.Reinforcement));
    }

    [Test]
    public void DungeonPressureConfig_TreasurePoints_ConfiguredCorrectly()
    {
        var config = CreateTestConfig();

        Assert.AreEqual(10, config.GetTreasurePoints("ConventionalTreasure"));
        Assert.AreEqual(20, config.GetTreasurePoints("DivineTreasure"));
        Assert.AreEqual(25, config.GetTreasurePoints("CursedTreasure"));
        Assert.AreEqual(30, config.GetTreasurePoints("ChaosTreasure"));

        // Fallback for empty/null/unknown
        Assert.AreEqual(10, config.GetTreasurePoints(null));
        Assert.AreEqual(10, config.GetTreasurePoints(""));
        Assert.AreEqual(10, config.GetTreasurePoints("UnknownTreasureFamily"));
    }

    [Test]
    public void DungeonPressureConfig_Validation_RequiresValidInstabilityConfig()
    {
        var config = CreateTestConfig();
        Assert.IsTrue(config.Validate(out string error), error);

        // Test invalid min collapse start
        typeof(DungeonPressureConfig).GetField("_minCollapseStartElapsedSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 0);
        Assert.IsFalse(config.Validate(out error));
        StringAssert.Contains("MinCollapseStartElapsedSeconds must be greater than 0", error);
    }
}
