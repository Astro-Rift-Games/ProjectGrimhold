#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

[Category("TASK214")]
public sealed class QuickSlotReleaseGateTests
{
    [Test]
    public void ActiveRaidingParticipant_MayRelease()
    {
        Assert.That(Gate(), Is.True);
    }

    [Test]
    public void DeadOrDownedAvatar_KeepsBindings()
    {
        Assert.That(Gate(isAlive: false), Is.False);
        Assert.That(Gate(isDowned: true), Is.False);
    }

    [Test]
    public void ExtractionInProgressOrMatchNotRunning_KeepsBindings()
    {
        Assert.That(Gate(isExtractionIdle: false), Is.False);
        Assert.That(Gate(isMatchInProgress: false), Is.False);
    }

    [TestCase(RaidParticipantState.Extracted)]
    [TestCase(RaidParticipantState.Defeated)]
    [TestCase(RaidParticipantState.Aborted)]
    public void EndedParticipant_KeepsBindings(RaidParticipantState state)
    {
        Assert.That(Gate(state: state), Is.False);
    }

    [TestCase(ExpeditionProgressionFinalizationCause.ExtractionConfirmed)]
    [TestCase(ExpeditionProgressionFinalizationCause.DefeatConfirmed)]
    [TestCase(ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed)]
    [TestCase(ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed)]
    public void FinalizedExpedition_KeepsBindingsEvenIfTheAvatarIsStillAlive(
        ExpeditionProgressionFinalizationCause cause)
    {
        // Voluntary abandon empties the inventory of a living, standing avatar.
        Assert.That(Gate(cause: cause), Is.False);
    }

    private static bool Gate(
        bool isAlive = true,
        bool isDowned = false,
        bool isExtractionIdle = true,
        bool isMatchInProgress = true,
        RaidParticipantState state = RaidParticipantState.Raiding,
        ExpeditionProgressionFinalizationCause cause = ExpeditionProgressionFinalizationCause.None) =>
        QuickSlotReleaseGate.CanRelease(
            isAlive, isDowned, isExtractionIdle, isMatchInProgress, state, cause);
}
#endif
