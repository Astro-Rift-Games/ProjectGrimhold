using NUnit.Framework;

public sealed class RaidPlayerDepartureTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void RaidingWithValidConfig_IsRetainedRegardlessOfDowned(bool downed)
    {
        Assert.That(RaidPlayerDeparturePolicy.EvaluateDeparture(
            RaidParticipantState.Raiding, downed, configValid: true),
            Is.EqualTo(PlayerDepartureAction.RetainDisconnected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidConfig_UsesDefinitiveDisconnectFailSafe(bool downed)
    {
        Assert.That(RaidPlayerDeparturePolicy.EvaluateDeparture(
            RaidParticipantState.Raiding, downed, configValid: false),
            Is.EqualTo(PlayerDepartureAction.FinalizeDefinitiveDisconnect));
    }

    [TestCase(RaidParticipantState.Extracted, PlayerDepartureAction.FinalizeExtracted)]
    [TestCase(RaidParticipantState.Defeated, PlayerDepartureAction.FinalizeDefeated)]
    [TestCase(RaidParticipantState.Aborted, PlayerDepartureAction.FinalizeAborted)]
    public void TerminalParticipant_KeepsItsResult(RaidParticipantState state, PlayerDepartureAction expected)
    {
        foreach (bool validConfig in new[] { false, true })
        foreach (bool downed in new[] { false, true })
            Assert.That(RaidPlayerDeparturePolicy.EvaluateDeparture(state, downed, validConfig), Is.EqualTo(expected));
    }

    [TestCase(RaidParticipantState.Raiding, false)]
    [TestCase(RaidParticipantState.Defeated, true)]
    [TestCase(RaidParticipantState.Extracted, true)]
    [TestCase(RaidParticipantState.Aborted, true)]
    public void RetainedDownedRaider_IsResolvedOnceItLeavesRaiding(RaidParticipantState state, bool expected)
    {
        Assert.That(RaidPlayerDeparturePolicy.IsRetainedDownedRaiderResolved(state), Is.EqualTo(expected));
    }
}
