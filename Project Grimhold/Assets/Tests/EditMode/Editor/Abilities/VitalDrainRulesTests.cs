using NUnit.Framework;

public sealed class VitalDrainRulesTests
{
    [Test]
    public void Configuration_IndependentParametersAndZeroEffect_AreValid()
    {
        Assert.That(VitalDrainRules.TryValidateConfiguration(3f, 5f, 0.5f, 0.75f, out var error), Is.True);
        Assert.That(error, Is.Empty);
        Assert.That(VitalDrainRules.TryValidateConfiguration(1f, 0f, 0f, 2f, out _), Is.True);
        Assert.That(VitalDrainRules.TryValidateConfiguration(1f, 5f, 2f, 2f, out _), Is.True);
    }

    [TestCase(0f, 5f, 0.5f, 1f)]
    [TestCase(float.NaN, 5f, 0.5f, 1f)]
    [TestCase(float.PositiveInfinity, 5f, 0.5f, 1f)]
    [TestCase(3f, -1f, 0.5f, 1f)]
    [TestCase(3f, float.NaN, 0.5f, 1f)]
    [TestCase(3f, float.PositiveInfinity, 0.5f, 1f)]
    [TestCase(3f, 5f, -1f, 1f)]
    [TestCase(3f, 5f, float.NaN, 1f)]
    [TestCase(3f, 5f, float.PositiveInfinity, 1f)]
    [TestCase(3f, 5f, 0.5f, 0f)]
    [TestCase(3f, 5f, 0.5f, float.NaN)]
    [TestCase(3f, 5f, 0.5f, float.PositiveInfinity)]
    public void Configuration_InvalidValues_Reject(float radius, float damage, float proportion, float preparation)
    {
        Assert.That(VitalDrainRules.TryValidateConfiguration(radius, damage, proportion, preparation, out var error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void ConfirmedDamage_UsesActualDamageAndIgnoresRejectedResults()
    {
        double total = VitalDrainRules.AddConfirmedDamage(0d, Result(true, 3f));
        total = VitalDrainRules.AddConfirmedDamage(total, Result(false, 100f));
        total = VitalDrainRules.AddConfirmedDamage(total, Result(true, 2f));
        Assert.That(total, Is.EqualTo(5d));
        Assert.That(VitalDrainRules.CalculateHealing(total, 0.5f, 10f, 30f), Is.EqualTo(2.5f));
    }

    [TestCase(10d, 1f, 28f, 30f, 2f)]
    [TestCase(10d, 1f, 30f, 30f, 0f)]
    [TestCase(0d, 1f, 10f, 30f, 0f)]
    [TestCase(10d, 0f, 10f, 30f, 0f)]
    [TestCase(10d, 2f, 10f, 30f, 20f)]
    public void Healing_IsProportionalAndNeverOverheals(double total, float proportion, float health, float maximum, float expected)
    {
        Assert.That(VitalDrainRules.CalculateHealing(total, proportion, health, maximum), Is.EqualTo(expected));
    }

    [Test]
    public void ConfirmedDamage_NonFiniteOrNegativeResults_AreIgnored()
    {
        foreach (float damage in new[] { float.NaN, float.PositiveInfinity, -1f, 0f })
            Assert.That(VitalDrainRules.AddConfirmedDamage(4d, Result(true, damage)), Is.EqualTo(4d));
    }

    private static DamageResult Result(bool applied, float damage) =>
        new DamageResult(default, applied, damage, 0f, false, DamageFailureReason.None);
}
