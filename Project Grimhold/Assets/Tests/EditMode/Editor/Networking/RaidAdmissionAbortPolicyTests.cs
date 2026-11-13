#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using MatchPhase = NetworkMatchController.MatchPhase;

/// <summary>FIX-03: when the Raid Host aborts a Waiting admission (S6, S8, S10).</summary>
public sealed class RaidAdmissionAbortPolicyTests
{
    private static readonly ProfileId HostProfile = new ProfileId("host");
    private static readonly ProfileId Member = new ProfileId("member");
    private static readonly ProfileId Outsider = new ProfileId("outsider");

    private static readonly ProfileId[] Cohort = { HostProfile, Member };

    [Test]
    public void RequiredMemberLost_AbortsImmediatelyWithoutWaitingForTheDeadline()
    {
        RaidAdmissionAbortDecision decision = RaidAdmissionAbortPolicy.Evaluate(
            MatchPhase.WaitingForPlayers, cohortAdmitted: false, deadlineExpired: false, requiredMemberLost: true);

        Assert.That(decision, Is.EqualTo(RaidAdmissionAbortDecision.RequiredMemberLost));
    }

    [Test]
    public void NeverJoinedMember_KeepsWaitingUntilTheDeadline()
    {
        // A member that has not joined yet is still inside its availability budget: no loss is recorded.
        RaidAdmissionAbortDecision decision = RaidAdmissionAbortPolicy.Evaluate(
            MatchPhase.WaitingForPlayers, cohortAdmitted: false, deadlineExpired: false, requiredMemberLost: false);

        Assert.That(decision, Is.EqualTo(RaidAdmissionAbortDecision.None));
    }

    [Test]
    public void DeadlineExpired_AbortsWhenTheCohortIsIncomplete()
    {
        RaidAdmissionAbortDecision decision = RaidAdmissionAbortPolicy.Evaluate(
            MatchPhase.WaitingForPlayers, cohortAdmitted: false, deadlineExpired: true, requiredMemberLost: false);

        Assert.That(decision, Is.EqualTo(RaidAdmissionAbortDecision.DeadlineExpired));
    }

    [Test]
    public void DeadlineExpired_WithTheCohortAdmitted_DoesNotAbort()
    {
        RaidAdmissionAbortDecision decision = RaidAdmissionAbortPolicy.Evaluate(
            MatchPhase.WaitingForPlayers, cohortAdmitted: true, deadlineExpired: true, requiredMemberLost: false);

        Assert.That(decision, Is.EqualTo(RaidAdmissionAbortDecision.None));
    }

    [Test]
    public void RequiredMemberLost_WithTheRemainingCohortAdmitted_StillAborts()
    {
        // S10: the remaining members must never start as a smaller expedition.
        RaidAdmissionAbortDecision decision = RaidAdmissionAbortPolicy.Evaluate(
            MatchPhase.WaitingForPlayers, cohortAdmitted: true, deadlineExpired: false, requiredMemberLost: true);

        Assert.That(decision, Is.EqualTo(RaidAdmissionAbortDecision.RequiredMemberLost));
    }

    [Test]
    public void RequiredMemberLost_PrecedesTheDeadlineWhenBothApply()
    {
        RaidAdmissionAbortDecision decision = RaidAdmissionAbortPolicy.Evaluate(
            MatchPhase.WaitingForPlayers, cohortAdmitted: false, deadlineExpired: true, requiredMemberLost: true);

        Assert.That(decision, Is.EqualTo(RaidAdmissionAbortDecision.RequiredMemberLost));
    }

    [TestCase(MatchPhase.Starting)]
    [TestCase(MatchPhase.InProgress)]
    [TestCase(MatchPhase.Closing)]
    [TestCase(MatchPhase.Finished)]
    public void AfterAdmissionClosed_NeverAborts(MatchPhase phase)
    {
        RaidAdmissionAbortDecision decision = RaidAdmissionAbortPolicy.Evaluate(
            phase, cohortAdmitted: false, deadlineExpired: true, requiredMemberLost: true);

        Assert.That(decision, Is.EqualTo(RaidAdmissionAbortDecision.None));
    }

    [Test]
    public void DepartedCohortMember_IsARequiredMemberLoss()
    {
        Assert.That(RaidAdmissionAbortPolicy.IsRequiredMemberLoss(Cohort, HostProfile, Member), Is.True);
    }

    [Test]
    public void TrioCohort_SecondMemberDeparture_IsARequiredMemberLoss()
    {
        var secondMember = new ProfileId("second-member");
        ProfileId[] trio = { HostProfile, Member, secondMember };

        Assert.That(RaidAdmissionAbortPolicy.IsRequiredMemberLoss(trio, HostProfile, secondMember), Is.True);
        Assert.That(RaidAdmissionAbortPolicy.IsRequiredMemberLoss(trio, HostProfile, Member), Is.True);
    }

    [Test]
    public void CaseDifferentProfile_IsNotAMemberLoss()
    {
        // ProfileId equality is an ordinal string comparison (ids are opaque and case-sensitive), so a
        // differently-cased id is a different profile and never maps onto the frozen cohort.
        Assert.That(new ProfileId("MEMBER") == Member, Is.False);
        Assert.That(
            RaidAdmissionAbortPolicy.IsRequiredMemberLoss(Cohort, HostProfile, new ProfileId("MEMBER")),
            Is.False);
    }

    [Test]
    public void DepartedHostProfile_IsNotAMemberLoss()
    {
        // The Host's own departure ends the session; it is not a remote member failing to enter.
        Assert.That(RaidAdmissionAbortPolicy.IsRequiredMemberLoss(Cohort, HostProfile, HostProfile), Is.False);
    }

    [Test]
    public void DepartedProfileOutsideTheCohort_IsNotAMemberLoss()
    {
        Assert.That(RaidAdmissionAbortPolicy.IsRequiredMemberLoss(Cohort, HostProfile, Outsider), Is.False);
    }

    [Test]
    public void UnmappedDeparture_IsNotAMemberLoss()
    {
        Assert.That(RaidAdmissionAbortPolicy.IsRequiredMemberLoss(Cohort, HostProfile, default), Is.False);
        Assert.That(RaidAdmissionAbortPolicy.IsRequiredMemberLoss(null, HostProfile, Member), Is.False);
    }
}
#endif
