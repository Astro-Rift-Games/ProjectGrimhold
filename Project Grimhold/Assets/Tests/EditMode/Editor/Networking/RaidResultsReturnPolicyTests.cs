using NUnit.Framework;

public sealed class RaidResultsReturnPolicyTests
{
    [Test]
    public void RaidingParticipant_IsRejectedEvenWhenAllOtherClientBarriersPass()
    {
        Assert.That(
            RaidResultsReturnPolicy.CanRequestClientReturn(
                RaidParticipantState.Raiding,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed,
                isExtractionProgressionComplete: true,
                hasProgressionResultSnapshot: true,
                isProgressionCommitConfirmed: true,
                isCompatiblePhase: true),
            Is.False);
    }

    [TestCase(RaidParticipantState.Defeated, ExpeditionProgressionFinalizationCause.DefeatConfirmed)]
    [TestCase(RaidParticipantState.Extracted, ExpeditionProgressionFinalizationCause.ExtractionConfirmed)]
    [TestCase(RaidParticipantState.Aborted, ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed)]
    public void AcceptedTerminalParticipant_CanRequestClientReturn(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause cause)
    {
        Assert.That(
            RaidResultsReturnPolicy.CanRequestClientReturn(
                state,
                cause,
                isExtractionProgressionComplete: true,
                hasProgressionResultSnapshot: true,
                isProgressionCommitConfirmed: true,
                isCompatiblePhase: true),
            Is.True);
    }

    [TestCase(RaidParticipantState.Aborted, ExpeditionProgressionFinalizationCause.DefinitiveDisconnectConfirmed)]
    [TestCase(RaidParticipantState.Aborted, ExpeditionProgressionFinalizationCause.None)]
    [TestCase(RaidParticipantState.Extracted, ExpeditionProgressionFinalizationCause.ExtractionConfirmed)]
    public void IncompleteOrUnsupportedTerminalParticipant_IsRejected(
        RaidParticipantState state,
        ExpeditionProgressionFinalizationCause cause)
    {
        Assert.That(
            RaidResultsReturnPolicy.CanRequestClientReturn(
                state,
                cause,
                isExtractionProgressionComplete: state != RaidParticipantState.Extracted,
                hasProgressionResultSnapshot: true,
                isProgressionCommitConfirmed: true,
                isCompatiblePhase: true),
            Is.False);
    }

    [Test]
    public void FinishedWithoutExplicitHostRequest_NeverStartsReturn()
    {
        Assert.That(
            CanStartHostReturn(
                hostReturnRequested: false,
                hostReturnStarted: false,
                hasConnectedRemoteParticipants: false),
            Is.False);
    }

    [Test]
    public void HostRequest_WithConnectedClient_RemainsPendingAfterTerminalObjectCleanup()
    {
        bool terminalClientObjectsRemain = false;
        bool peerStillBelongsToRunner = true;

        Assert.That(terminalClientObjectsRemain, Is.False);
        Assert.That(
            CanStartHostReturn(
                hostReturnRequested: true,
                hostReturnStarted: false,
                hasConnectedRemoteParticipants: peerStillBelongsToRunner),
            Is.False);
    }

    [Test]
    public void HostRequest_StartsOnceAfterOnPlayerLeftConfirmsLastClientDeparture()
    {
        bool peerStillBelongsToRunner = true;
        Assert.That(CanStartHostReturn(true, false, peerStillBelongsToRunner), Is.False);

        peerStillBelongsToRunner = false;
        Assert.That(CanStartHostReturn(true, false, peerStillBelongsToRunner), Is.True);

        bool hostReturnStarted = true;
        Assert.That(CanStartHostReturn(true, hostReturnStarted, peerStillBelongsToRunner), Is.False);
    }

    [Test]
    public void HostEligibility_RequiresTerminalLocalParticipantAndFinishedRaidWithoutRaiders()
    {
        Assert.That(
            RaidResultsReturnPolicy.CanRequestHostReturn(
                RaidParticipantState.Defeated,
                ExpeditionProgressionFinalizationCause.DefeatConfirmed,
                isExtractionProgressionComplete: true,
                hasProgressionResultSnapshot: true,
                isProgressionCommitConfirmed: true,
                isServer: true,
                hasRaidingParticipants: false,
                isMatchFinished: true),
            Is.True);

        Assert.That(
            RaidResultsReturnPolicy.CanRequestHostReturn(
                RaidParticipantState.Raiding,
                ExpeditionProgressionFinalizationCause.DefeatConfirmed,
                isExtractionProgressionComplete: true,
                hasProgressionResultSnapshot: true,
                isProgressionCommitConfirmed: true,
                isServer: true,
                hasRaidingParticipants: false,
                isMatchFinished: true),
            Is.False);
    }

