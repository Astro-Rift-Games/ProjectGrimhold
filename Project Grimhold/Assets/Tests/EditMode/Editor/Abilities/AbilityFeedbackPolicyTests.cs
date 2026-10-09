using NUnit.Framework;

public sealed class AbilityFeedbackPolicyTests
{
    [Test]
    public void ShouldNotifyRejection_None_IsFalse()
    {
        Assert.That(AbilityFeedbackPolicy.ShouldNotifyRejection(AbilityActivationFailure.None), Is.False);
    }

    [TestCase(AbilityActivationFailure.Cooldown)]
    [TestCase(AbilityActivationFailure.InsufficientResource)]
    [TestCase(AbilityActivationFailure.RequirementsNotMet)]
    [TestCase(AbilityActivationFailure.AlreadyExecuting)]
    [TestCase(AbilityActivationFailure.BehaviourRejected)]
    [TestCase(AbilityActivationFailure.InvalidPlan)]
    [TestCase(AbilityActivationFailure.AimUnavailable)]
    public void ShouldNotifyRejection_RealRejections_IsTrue(AbilityActivationFailure failure)
    {
        Assert.That(AbilityFeedbackPolicy.ShouldNotifyRejection(failure), Is.True);
    }

    [TestCase(AbilityExecutionStopReason.Downed)]
    [TestCase(AbilityExecutionStopReason.Stun)]
    [TestCase(AbilityExecutionStopReason.Knockback)]
    [TestCase(AbilityExecutionStopReason.ConfigurationUnavailable)]
    public void ShouldNotifyInterruption_PlayerVisibleStops_IsTrue(AbilityExecutionStopReason reason)
    {
        Assert.That(AbilityFeedbackPolicy.ShouldNotifyInterruption(reason), Is.True);
    }

    [TestCase(AbilityExecutionStopReason.Completed)]
    [TestCase(AbilityExecutionStopReason.ParticipationEnded)]
    public void ShouldNotifyInterruption_NormalOrTeardown_IsFalse(AbilityExecutionStopReason reason)
    {
        Assert.That(AbilityFeedbackPolicy.ShouldNotifyInterruption(reason), Is.False);
    }
}
