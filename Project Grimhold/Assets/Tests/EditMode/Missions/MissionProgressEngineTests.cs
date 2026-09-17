using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class MissionProgressEngineTests
{
    private MissionDefinition _testMission;
    private MissionInstanceState _state;
    private ProfileId _profileId;

    [SetUp]
    public void SetUp()
    {
        _profileId = new ProfileId("player1");

        _testMission = ScriptableObject.CreateInstance<MissionDefinition>();
        typeof(MissionDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_testMission, "test_mission");

        _testMission.Phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                Objectives = new List<ObjectiveDefinition>
                {
                    new ObjectiveDefinition { Family = ObjectiveFamily.EliminacionPvE, Condition = new ObjectiveCondition { RequiredAmount = 5 } },
                    new ObjectiveDefinition { Family = ObjectiveFamily.Interaccion, Condition = new ObjectiveCondition { TargetId = "chest_silver", RequiredAmount = 1 } }
                }
            },
            new PhaseDefinition
            {
                Objectives = new List<ObjectiveDefinition>
                {
                    new ObjectiveDefinition { Family = ObjectiveFamily.Exploracion, Condition = new ObjectiveCondition { ZoneId = "boss_room", RequiredAmount = 1 } }
                }
            }
        };

        _state = new MissionInstanceState(new MissionId("test_mission"), MissionState.Activa);
    }

    [Test]
    public void ApplyProgress_IncrementsCorrectObjective_WithoutOverflow()
    {
        var ev = new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 3, _profileId);
        bool progressed = MissionProgressEngine.TryApplyProgress(_state, _testMission, ev);

        Assert.IsTrue(progressed);
        Assert.AreEqual(3, _state.ObjectiveProgress[0].CurrentAmount);
        Assert.IsFalse(_state.ObjectiveProgress.ContainsKey(1));

        var ev2 = new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 5, _profileId);
        bool progressed2 = MissionProgressEngine.TryApplyProgress(_state, _testMission, ev2);

        Assert.IsTrue(progressed2);
        Assert.AreEqual(5, _state.ObjectiveProgress[0].CurrentAmount); // Capped at 5
    }

    [Test]
    public void ApplyProgress_TargetFilter_RejectsMismatchedTarget()
    {
        var evFail = new MissionContributionEvent(ObjectiveFamily.Interaccion, 1, _profileId, targetId: "chest_wood");
        bool progressedFail = MissionProgressEngine.TryApplyProgress(_state, _testMission, evFail);

        Assert.IsFalse(progressedFail);
        Assert.IsFalse(_state.ObjectiveProgress.ContainsKey(1));

        var evSuccess = new MissionContributionEvent(ObjectiveFamily.Interaccion, 1, _profileId, targetId: "chest_silver");
        bool progressedSuccess = MissionProgressEngine.TryApplyProgress(_state, _testMission, evSuccess);

        Assert.IsTrue(progressedSuccess);
        Assert.AreEqual(1, _state.ObjectiveProgress[1].CurrentAmount);
    }

    [Test]
    public void ApplyProgress_CompletesPhase_AdvancesToNextPhase()
    {
        var ev1 = new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 5, _profileId);
        MissionProgressEngine.TryApplyProgress(_state, _testMission, ev1);
        
        Assert.AreEqual(0, _state.CurrentPhaseIndex);

        var ev2 = new MissionContributionEvent(ObjectiveFamily.Interaccion, 1, _profileId, targetId: "chest_silver");
        MissionProgressEngine.TryApplyProgress(_state, _testMission, ev2);

        // Both objectives met, should advance phase
        Assert.AreEqual(1, _state.CurrentPhaseIndex);
        Assert.AreEqual(0, _state.ObjectiveProgress.Count); // Progress cleared for new phase
    }

    [Test]
    public void ApplyProgress_CompletesLastPhase_TransitionsToPendienteDeReclamar()
    {
        // Complete Phase 0
        MissionProgressEngine.TryApplyProgress(_state, _testMission, new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 5, _profileId));
        MissionProgressEngine.TryApplyProgress(_state, _testMission, new MissionContributionEvent(ObjectiveFamily.Interaccion, 1, _profileId, targetId: "chest_silver"));

        Assert.AreEqual(1, _state.CurrentPhaseIndex);

        // Complete Phase 1
        var ev3 = new MissionContributionEvent(ObjectiveFamily.Exploracion, 1, _profileId, zoneId: "boss_room");
        MissionProgressEngine.TryApplyProgress(_state, _testMission, ev3);

        Assert.AreEqual(MissionState.PendienteDeReclamar, _state.State);
    }

    [Test]
    public void ApplyProgress_ZoneFilter_RejectsMismatchedZone()
    {
        // Move to Phase 1
        MissionProgressEngine.TryApplyProgress(_state, _testMission, new MissionContributionEvent(ObjectiveFamily.EliminacionPvE, 5, _profileId));
        MissionProgressEngine.TryApplyProgress(_state, _testMission, new MissionContributionEvent(ObjectiveFamily.Interaccion, 1, _profileId, targetId: "chest_silver"));

        var evFail = new MissionContributionEvent(ObjectiveFamily.Exploracion, 1, _profileId, zoneId: "town");
        bool progressedFail = MissionProgressEngine.TryApplyProgress(_state, _testMission, evFail);

        Assert.IsFalse(progressedFail);
        Assert.AreEqual(1, _state.CurrentPhaseIndex);
        
        var evSuccess = new MissionContributionEvent(ObjectiveFamily.Exploracion, 1, _profileId, zoneId: "boss_room");
        MissionProgressEngine.TryApplyProgress(_state, _testMission, evSuccess);

        Assert.AreEqual(MissionState.PendienteDeReclamar, _state.State);
    }
}