    [Test]
    public void CancelledHostParticipant_IsNeverEligibleThroughTheResultsGate()
    {
        Assert.That(
            RaidResultsReturnPolicy.CanRequestHostReturn(
                RaidParticipantState.Aborted,
                ExpeditionProgressionFinalizationCause.None,
                isExtractionProgressionComplete: false,
                hasProgressionResultSnapshot: true,
                isProgressionCommitConfirmed: true,
                isServer: true,
                hasRaidingParticipants: false,
                isMatchFinished: true),
            Is.False);
    }

    [TestCase(NetworkMatchController.MatchPhase.Closing)]
    [TestCase(NetworkMatchController.MatchPhase.Finished)]
    public void HostCancellationClosure_ArmsThePendingGlobalReturn(
        NetworkMatchController.MatchPhase phase)
    {
        Assert.That(CanArmHostCancellationReturn(phase: phase), Is.True);
    }

    [TestCase(RaidClosureReason.NaturalCompletion)]
    [TestCase(RaidClosureReason.BootstrapFailure)]
    public void OtherClosureReasons_NeverArmThePendingGlobalReturn(RaidClosureReason reason)
    {
        Assert.That(CanArmHostCancellationReturn(closureReason: reason), Is.False);
    }

    [Test]
    public void HostCancellationClosure_DoesNotArmBeforeClosureOrOutsideTheRaid()
    {
        Assert.That(
            CanArmHostCancellationReturn(phase: NetworkMatchController.MatchPhase.InProgress),
            Is.False);
        Assert.That(
            CanArmHostCancellationReturn(state: SessionConnectionState.ReturningTown),
            Is.False);
        Assert.That(CanArmHostCancellationReturn(hasValidHostRunner: false), Is.False);
        Assert.That(CanArmHostCancellationReturn(operationActive: true), Is.False);
    }

    [Test]
    public void HostCancellationClosure_ArmsExactlyOnceAcrossFrames()
    {
        Assert.That(CanArmHostCancellationReturn(), Is.True);

        // The armed latch survives the frames the closure takes to reach Finished and the
        // frames the Host waits for the last remote peer to leave.
        Assert.That(CanArmHostCancellationReturn(cancellationReturnArmed: true), Is.False);
        Assert.That(CanArmHostCancellationReturn(hostReturnRequested: true), Is.False);
        Assert.That(CanArmHostCancellationReturn(hostReturnStarted: true), Is.False);
    }

    [Test]
    public void ArmedHostCancellation_StartsOnlyAfterTheLastRemotePeerLeaves()
    {
        Assert.That(
            CanStartHostReturn(
                hostReturnRequested: true,
                hostReturnStarted: false,
                hasConnectedRemoteParticipants: true),
            Is.False);
        Assert.That(
            CanStartHostReturn(
                hostReturnRequested: true,
                hostReturnStarted: false,
                hasConnectedRemoteParticipants: false),
            Is.True);
    }

    private static bool CanArmHostCancellationReturn(
        bool cancellationReturnArmed = false,
        bool hostReturnRequested = false,
        bool hostReturnStarted = false,
        bool operationActive = false,
        SessionConnectionState state = SessionConnectionState.Raid,
        bool hasValidHostRunner = true,
        RaidClosureReason closureReason = RaidClosureReason.HostCancellation,
        NetworkMatchController.MatchPhase phase = NetworkMatchController.MatchPhase.Closing)
    {
        return SessionConnectionCoordinator.ShouldArmHostCancellationReturn(
            cancellationReturnArmed,
            hostReturnRequested,
            hostReturnStarted,
            operationActive,
            state,
            hasValidHostRunner,
            closureReason,
            phase);
    }

    private static bool CanStartHostReturn(
        bool hostReturnRequested,
        bool hostReturnStarted,
        bool hasConnectedRemoteParticipants)
    {
        return RaidResultsReturnPolicy.ShouldStartHostReturn(
            hostReturnRequested,
            hostReturnStarted,
            operationActive: false,
            coordinatorIsInRaid: true,
            hasValidHostRunner: true,
            isMatchFinished: true,
            hasRaidingParticipants: false,
            hasConnectedRemoteParticipants);
    }
}
