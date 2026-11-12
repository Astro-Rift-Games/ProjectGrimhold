#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// FIX-50 R2: the replacement Host must not re-finalize an outgoing Host whose exit was already
/// resolved (replicated state only), while an abrupt Host disconnect keeps the unresolved path.
/// </summary>
public sealed class HostMigrationRosterPolicyTests
{
    [Test]
    public void ReturnAuthorizedAbortedAbandon_IsDepartedTerminal()
    {
        Assert.AreEqual(
            HistoricHostRosterClass.DepartedTerminal,
            HostMigrationRosterPolicy.ClassifyHistoricHost(
                RaidParticipantState.Aborted,
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed,
                isReturnAuthorized: true));
    }

    [Test]
    public void ReturnAuthorizedExtracted_IsDepartedTerminal()
    {
        Assert.AreEqual(
            HistoricHostRosterClass.DepartedTerminal,
            HostMigrationRosterPolicy.ClassifyHistoricHost(
                RaidParticipantState.Extracted,
                ExpeditionProgressionFinalizationCause.ExtractionConfirmed,
                isReturnAuthorized: true));
    }

    [Test]
    public void ReturnAuthorizedDefeated_IsDepartedTerminal()
    {
        Assert.AreEqual(
            HistoricHostRosterClass.DepartedTerminal,
            HostMigrationRosterPolicy.ClassifyHistoricHost(
                RaidParticipantState.Defeated,
                ExpeditionProgressionFinalizationCause.DefeatConfirmed,
                isReturnAuthorized: true));
    }

    [Test]
    public void AbandonWithoutReturnAuthorization_IsDepartedTerminal()
    {
        // FIX-49: the Host abandoned (corpse generated) and may drop before pressing Volver;
        // the replacement must not re-finalize it as Aborted.
        Assert.AreEqual(
            HistoricHostRosterClass.DepartedTerminal,
            HostMigrationRosterPolicy.ClassifyHistoricHost(
                RaidParticipantState.Aborted,
                ExpeditionProgressionFinalizationCause.VoluntaryAbandonConfirmed,
                isReturnAuthorized: false));
    }

    [Test]
    public void DefeatedWithoutReturnAuthorization_StaysUnresolved()
    {
        Assert.AreEqual(
            HistoricHostRosterClass.Unresolved,
            HostMigrationRosterPolicy.ClassifyHistoricHost(
                RaidParticipantState.Defeated,
                ExpeditionProgressionFinalizationCause.DefeatConfirmed,
                isReturnAuthorized: false));
    }

    [TestCase(RaidParticipantState.Raiding)]
    [TestCase(RaidParticipantState.Defeated)]
    [TestCase(RaidParticipantState.Extracted)]
    public void AbruptDisconnect_StaysUnresolved(RaidParticipantState state)
    {
        Assert.AreEqual(
            HistoricHostRosterClass.Unresolved,
            HostMigrationRosterPolicy.ClassifyHistoricHost(
                state,
                ExpeditionProgressionFinalizationCause.None,
                isReturnAuthorized: false));
    }
}
#endif
