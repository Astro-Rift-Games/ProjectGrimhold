using NUnit.Framework;

public sealed class RestorationRulesTests
{
    // ---- Target condition ----------------------------------------------------------------

    [Test]
    public void CanReceiveRestoration_MissingHealth_IsValid() =>
        Assert.That(RestorationRules.CanReceiveRestoration(true, false, 40f, 100f), Is.True);

    [Test]
    public void CanReceiveRestoration_FullHealth_IsNotValid() =>
        Assert.That(RestorationRules.CanReceiveRestoration(true, false, 100f, 100f), Is.False);

    [Test]
    public void CanReceiveRestoration_HealthAboveMaximum_IsNotValid() =>
        Assert.That(RestorationRules.CanReceiveRestoration(true, false, 120f, 100f), Is.False);

    [Test]
    public void CanReceiveRestoration_Downed_IsNotValid() =>
        Assert.That(RestorationRules.CanReceiveRestoration(true, true, 0f, 100f), Is.False);

    [Test]
    public void CanReceiveRestoration_Dead_IsNotValid() =>
        Assert.That(RestorationRules.CanReceiveRestoration(false, false, 0f, 100f), Is.False);

    // ---- Configuration -------------------------------------------------------------------

    [Test]
    public void TryValidateConfiguration_BaselineValues_IsValid()
    {
        Assert.That(RestorationRules.TryValidateConfiguration(3f, 30f, 1f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [TestCase(0f, 30f, 1f)]
    [TestCase(-1f, 30f, 1f)]
    [TestCase(float.NaN, 30f, 1f)]
    [TestCase(float.PositiveInfinity, 30f, 1f)]
    [TestCase(3f, 0f, 1f)]
    [TestCase(3f, -5f, 1f)]
    [TestCase(3f, float.NaN, 1f)]
    [TestCase(3f, float.PositiveInfinity, 1f)]
    [TestCase(3f, 30f, 0f)]
    [TestCase(3f, 30f, -0.5f)]
    [TestCase(3f, 30f, float.NaN)]
    [TestCase(3f, 30f, float.PositiveInfinity)]
    public void TryValidateConfiguration_InvalidInput_IsRejectedWithAMessage(
        float radius, float healAmount, float preparationSeconds)
    {
        Assert.That(RestorationRules.TryValidateConfiguration(radius, healAmount, preparationSeconds,
            out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }
}
