using NUnit.Framework;

public sealed class RecoveryWeaponLaunchConsentTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(16)]
    public void LaunchCannotFreezeBeforeEveryMemberConsents(int count)
    {
        for (int accepted = 0; accepted < count; accepted++)
            Assert.That(RecoveryWeaponPreparationLaunchRules.CanFreeze(3, 3, count, accepted), Is.False);
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanFreeze(3, 3, count, count), Is.True);
    }

    [Test]
    public void RosterChange_InvalidatesEvenPreviouslyUnanimousConsent()
    {
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanFreeze(3, 4, 2, 2), Is.False);
    }

    [Test]
    public void CancelledAttemptAndLateResponses_CannotAuthorizeLaunch()
    {
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanAcceptResponse(false, 4, 4, true), Is.False);
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanAcceptResponse(true, 5, 4, true), Is.False);
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanAcceptResponse(true, 5, 5, false), Is.False);
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanAcceptResponse(true, 5, 5, true), Is.True);
    }

    [Test]
    public void RepeatedResponseIsIdempotentButCannotCreateMissingMemberConsent()
    {
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanAcceptResponse(true, 5, 5, true), Is.True);
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanAcceptResponse(true, 5, 5, true), Is.True);
        Assert.That(RecoveryWeaponPreparationLaunchRules.CanFreeze(3, 3, 2, 1), Is.False);
    }
}
