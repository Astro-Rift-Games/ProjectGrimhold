using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class DungeonPressureConfigTests
{
    private void SetConfigValues(DungeonPressureConfig config, int total, int reinforcements, int critical, int maxGlobal = 40)
    {
        var type = typeof(DungeonPressureConfig);
        type.GetField("_totalDurationSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, total);
        type.GetField("_reinforcementsThresholdSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, reinforcements);
        type.GetField("_criticalPressureThresholdSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, critical);
        type.GetField("_maxGlobalEnemies", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, maxGlobal);
    }

    private void SetPolicyValue(DungeonPressureConfig config, string fieldName, Spawning.ReinforcementPolicy policy)
    {
        typeof(DungeonPressureConfig).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, policy);
    }

    private DungeonPressureConfig CreateValidConfig()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        SetConfigValues(config, 600, 300, 120, 40);
        
        SetPolicyValue(config, "_normalPolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 0 });
        
        SetPolicyValue(config, "_reinforcementsPolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 5, EvaluationIntervalSeconds = 15f, MaxSpawnsPerAttempt = 2 });
        SetPolicyValue(config, "_criticalPressurePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 10, EvaluationIntervalSeconds = 5f, MaxSpawnsPerAttempt = 5 });
        SetPolicyValue(config, "_collapsePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 15, EvaluationIntervalSeconds = 3f, MaxSpawnsPerAttempt = 5 });
        
        return config;
    }

    [Test]
    public void Validate_ValidConfig_ReturnsTrue()
    {
        var config = CreateValidConfig();
        bool isValid = config.Validate(out string error);
        Assert.IsTrue(isValid, error);
    }

    [Test]
    public void Validate_NormalWithBudget_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_normalPolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 1, EvaluationIntervalSeconds = 1f, MaxSpawnsPerAttempt = 1 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("must have PopulationBudget = 0", error);
    }

    [Test]
    public void Validate_CollapseWithoutBudget_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_collapsePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 0, EvaluationIntervalSeconds = 3f, MaxSpawnsPerAttempt = 5 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("collapse applies maximum PvE pressure", error);
    }

    [Test]
    public void Validate_CollapseWeakerThanCritical_Budget_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_collapsePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 8, EvaluationIntervalSeconds = 3f, MaxSpawnsPerAttempt = 5 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("cannot be less than CriticalPressure PopulationBudget", error);
    }

    [Test]
    public void Validate_CollapseWeakerThanCritical_Interval_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_collapsePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 15, EvaluationIntervalSeconds = 10f, MaxSpawnsPerAttempt = 5 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("Collapse should be more frequent", error);
    }

    [Test]
    public void Validate_CollapseWeakerThanCritical_MaxSpawns_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_collapsePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 15, EvaluationIntervalSeconds = 3f, MaxSpawnsPerAttempt = 2 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("cannot be less than CriticalPressure MaxSpawnsPerAttempt", error);
    }

    [Test]
    public void Validate_CriticalWeakerThanReinforcements_Budget_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_criticalPressurePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 3, EvaluationIntervalSeconds = 5f, MaxSpawnsPerAttempt = 2 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("cannot be less than Reinforcements PopulationBudget", error);
    }

    [Test]
    public void Validate_CriticalWeakerThanReinforcements_Interval_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_criticalPressurePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 10, EvaluationIntervalSeconds = 20f, MaxSpawnsPerAttempt = 5 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("Critical should be more frequent", error);
    }

    [Test]
    public void Validate_CriticalWeakerThanReinforcements_MaxSpawns_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_criticalPressurePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 10, EvaluationIntervalSeconds = 5f, MaxSpawnsPerAttempt = 1 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("cannot be less than Reinforcements MaxSpawnsPerAttempt", error);
    }

    [Test]
    public void Validate_PopulationBudgetExceedsGlobal_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_criticalPressurePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 50, EvaluationIntervalSeconds = 5f, MaxSpawnsPerAttempt = 5 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("exceeding MaxGlobalEnemies", error);
    }

    [Test]
    public void Validate_MaxSpawnsExceedsBudget_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_criticalPressurePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 10, EvaluationIntervalSeconds = 5f, MaxSpawnsPerAttempt = 15 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("exceeding PopulationBudget", error);
    }

    [Test]
    public void Validate_IntervalZero_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_criticalPressurePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 10, EvaluationIntervalSeconds = 0f, MaxSpawnsPerAttempt = 5 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("EvaluationIntervalSeconds is <= 0", error);
    }

    [Test]
    public void Validate_MaxSpawnsZero_ReturnsFalse()
    {
        var config = CreateValidConfig();
        SetPolicyValue(config, "_criticalPressurePolicy", new Spawning.ReinforcementPolicy { PopulationBudget = 10, EvaluationIntervalSeconds = 5f, MaxSpawnsPerAttempt = 0 });
        
        Assert.IsFalse(config.Validate(out string error));
        StringAssert.Contains("MaxSpawnsPerAttempt is < 1", error);
    }
}
