using NUnit.Framework;
using UnityEngine;

public sealed class RaidConnectivityContinuityTests
{
    [Test]
    public void ReconnectBudgetConfig_ValidatesDuration()
    {
        var config = ScriptableObject.CreateInstance<ReconnectBudgetConfig>();
        var serializedObject = new UnityEditor.SerializedObject(config);
        
        serializedObject.FindProperty("_durationSeconds").floatValue = 120f;
        serializedObject.ApplyModifiedProperties();
        Assert.That(config.TryValidate(out string error), Is.True);
        Assert.That(error, Is.Null);

        serializedObject.FindProperty("_durationSeconds").floatValue = -1f;
        serializedObject.ApplyModifiedProperties();
        Assert.That(config.TryValidate(out error), Is.False);
        Assert.That(error, Does.Contain("greater than zero"));
        
        serializedObject.FindProperty("_durationSeconds").floatValue = 0f;
        serializedObject.ApplyModifiedProperties();
        Assert.That(config.TryValidate(out error), Is.False);
    }

    [Test]
    public void ReconnectBudgetRules_CalculateInitialTicks()
    {
        Assert.That(ReconnectBudgetRules.CalculateInitialTicks(120f, 60), Is.EqualTo(7200));
        Assert.That(ReconnectBudgetRules.CalculateInitialTicks(-5f, 60), Is.EqualTo(0));
        Assert.That(ReconnectBudgetRules.CalculateInitialTicks(10f, 0), Is.EqualTo(0));
    }

    [Test]
    public void ReconnectBudgetRules_ConsumeTick_AccumulativeAndSticky()
    {
        int current = 5;
        
        // Connect/disconnect scenarios
        current = ReconnectBudgetRules.ConsumeTick(current, isDisconnected: false, RaidParticipantState.Raiding);
        Assert.That(current, Is.EqualTo(5), "Does not consume while connected");

        current = ReconnectBudgetRules.ConsumeTick(current, isDisconnected: true, RaidParticipantState.Raiding);
        Assert.That(current, Is.EqualTo(4), "Consumes while disconnected and raiding");

        current = ReconnectBudgetRules.ConsumeTick(current, isDisconnected: true, RaidParticipantState.Defeated);
        Assert.That(current, Is.EqualTo(4), "Does not consume while defeated");

        // Sticky at 0
        current = 1;
        current = ReconnectBudgetRules.ConsumeTick(current, isDisconnected: true, RaidParticipantState.Raiding);
        Assert.That(current, Is.EqualTo(0));
        
        current = ReconnectBudgetRules.ConsumeTick(current, isDisconnected: true, RaidParticipantState.Raiding);
        Assert.That(current, Is.EqualTo(0), "Sticky at 0");
    }

    [TestCase(RaidParticipantState.Extracted, false, true, true, PlayerDepartureAction.FinalizeExtracted)]
    [TestCase(RaidParticipantState.Defeated, false, true, true, PlayerDepartureAction.FinalizeDefeated)]
    [TestCase(RaidParticipantState.Aborted, false, true, true, PlayerDepartureAction.FinalizeAborted)]
    [TestCase(RaidParticipantState.Raiding, false, true, true, PlayerDepartureAction.RetainDisconnected)]
    [TestCase(RaidParticipantState.Raiding, true, true, true, PlayerDepartureAction.RetainDisconnected)]
    [TestCase(RaidParticipantState.Raiding, false, false, true, PlayerDepartureAction.FinalizeDefinitiveDisconnect)]
    [TestCase(RaidParticipantState.Raiding, false, true, false, PlayerDepartureAction.FinalizeDefinitiveDisconnect)]
    public void DeparturePolicy_EvaluatesCorrectAction(
        RaidParticipantState state,
        bool avatarIsDowned,
        bool hasDecider,
        bool configValid,
        PlayerDepartureAction expected)
    {
        Assert.That(
            RaidPlayerDeparturePolicy.EvaluateDeparture(state, avatarIsDowned, hasDecider, configValid),
            Is.EqualTo(expected));
    }

    [TestCase(RaidParticipantState.Raiding, true)]
    [TestCase(RaidParticipantState.Extracted, false)]
    [TestCase(RaidParticipantState.Defeated, false)]
    [TestCase(RaidParticipantState.Aborted, false)]
    public void RaidConnectivitySnapshot_ActiveOrRecoverableMember(RaidParticipantState state, bool expected)
    {
        var snapshot = new RaidConnectivitySnapshot(false, 100, state);
        Assert.That(snapshot.IsActiveOrRecoverableMember, Is.EqualTo(expected));
    }

    [Test]
    public void DecisionRules_EvaluatesAllRejections()
    {
        // 1. RejectedAlreadyResolved
        Assert.That(
            RaidTeammateAbsenceDecisionRules.Evaluate(
                new RaidConnectivitySnapshot(true, 0, RaidParticipantState.Defeated),
                isTeammate: true, isConfirmerConnected: true, RaidParticipantState.Raiding),
            Is.EqualTo(TeammateAbsenceDecisionResult.RejectedAlreadyResolved));

        // 2. RejectedNotTeammate
        Assert.That(
            RaidTeammateAbsenceDecisionRules.Evaluate(
                new RaidConnectivitySnapshot(true, 0, RaidParticipantState.Raiding),
                isTeammate: false, isConfirmerConnected: true, RaidParticipantState.Raiding),
            Is.EqualTo(TeammateAbsenceDecisionResult.RejectedNotTeammate));

        // 3. RejectedConfirmerUnavailable
        Assert.That(
            RaidTeammateAbsenceDecisionRules.Evaluate(
                new RaidConnectivitySnapshot(true, 0, RaidParticipantState.Raiding),
                isTeammate: true, isConfirmerConnected: false, RaidParticipantState.Raiding),
            Is.EqualTo(TeammateAbsenceDecisionResult.RejectedConfirmerUnavailable));
            
        Assert.That(
            RaidTeammateAbsenceDecisionRules.Evaluate(
                new RaidConnectivitySnapshot(true, 0, RaidParticipantState.Raiding),
                isTeammate: true, isConfirmerConnected: true, RaidParticipantState.Defeated),
            Is.EqualTo(TeammateAbsenceDecisionResult.RejectedConfirmerUnavailable));

        // 4. RejectedReconnected
        Assert.That(
            RaidTeammateAbsenceDecisionRules.Evaluate(
                new RaidConnectivitySnapshot(false, 0, RaidParticipantState.Raiding),
                isTeammate: true, isConfirmerConnected: true, RaidParticipantState.Raiding),
            Is.EqualTo(TeammateAbsenceDecisionResult.RejectedReconnected));

        // 5. RejectedBudgetNotExhausted
        Assert.That(
            RaidTeammateAbsenceDecisionRules.Evaluate(
                new RaidConnectivitySnapshot(true, 10, RaidParticipantState.Raiding),
                isTeammate: true, isConfirmerConnected: true, RaidParticipantState.Raiding),
            Is.EqualTo(TeammateAbsenceDecisionResult.RejectedBudgetNotExhausted));

        // 6. Confirmable
        Assert.That(
            RaidTeammateAbsenceDecisionRules.Evaluate(
                new RaidConnectivitySnapshot(true, 0, RaidParticipantState.Raiding),
                isTeammate: true, isConfirmerConnected: true, RaidParticipantState.Raiding),
            Is.EqualTo(TeammateAbsenceDecisionResult.Confirmable));
    }
}
