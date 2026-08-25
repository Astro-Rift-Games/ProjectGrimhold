using NUnit.Framework;

public sealed class RaidParticipantIdTests
{
    [Test]
    public void Assignment_IsOrdinalStableAndIndependentOfCohortOrder()
    {
        ProfileId alpha = new("arbitrary-profile-alpha");
        ProfileId middle = new("profile-middle");
        ProfileId omega = new("zz-profile");
        ProfileId[] first = { omega, alpha, middle };
        ProfileId[] second = { middle, omega, alpha };

        Assert.That(RaidParticipantIdAssignment.TryResolve(first, alpha, out RaidParticipantId alphaId), Is.True);
        Assert.That(RaidParticipantIdAssignment.TryResolve(first, middle, out RaidParticipantId middleId), Is.True);
        Assert.That(RaidParticipantIdAssignment.TryResolve(first, omega, out RaidParticipantId omegaId), Is.True);
        Assert.That(RaidParticipantIdAssignment.TryResolve(second, alpha, out RaidParticipantId restoredAlpha), Is.True);
        Assert.That(RaidParticipantIdAssignment.TryResolve(second, middle, out RaidParticipantId restoredMiddle), Is.True);
        Assert.That(RaidParticipantIdAssignment.TryResolve(second, omega, out RaidParticipantId restoredOmega), Is.True);

        Assert.That(new[] { alphaId.Value, middleId.Value, omegaId.Value }, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(restoredAlpha, Is.EqualTo(alphaId));
        Assert.That(restoredMiddle, Is.EqualTo(middleId));
        Assert.That(restoredOmega, Is.EqualTo(omegaId));
    }

    [Test]
    public void Assignment_RejectsMissingDuplicateAndOutOfRangeValues()
    {
        ProfileId profile = new("any-nonempty-profile");
        Assert.That(RaidParticipantIdAssignment.TryResolve(new[] { profile, profile }, profile, out _), Is.False);
        Assert.That(RaidParticipantIdAssignment.TryResolve(new[] { profile }, new ProfileId("missing"), out _), Is.False);
        Assert.That(RaidParticipantId.TryCreate(0, out _), Is.False);
        Assert.That(RaidParticipantId.TryCreate(16, out RaidParticipantId maximum), Is.True);
        Assert.That(maximum.Value, Is.EqualTo(16));
        Assert.That(RaidParticipantId.TryCreate(17, out _), Is.False);
    }

    [Test]
    public void ReconnectResolution_ReusesSameIdForSameFrozenCohort()
    {
        ProfileId profile = new("profile-with-any-valid-shape");
        ProfileId[] cohort = { new ProfileId("zeta"), profile, new ProfileId("alpha") };

        Assert.That(RaidParticipantIdAssignment.TryResolve(cohort, profile, out RaidParticipantId admitted), Is.True);
        Assert.That(RaidParticipantIdAssignment.TryResolve(cohort, profile, out RaidParticipantId reconnect), Is.True);

        Assert.That(reconnect, Is.EqualTo(admitted));
    }
}
