using NUnit.Framework;
using UnityEditor;

public sealed class RaidAbsoluteClosureTests
{
    [TestCase(RaidParticipantState.Raiding, true)]
    [TestCase(RaidParticipantState.Extracted, false)]
    [TestCase(RaidParticipantState.Defeated, false)]
    [TestCase(RaidParticipantState.Aborted, false)]
    public void Selection_OnlyUnresolvedRaiders_RegardlessOfConnectivityOrDowned(
        RaidParticipantState state, bool expected)
    {
        // Connectivity and Downed do not change the functional participant state.
        Assert.That(AbsoluteClosureRules.ShouldDefeat(state), Is.EqualTo(expected));
    }

    [Test]
    public void DefaultAsset_TotalPlusCollapse_Equals17Minutes()
    {
        var config = AssetDatabase.LoadAssetAtPath<DungeonPressureConfig>(
            "Assets/Scriptable Objects/DefaultDungeonPressureConfig.asset");
        Assert.That(config, Is.Not.Null);
        Assert.That(config.Validate(out string error), Is.True, error);
        Assert.That(config.TotalDurationSeconds, Is.EqualTo(900));
        Assert.That(config.CollapseDurationSeconds, Is.EqualTo(120));
        Assert.That(config.TotalDurationSeconds + config.CollapseDurationSeconds, Is.EqualTo(17 * 60));
        Assert.That(config.ReinforcementsThresholdSeconds, Is.EqualTo(240));
        Assert.That(config.CriticalPressureThresholdSeconds, Is.EqualTo(60));
    }
}
