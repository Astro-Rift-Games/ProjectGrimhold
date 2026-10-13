using NUnit.Framework;

public sealed class PurificationRulesTests
{
    // ---- Target condition ----------------------------------------------------------------

    [Test]
    public void IsValidTarget_PurifiableStateOnly_IsValid() =>
        Assert.That(PurificationRules.IsValidTarget(true, false), Is.True);

    [Test]
    public void IsValidTarget_NoStateButCanReceiveProtection_IsValidForPreventiveUse() =>
        Assert.That(PurificationRules.IsValidTarget(false, true), Is.True);

    [Test]
    public void IsValidTarget_StateAndProtection_IsValid() =>
        Assert.That(PurificationRules.IsValidTarget(true, true), Is.True);

    [Test]
    public void IsValidTarget_NeitherStateNorProtection_IsNotValid() =>
        Assert.That(PurificationRules.IsValidTarget(false, false), Is.False);

    // ---- Configuration -------------------------------------------------------------------

    [Test]
    public void TryValidateConfiguration_BaselineValues_IsValid()
    {
        Assert.That(PurificationRules.TryValidateConfiguration(3f, 0.5f, 5f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [TestCase(0f, 0.5f, 5f)]
    [TestCase(-1f, 0.5f, 5f)]
    [TestCase(float.NaN, 0.5f, 5f)]
    [TestCase(float.PositiveInfinity, 0.5f, 5f)]
    [TestCase(3f, 0f, 5f)]
    [TestCase(3f, -0.5f, 5f)]
    [TestCase(3f, float.NaN, 5f)]
    [TestCase(3f, float.PositiveInfinity, 5f)]
    [TestCase(3f, 0.5f, 0f)]
    [TestCase(3f, 0.5f, -5f)]
    [TestCase(3f, 0.5f, float.NaN)]
    [TestCase(3f, 0.5f, float.PositiveInfinity)]
    public void TryValidateConfiguration_InvalidInput_IsRejectedWithAMessage(
        float radius, float castSeconds, float protectionSeconds)
    {
        Assert.That(PurificationRules.TryValidateConfiguration(radius, castSeconds, protectionSeconds,
            out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }
}
