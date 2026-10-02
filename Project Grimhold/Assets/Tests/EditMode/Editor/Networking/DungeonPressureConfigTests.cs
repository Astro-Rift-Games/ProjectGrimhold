using NUnit.Framework;
using UnityEngine;

public class DungeonPressureConfigTests
{
    [Test]
    public void Validate_ValidConfig_ReturnsTrue()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        var so = new UnityEditor.SerializedObject(config);
        so.FindProperty("_totalDurationSeconds").intValue = 600;
        so.FindProperty("_reinforcementsThresholdSeconds").intValue = 300;
        so.FindProperty("_criticalPressureThresholdSeconds").intValue = 120;
        so.ApplyModifiedPropertiesWithoutUndo();

        bool isValid = config.Validate(out string error);

        Assert.IsTrue(isValid);
        Assert.IsEmpty(error);
    }

    [Test]
    public void Validate_TotalDurationZero_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        var so = new UnityEditor.SerializedObject(config);
        so.FindProperty("_totalDurationSeconds").intValue = 0;
        so.FindProperty("_reinforcementsThresholdSeconds").intValue = 300;
        so.FindProperty("_criticalPressureThresholdSeconds").intValue = 120;
        so.ApplyModifiedPropertiesWithoutUndo();

        bool isValid = config.Validate(out string error);

        Assert.IsFalse(isValid);
        Assert.AreEqual("TotalDurationSeconds must be greater than 0.", error);
    }

    [Test]
    public void Validate_CriticalPressureZero_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        var so = new UnityEditor.SerializedObject(config);
        so.FindProperty("_totalDurationSeconds").intValue = 600;
        so.FindProperty("_reinforcementsThresholdSeconds").intValue = 300;
        so.FindProperty("_criticalPressureThresholdSeconds").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        bool isValid = config.Validate(out string error);

        Assert.IsFalse(isValid);
        Assert.AreEqual("CriticalPressureThresholdSeconds must be greater than 0.", error);
    }

    [Test]
    public void Validate_ReinforcementsNotGreaterThanCritical_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        var so = new UnityEditor.SerializedObject(config);
        so.FindProperty("_totalDurationSeconds").intValue = 600;
        so.FindProperty("_reinforcementsThresholdSeconds").intValue = 120;
        so.FindProperty("_criticalPressureThresholdSeconds").intValue = 120; // equal
        so.ApplyModifiedPropertiesWithoutUndo();

        bool isValid = config.Validate(out string error);

        Assert.IsFalse(isValid);
        Assert.AreEqual("ReinforcementsThresholdSeconds must be strictly greater than CriticalPressureThresholdSeconds.", error);
    }

    [Test]
    public void Validate_TotalNotGreaterThanReinforcements_ReturnsFalse()
    {
        var config = ScriptableObject.CreateInstance<DungeonPressureConfig>();
        var so = new UnityEditor.SerializedObject(config);
        so.FindProperty("_totalDurationSeconds").intValue = 300; // equal
        so.FindProperty("_reinforcementsThresholdSeconds").intValue = 300;
        so.FindProperty("_criticalPressureThresholdSeconds").intValue = 120;
        so.ApplyModifiedPropertiesWithoutUndo();

        bool isValid = config.Validate(out string error);

        Assert.IsFalse(isValid);
        Assert.AreEqual("TotalDurationSeconds must be strictly greater than ReinforcementsThresholdSeconds.", error);
    }
}
