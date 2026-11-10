using NUnit.Framework;
using System.Collections.Generic;

public class MissionLifecycleRulesTests
{
    [TestCase(MissionState.Disponible, MissionState.Activa, ExpectedResult = true)]
    [TestCase(MissionState.Activa, MissionState.Completada, ExpectedResult = true)]
    [TestCase(MissionState.Activa, MissionState.Abandonada, ExpectedResult = true)]
    [TestCase(MissionState.Completada, MissionState.PendienteDeReclamar, ExpectedResult = true)]
    [TestCase(MissionState.PendienteDeReclamar, MissionState.Reclamada, ExpectedResult = true)]
    public bool CanTransitionTo_ValidTransitions_ReturnsTrue(MissionState current, MissionState target)
    {
        return MissionLifecycleRules.CanTransitionTo(current, target);
    }

    [TestCase(MissionState.Disponible, MissionState.Completada, ExpectedResult = false)]
    [TestCase(MissionState.Activa, MissionState.Reclamada, ExpectedResult = false)]
    [TestCase(MissionState.Activa, MissionState.PendienteDeReclamar, ExpectedResult = false)]
    [TestCase(MissionState.Completada, MissionState.Reclamada, ExpectedResult = false)]
    [TestCase(MissionState.Reclamada, MissionState.Completada, ExpectedResult = false)]
    [TestCase(MissionState.Abandonada, MissionState.Activa, ExpectedResult = false)]
    public bool CanTransitionTo_InvalidTransitions_ReturnsFalse(MissionState current, MissionState target)
    {
        return MissionLifecycleRules.CanTransitionTo(current, target);
    }

    [Test]
    public void CanAcceptMission_UnderLimit_ReturnsTrue()
    {
        var activeTypes = new List<MissionType> { MissionType.Normal, MissionType.Unica };
        Assert.IsTrue(MissionLifecycleRules.CanAcceptMission(MissionType.Normal, activeTypes));
    }

    [Test]
    public void CanAcceptMission_AtLimit_ReturnsFalse()
    {
        var activeTypes = new List<MissionType> { MissionType.Normal, MissionType.Normal, MissionType.Unica };
        Assert.IsFalse(MissionLifecycleRules.CanAcceptMission(MissionType.Normal, activeTypes));
    }

    [Test]
    public void CanAcceptMission_Semanal_IgnoresLimit()
    {
        var activeTypes = new List<MissionType> { MissionType.Normal, MissionType.Normal, MissionType.Normal };
        Assert.IsTrue(MissionLifecycleRules.CanAcceptMission(MissionType.Semanal, activeTypes));
    }

    [Test]
    public void CanAcceptMission_ActiveSemanal_DoesNotCountTowardsLimit()
    {
        var activeTypes = new List<MissionType> { MissionType.Normal, MissionType.Normal, MissionType.Semanal };
        Assert.IsTrue(MissionLifecycleRules.CanAcceptMission(MissionType.Normal, activeTypes));
    }
}
