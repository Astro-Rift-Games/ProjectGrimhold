using NUnit.Framework;
using UnityEngine;

public sealed class SeismicStrikeRulesTests
{
    // ---- Configuration -------------------------------------------------------------------

    [Test]
    public void TryValidateConfiguration_BaselineValues_IsValid()
    {
        Assert.That(SeismicStrikeRules.TryValidateConfiguration(3f, 5f, 8f, 0.75f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidateConfiguration_ZeroDamageAndZeroKnockback_IsValid()
    {
        Assert.That(SeismicStrikeRules.TryValidateConfiguration(3f, 0f, 0f, 0.75f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [TestCase(0f, 5f, 8f, 0.75f)]
    [TestCase(-1f, 5f, 8f, 0.75f)]
    [TestCase(float.NaN, 5f, 8f, 0.75f)]
    [TestCase(float.PositiveInfinity, 5f, 8f, 0.75f)]
    [TestCase(3f, -1f, 8f, 0.75f)]
    [TestCase(3f, float.NaN, 8f, 0.75f)]
    [TestCase(3f, float.PositiveInfinity, 8f, 0.75f)]
    [TestCase(3f, 5f, -1f, 0.75f)]
    [TestCase(3f, 5f, float.NaN, 0.75f)]
    [TestCase(3f, 5f, float.PositiveInfinity, 0.75f)]
    [TestCase(3f, 5f, 8f, 0f)]
    [TestCase(3f, 5f, 8f, -0.5f)]
    [TestCase(3f, 5f, 8f, float.NaN)]
    [TestCase(3f, 5f, 8f, float.PositiveInfinity)]
    public void TryValidateConfiguration_InvalidInput_IsRejectedWithAMessage(
        float radius, float damage, float knockbackForce, float preparationSeconds)
    {
        Assert.That(SeismicStrikeRules.TryValidateConfiguration(radius, damage, knockbackForce, preparationSeconds,
            out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    // ---- Radial direction ----------------------------------------------------------------

    [Test]
    public void ResolveKnockbackDirection_TargetToTheRight_PointsRight()
    {
        Vector2 direction = SeismicStrikeRules.ResolveKnockbackDirection(Vector2.zero, new Vector2(3f, 0f));
        Assert.That(direction, Is.EqualTo(Vector2.right));
    }

    [Test]
    public void ResolveKnockbackDirection_IsRelativeToTheCaster()
    {
        Vector2 direction = SeismicStrikeRules.ResolveKnockbackDirection(new Vector2(10f, 10f), new Vector2(10f, 7f));
        Assert.That(direction, Is.EqualTo(Vector2.down));
    }

    [Test]
    public void ResolveKnockbackDirection_DiagonalTarget_IsNormalized()
    {
        Vector2 direction = SeismicStrikeRules.ResolveKnockbackDirection(Vector2.zero, new Vector2(2f, 2f));
        Assert.That(direction.magnitude, Is.EqualTo(1f).Within(1e-5f));
        Assert.That(direction.x, Is.EqualTo(direction.y).Within(1e-5f));
        Assert.That(direction.x, Is.GreaterThan(0f));
    }

    [Test]
    public void ResolveKnockbackDirection_CoincidentTarget_UsesTheDocumentedFallback()
    {
        Vector2 direction = SeismicStrikeRules.ResolveKnockbackDirection(new Vector2(4f, 4f), new Vector2(4f, 4f));
        Assert.That(direction, Is.EqualTo(SeismicStrikeRules.FallbackDirection));
        Assert.That(SeismicStrikeRules.FallbackDirection.magnitude, Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void ResolveKnockbackDirection_NearlyCoincidentTarget_UsesTheFallback()
    {
        Vector2 direction = SeismicStrikeRules.ResolveKnockbackDirection(Vector2.zero, new Vector2(1e-6f, 0f));
        Assert.That(direction, Is.EqualTo(SeismicStrikeRules.FallbackDirection));
    }

    [Test]
    public void ResolveKnockbackDirection_NonFinitePosition_UsesTheFallback()
    {
        Vector2 direction = SeismicStrikeRules.ResolveKnockbackDirection(Vector2.zero, new Vector2(float.NaN, 1f));
        Assert.That(direction, Is.EqualTo(SeismicStrikeRules.FallbackDirection));
    }

    // ---- Shared impact -------------------------------------------------------------------

    [Test]
    public void UsesDamagePipeline_OnlyForPositiveDamage()
    {
        Assert.That(AbilityImpact.UsesDamagePipeline(10f), Is.True);
        Assert.That(AbilityImpact.UsesDamagePipeline(0f), Is.False);
    }
}
