using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class DungeonPressureConfigTests
{
    private void SetConfigValues(DungeonPressureConfig config, int total, int reinforcements, int critical)
    {
        var type = typeof(DungeonPressureConfig);
        type.GetField("_totalDurationSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, total);
        type.GetField("_reinforcementsThresholdSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, reinforcements);
        type.GetField("_criticalPressureThresholdSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, critical);
    }

    [Test]
    public void Validate_ValidConfig_ReturnsTrue()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        SetConfigValues(config, 600, 300, 120);

        bool isValid = config.Validate(out string error);

        Assert.IsTrue(isValid);
        Assert.IsEmpty(error);
    }

    [Test]
    public void Validate_TotalDurationZero_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        SetConfigValues(config, 0, 300, 120);

        bool isValid = config.Validate(out string error);

        Assert.IsFalse(isValid);
        Assert.AreEqual("TotalDurationSeconds must be greater than 0.", error);
    }

    [Test]
    public void Validate_CriticalPressureZero_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        SetConfigValues(config, 600, 300, 0);

        bool isValid = config.Validate(out string error);

        Assert.IsFalse(isValid);
        Assert.AreEqual("CriticalPressureThresholdSeconds must be greater than 0.", error);
    }

    [Test]
    public void Validate_ReinforcementsNotGreaterThanCritical_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        SetConfigValues(config, 600, 120, 120); // equal

        bool isValid = config.Validate(out string error);

        Assert.IsFalse(isValid);
        Assert.AreEqual("ReinforcementsThresholdSeconds must be strictly greater than CriticalPressureThresholdSeconds.", error);
    }

    [Test]
    public void Validate_TotalNotGreaterThanReinforcements_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        SetConfigValues(config, 300, 300, 120); // equal

        bool isValid = config.Validate(out string error);

        Assert.IsFalse(isValid);
        Assert.AreEqual("TotalDurationSeconds must be strictly greater than ReinforcementsThresholdSeconds.", error);
    }
}
