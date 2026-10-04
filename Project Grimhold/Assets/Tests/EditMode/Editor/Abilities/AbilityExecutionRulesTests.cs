using NUnit.Framework;

public sealed class AbilityExecutionRulesTests
{
    [TestCase(AbilityExecutionPhase.Executing, 0f)]
    [TestCase(AbilityExecutionPhase.Executing, 1f)]
    [TestCase(AbilityExecutionPhase.Preparing, 0.5f)]
    public void AuthoredStartPlan_IsAccepted(AbilityExecutionPhase phase, float duration)
    {
        Assert.That(new AbilityExecutionPlan(phase, duration).IsValidStart, Is.True);
    }

    [TestCase(AbilityExecutionPhase.Idle, 0f)]
    [TestCase(AbilityExecutionPhase.Preparing, 0f)]
    [TestCase(AbilityExecutionPhase.Executing, -1f)]
    [TestCase((AbilityExecutionPhase)99, 1f)]
    public void InvalidStartPlan_IsRejected(AbilityExecutionPhase phase, float duration)
    {
        Assert.That(new AbilityExecutionPlan(phase, duration).IsValidStart, Is.False);
    }

    [Test]
    public void NonFiniteStartPlan_IsRejected()
    {
        Assert.That(new AbilityExecutionPlan(AbilityExecutionPhase.Executing, float.NaN).IsValidStart, Is.False);
        Assert.That(new AbilityExecutionPlan(AbilityExecutionPhase.Preparing, float.PositiveInfinity).IsValidStart, Is.False);
    }
}
