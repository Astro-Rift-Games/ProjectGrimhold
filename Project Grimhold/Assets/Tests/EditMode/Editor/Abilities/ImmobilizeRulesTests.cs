using NUnit.Framework;

public sealed class ImmobilizeRulesTests
{
    // ---- Configuration -------------------------------------------------------------------

    [Test]
    public void TryValidateConfiguration_BaselineValues_IsValid()
    {
        Assert.That(ImmobilizeRules.TryValidateConfiguration(3f, 2f, 1f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidateConfiguration_ZeroDamage_IsValid()
    {
        Assert.That(ImmobilizeRules.TryValidateConfiguration(3f, 0f, 1f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidateConfiguration_IntervalLongerThanDuration_IsValid()
    {
        Assert.That(ImmobilizeRules.TryValidateConfiguration(1f, 2f, 5f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    // duration, damage per tick, tick interval
    [TestCase(0f, 2f, 1f)]
    [TestCase(-1f, 2f, 1f)]
    [TestCase(float.NaN, 2f, 1f)]
    [TestCase(float.PositiveInfinity, 2f, 1f)]
    [TestCase(3f, -1f, 1f)]
    [TestCase(3f, float.NaN, 1f)]
    [TestCase(3f, float.PositiveInfinity, 1f)]
    [TestCase(3f, 2f, 0f)]
    [TestCase(3f, 2f, -0.5f)]
    [TestCase(3f, 2f, float.NaN)]
    [TestCase(3f, 2f, float.PositiveInfinity)]
    public void TryValidateConfiguration_InvalidInput_IsRejectedWithAMessage(float durationSeconds, float damagePerTick,
        float tickIntervalSeconds)
    {
        Assert.That(ImmobilizeRules.TryValidateConfiguration(durationSeconds, damagePerTick, tickIntervalSeconds,
            out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    // ---- Step decision -------------------------------------------------------------------

    [Test]
    public void Decide_NotActive_DoesNothingWhateverElseIsTrue()
    {
        Assert.That(ImmobilizeRules.Decide(false, true, true, true), Is.EqualTo(ImmobilizeStep.None));
        Assert.That(ImmobilizeRules.Decide(false, false, true, true), Is.EqualTo(ImmobilizeStep.None));
    }

    [Test]
    public void Decide_ActiveAndNothingDue_DoesNothing()
    {
        Assert.That(ImmobilizeRules.Decide(true, true, false, false), Is.EqualTo(ImmobilizeStep.None));
    }

    [Test]
    public void Decide_TickDue_AppliesOneTick()
    {
        Assert.That(ImmobilizeRules.Decide(true, true, false, true), Is.EqualTo(ImmobilizeStep.ApplyTick));
    }

    [Test]
    public void Decide_DurationExpiredWithoutTick_Ends()
    {
        Assert.That(ImmobilizeRules.Decide(true, true, true, false), Is.EqualTo(ImmobilizeStep.End));
    }

    [Test]
    public void Decide_TickAndExpiryOnTheSameStep_AppliesTheLastTickThenEnds()
    {
        Assert.That(ImmobilizeRules.Decide(true, true, true, true), Is.EqualTo(ImmobilizeStep.ApplyTick | ImmobilizeStep.End));
    }

    [Test]
    public void Decide_DeadTarget_EndsWithoutAnotherTick()
    {
        Assert.That(ImmobilizeRules.Decide(true, false, false, true), Is.EqualTo(ImmobilizeStep.End));
        Assert.That(ImmobilizeRules.Decide(true, false, true, true), Is.EqualTo(ImmobilizeStep.End));
        Assert.That(ImmobilizeRules.Decide(true, false, false, false), Is.EqualTo(ImmobilizeStep.End));
    }
}
