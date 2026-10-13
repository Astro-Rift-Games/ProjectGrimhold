using NUnit.Framework;

public sealed class EmpowerRulesTests
{
    // ---- Damage multiplier ---------------------------------------------------------------

    [Test]
    public void ResolveDamageMultiplier_Active_IsOnePlusTheBonus() =>
        Assert.That(EmpowerRules.ResolveDamageMultiplier(true, 0.3f), Is.EqualTo(1.3f).Within(0.0001f));

    [Test]
    public void ResolveDamageMultiplier_Inactive_IsExactlyOne() =>
        Assert.That(EmpowerRules.ResolveDamageMultiplier(false, 0.3f), Is.EqualTo(1f));

    [TestCase(0f)]
    [TestCase(-0.5f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void ResolveDamageMultiplier_UnusableStoredBonus_IsExactlyOne(float bonusFraction) =>
        Assert.That(EmpowerRules.ResolveDamageMultiplier(true, bonusFraction), Is.EqualTo(1f));

    // ---- Refresh -------------------------------------------------------------------------

    [Test]
    public void ResolveRefreshedBonus_SameBonusAgain_DoesNotAccumulate() =>
        Assert.That(EmpowerRules.ResolveRefreshedBonus(0.3f, 0.3f), Is.EqualTo(0.3f));

    [Test]
    public void ResolveRefreshedBonus_DifferentBonus_IsSetNotAdded()
    {
        Assert.That(EmpowerRules.ResolveRefreshedBonus(0.3f, 0.5f), Is.EqualTo(0.5f));
        Assert.That(EmpowerRules.ResolveRefreshedBonus(0.5f, 0.3f), Is.EqualTo(0.3f));
    }

    [Test]
    public void ResolveRefreshedBonus_RepeatedApplications_KeepASingleMultiplier()
    {
        float bonus = 0f;
        for (int i = 0; i < 5; i++) bonus = EmpowerRules.ResolveRefreshedBonus(bonus, 0.3f);

        Assert.That(EmpowerRules.ResolveDamageMultiplier(true, bonus), Is.EqualTo(1.3f).Within(0.0001f));
    }

    // ---- Configuration -------------------------------------------------------------------

    [Test]
    public void TryValidateConfiguration_BaselineValues_IsValid()
    {
        Assert.That(EmpowerRules.TryValidateConfiguration(3f, 0.3f, 10f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [TestCase(0f, 0.3f, 10f)]
    [TestCase(-1f, 0.3f, 10f)]
    [TestCase(float.NaN, 0.3f, 10f)]
    [TestCase(float.PositiveInfinity, 0.3f, 10f)]
    [TestCase(3f, 0f, 10f)]
    [TestCase(3f, -0.3f, 10f)]
    [TestCase(3f, float.NaN, 10f)]
    [TestCase(3f, float.PositiveInfinity, 10f)]
    [TestCase(3f, 0.3f, 0f)]
    [TestCase(3f, 0.3f, -10f)]
    [TestCase(3f, 0.3f, float.NaN)]
    [TestCase(3f, 0.3f, float.PositiveInfinity)]
    public void TryValidateConfiguration_InvalidInput_IsRejectedWithAMessage(
        float radius, float bonusFraction, float durationSeconds)
    {
        Assert.That(EmpowerRules.TryValidateConfiguration(radius, bonusFraction, durationSeconds,
            out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void TryValidateEffect_ValidValues_IsValid() =>
        Assert.That(EmpowerRules.TryValidateEffect(0.3f, 10f, out _), Is.True);

    [TestCase(0f, 10f)]
    [TestCase(0.3f, 0f)]
    [TestCase(float.NaN, 10f)]
    [TestCase(0.3f, float.PositiveInfinity)]
    public void TryValidateEffect_InvalidValues_IsRejected(float bonusFraction, float durationSeconds) =>
        Assert.That(EmpowerRules.TryValidateEffect(bonusFraction, durationSeconds, out _), Is.False);
}
