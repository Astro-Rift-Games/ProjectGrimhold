#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// FIX-51: disconnected Raiders and Extracted participants with a pending commit survive the Host
/// handoff. Pure rules only; the multi-process handoff itself is validated manually.
/// </summary>
public sealed class HostMigrationRetentionPolicyTests
{
    private const ExtractionExperienceTransactionPhase NoPhase = ExtractionExperienceTransactionPhase.None;

    // --- Classify: disconnected Raiders (S1, S3) ---

    [Test]
    public void Classify_RaidingDisconnectedWithBudget_Retains()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Retain,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Raiding, false, true, 120, false, NoPhase));
    }

    [Test]
    public void Classify_RaidingDisconnectedBudgetExhausted_KeepsExistingRecoverPath()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Recover,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Raiding, false, true, 0, false, NoPhase));
    }

    [Test]
    public void Classify_RaidingDisconnectedAbsenceConfirmed_KeepsExistingRecoverPath()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Recover,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Raiding, false, true, 120, true, NoPhase));
    }

    [Test]
    public void Classify_RaidingConnected_Recovers()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Recover,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Raiding, false, false, 120, false, NoPhase));
    }

    [Test]
    public void Classify_ReturnAuthorizedRaiding_Finalizes()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Finalize,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Raiding, true, true, 120, false, NoPhase));
    }

    [Test]
    public void Classify_DefeatedWithStaleConnectivity_NeverRetained()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Recover,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Defeated, false, true, 120, false, NoPhase));
    }

    [Test]
    public void Classify_Aborted_Finalizes()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Finalize,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Aborted, false, false, 0, false, NoPhase));
    }

    // --- Classify: Extracted with pending commit (S2, S4) ---

    [TestCase(ExtractionExperienceTransactionPhase.None)]
    [TestCase(ExtractionExperienceTransactionPhase.AwaitingExperiencePreparation)]
    [TestCase(ExtractionExperienceTransactionPhase.AwaitingPersistenceAck)]
    [TestCase(ExtractionExperienceTransactionPhase.ExtractedLootPending)]
    [TestCase(ExtractionExperienceTransactionPhase.ProgressionPending)]
    public void Classify_ExtractedPendingCommit_Recovers(ExtractionExperienceTransactionPhase phase)
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Recover,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Extracted, false, false, 0, false, phase));
    }

    [Test]
    public void Classify_ExtractedCommitComplete_StaysTerminal()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Finalize,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Extracted, false, false, 0, false,
                ExtractionExperienceTransactionPhase.Complete));
    }

    [Test]
    public void Classify_ExtractedPendingButReturnAuthorized_Finalizes()
    {
        Assert.AreEqual(
            HostMigrationParticipantDisposition.Finalize,
            HostMigrationRetentionPolicy.Classify(
                RaidParticipantState.Extracted, true, false, 0, false,
                ExtractionExperienceTransactionPhase.AwaitingPersistenceAck));
    }

    [TestCase(RaidParticipantState.Extracted, ExtractionExperienceTransactionPhase.AwaitingPersistenceAck, true)]
    [TestCase(RaidParticipantState.Extracted, ExtractionExperienceTransactionPhase.Complete, false)]
    [TestCase(RaidParticipantState.Raiding, ExtractionExperienceTransactionPhase.None, false)]
    [TestCase(RaidParticipantState.Defeated, ExtractionExperienceTransactionPhase.None, false)]
    public void IsExtractionCommitPending_OnlyExtractedBeforeComplete(
        RaidParticipantState state,
        ExtractionExperienceTransactionPhase phase,
        bool expected)
    {
        Assert.AreEqual(expected, HostMigrationRetentionPolicy.IsExtractionCommitPending(state, phase));
    }

    // --- Eligibility seam used by InitializeHostMigrationRoster ---

    [Test]
    public void Eligibility_ExtractedPendingCommit_IsEligible()
    {
        Assert.IsTrue(NetworkSpawnManager.IsHostMigrationRecoveryEligible(
            new ProfileId("survivor"), new ProfileId("old-host"),
            RaidParticipantState.Extracted,
            isReturnAuthorized: false,
            terminalKnown: false,
            extractionCommitPending: true));
    }

    [Test]
    public void Eligibility_ExtractedCompleteCommit_StaysIneligible()
    {
        Assert.IsFalse(NetworkSpawnManager.IsHostMigrationRecoveryEligible(
            new ProfileId("survivor"), new ProfileId("old-host"),
            RaidParticipantState.Extracted,
            isReturnAuthorized: false,
            terminalKnown: true,
            extractionCommitPending: false));
    }

    [Test]
    public void Eligibility_ExtractedPendingOfOldHost_IsIneligible()
    {
        Assert.IsFalse(NetworkSpawnManager.IsHostMigrationRecoveryEligible(
            new ProfileId("old-host"), new ProfileId("old-host"),
            RaidParticipantState.Extracted,
            isReturnAuthorized: false,
            terminalKnown: false,
            extractionCommitPending: true));
    }

    // --- Recovered-state validation at seal and on the client (S2) ---

    [TestCase(RaidParticipantState.Raiding, false, true)]
    [TestCase(RaidParticipantState.Defeated, false, true)]
    [TestCase(RaidParticipantState.Extracted, false, true)]
    [TestCase(RaidParticipantState.Extracted, true, false)]
    [TestCase(RaidParticipantState.Aborted, false, false)]
    [TestCase(RaidParticipantState.Raiding, true, false)]
    public void IsRecoveredStateValid_AcceptsExtractedRecoveredParticipant(
        RaidParticipantState state,
        bool isReturnAuthorized,
        bool expected)
    {
        Assert.AreEqual(expected, HostMigrationRetentionPolicy.IsRecoveredStateValid(state, isReturnAuthorized));
    }

    // --- Unrecovered at seal ---

    [Test]
    public void UnrecoveredAtSeal_ExtractedPending_KeepsObjectsInsteadOfDespawning()
    {
        Assert.AreEqual(
            HostMigrationUnrecoveredSealAction.KeepObjectsTerminal,
            HostMigrationRetentionPolicy.ClassifyUnrecoveredAtSeal(
                RaidParticipantState.Extracted,
                ExtractionExperienceTransactionPhase.AwaitingPersistenceAck,
                isHistoricHost: false));
    }

    [Test]
    public void UnrecoveredAtSeal_HistoricHostExtractedPending_KeepsExistingFinalization()
    {
        Assert.AreEqual(
            HostMigrationUnrecoveredSealAction.Finalize,
            HostMigrationRetentionPolicy.ClassifyUnrecoveredAtSeal(
                RaidParticipantState.Extracted,
                ExtractionExperienceTransactionPhase.AwaitingPersistenceAck,
                isHistoricHost: true));
    }

    [TestCase(RaidParticipantState.Raiding)]
    [TestCase(RaidParticipantState.Defeated)]
    public void UnrecoveredAtSeal_RaidingAndDefeated_KeepExistingFinalization(RaidParticipantState state)
    {
        Assert.AreEqual(
            HostMigrationUnrecoveredSealAction.Finalize,
            HostMigrationRetentionPolicy.ClassifyUnrecoveredAtSeal(
                state, ExtractionExperienceTransactionPhase.None, isHistoricHost: false));
    }

    // --- Reconnect budget is paused while the recovery window is unsealed (S3) ---

    [Test]
    public void ConsumeTick_PausedDuringRecoveryWindow_KeepsBudget()
    {
        Assert.AreEqual(
            120,
            ReconnectBudgetRules.ConsumeTick(
                120, isDisconnected: true, RaidParticipantState.Raiding, isBudgetPaused: true));
    }

    [Test]
    public void ConsumeTick_AfterSeal_ResumesConsumingFromRemainingBudget()
    {
        int budget = 120;
        budget = ReconnectBudgetRules.ConsumeTick(
            budget, true, RaidParticipantState.Raiding, isBudgetPaused: true);
        budget = ReconnectBudgetRules.ConsumeTick(
            budget, true, RaidParticipantState.Raiding, isBudgetPaused: false);
        Assert.AreEqual(119, budget);
    }

    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    public void IsReconnectBudgetPaused_OnlyForUnsealedMigrationResume(
        bool isMigrationResume,
        bool isRecoverySealed,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            HostMigrationRetentionPolicy.IsReconnectBudgetPaused(isMigrationResume, isRecoverySealed));
    }

    // --- After seal, retained participants reconnect through the normal path (S1) ---

    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    public void UsesMigrationArrivalBoundary_EndsWhenRecoveryIsSealed(
        bool isMigrationResume,
        bool isRecoverySealed,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            HostMigrationRetentionPolicy.UsesMigrationArrivalBoundary(isMigrationResume, isRecoverySealed));
    }
}
#endif
