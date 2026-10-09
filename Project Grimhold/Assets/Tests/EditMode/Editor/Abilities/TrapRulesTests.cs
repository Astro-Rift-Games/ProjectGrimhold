using NUnit.Framework;
using UnityEngine;

public sealed class TrapRulesTests
{
    // ---- Configuration -------------------------------------------------------------------

    [Test]
    public void TryValidateConfiguration_BaselineValues_IsValid()
    {
        Assert.That(TrapRules.TryValidateConfiguration(1.5f, 20f, 3, 0.5f, 3f, 2f, 1f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryValidateConfiguration_ZeroPeriodicDamage_IsValid()
    {
        Assert.That(TrapRules.TryValidateConfiguration(1.5f, 20f, 3, 0.5f, 3f, 0f, 1f, out string error), Is.True);
        Assert.That(error, Is.Empty);
    }

    // placementDistance, lifetime, maxPerCaster, triggerRadius, immobilize, periodicDamage, tickInterval
    [TestCase(0f, 20f, 3, 0.5f, 3f, 2f, 1f)]
    [TestCase(-1f, 20f, 3, 0.5f, 3f, 2f, 1f)]
    [TestCase(float.NaN, 20f, 3, 0.5f, 3f, 2f, 1f)]
    [TestCase(float.PositiveInfinity, 20f, 3, 0.5f, 3f, 2f, 1f)]
    [TestCase(1.5f, 0f, 3, 0.5f, 3f, 2f, 1f)]
    [TestCase(1.5f, float.NaN, 3, 0.5f, 3f, 2f, 1f)]
    [TestCase(1.5f, 20f, 0, 0.5f, 3f, 2f, 1f)]
    [TestCase(1.5f, 20f, -1, 0.5f, 3f, 2f, 1f)]
    [TestCase(1.5f, 20f, 3, 0f, 3f, 2f, 1f)]
    [TestCase(1.5f, 20f, 3, float.NaN, 3f, 2f, 1f)]
    [TestCase(1.5f, 20f, 3, 0.5f, 0f, 2f, 1f)]
    [TestCase(1.5f, 20f, 3, 0.5f, float.PositiveInfinity, 2f, 1f)]
    [TestCase(1.5f, 20f, 3, 0.5f, 3f, -1f, 1f)]
    [TestCase(1.5f, 20f, 3, 0.5f, 3f, float.NaN, 1f)]
    [TestCase(1.5f, 20f, 3, 0.5f, 3f, 2f, 0f)]
    [TestCase(1.5f, 20f, 3, 0.5f, 3f, 2f, float.NaN)]
    public void TryValidateConfiguration_InvalidInput_IsRejectedWithAMessage(float placementDistance,
        float lifetimeSeconds, int maxPerCaster, float triggerRadius, float immobilizeSeconds, float periodicDamage,
        float tickIntervalSeconds)
    {
        Assert.That(TrapRules.TryValidateConfiguration(placementDistance, lifetimeSeconds, maxPerCaster, triggerRadius,
            immobilizeSeconds, periodicDamage, tickIntervalSeconds, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    // ---- Placement position --------------------------------------------------------------

    [Test]
    public void TryResolvePlacement_PlacesTheTrapAheadOfTheCasterAlongTheAim()
    {
        Assert.That(TrapRules.TryResolvePlacement(new Vector2(2f, 3f), Vector2.right, 1.5f, out Vector2 position), Is.True);
        Assert.That(position.x, Is.EqualTo(3.5f).Within(1e-5f));
        Assert.That(position.y, Is.EqualTo(3f).Within(1e-5f));
    }

    [Test]
    public void TryResolvePlacement_NormalizesTheAimBeforeApplyingTheDistance()
    {
        Assert.That(TrapRules.TryResolvePlacement(Vector2.zero, new Vector2(0f, 5f), 2f, out Vector2 position), Is.True);
        Assert.That(position.x, Is.EqualTo(0f).Within(1e-5f));
        Assert.That(position.y, Is.EqualTo(2f).Within(1e-5f));
    }

    [TestCase(0f, 0f)]
    [TestCase(float.NaN, 1f)]
    [TestCase(1f, float.PositiveInfinity)]
    public void TryResolvePlacement_UnusableAim_IsRejected(float x, float y)
    {
        Assert.That(TrapRules.TryResolvePlacement(Vector2.zero, new Vector2(x, y), 2f, out Vector2 position), Is.False);
        Assert.That(position, Is.EqualTo(Vector2.zero));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    public void TryResolvePlacement_UnusableDistance_IsRejected(float distance)
    {
        Assert.That(TrapRules.TryResolvePlacement(Vector2.zero, Vector2.up, distance, out _), Is.False);
    }

    [Test]
    public void TryResolvePlacement_NonFiniteOrigin_IsRejected()
    {
        Assert.That(TrapRules.TryResolvePlacement(new Vector2(float.NaN, 0f), Vector2.up, 2f, out _), Is.False);
    }

    // ---- Per-caster limit ----------------------------------------------------------------

    [Test]
    public void SelectEviction_BelowTheLimit_EvictsNothing()
    {
        Assert.That(TrapRules.SelectEvictionIndex(new[] { 40, 10 }, 2, 3), Is.EqualTo(TrapRules.NoEviction));
    }

    [Test]
    public void SelectEviction_AtTheLimit_EvictsTheTrapWithTheLeastRemainingLifetime()
    {
        Assert.That(TrapRules.SelectEvictionIndex(new[] { 40, 10, 25 }, 3, 3), Is.EqualTo(1));
    }

    [Test]
    public void SelectEviction_TiedLifetimes_EvictsTheFirstListedTrap()
    {
        Assert.That(TrapRules.SelectEvictionIndex(new[] { 10, 10, 25 }, 3, 3), Is.EqualTo(0));
    }

    [Test]
    public void SelectEviction_OnlyConsidersTheLiveEntries()
    {
        // The array is a reusable buffer: entries past `count` are stale and must be ignored.
        Assert.That(TrapRules.SelectEvictionIndex(new[] { 40, 30, 1 }, 2, 2), Is.EqualTo(1));
    }

    [Test]
    public void SelectEviction_OverTheLimit_StillEvictsASingleTrap()
    {
        Assert.That(TrapRules.SelectEvictionIndex(new[] { 40, 10, 25, 5 }, 4, 3), Is.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void SelectEviction_NoLimit_EvictsNothing(int maxPerCaster)
    {
        Assert.That(TrapRules.SelectEvictionIndex(new[] { 40, 10 }, 2, maxPerCaster), Is.EqualTo(TrapRules.NoEviction));
    }

    [Test]
    public void SelectEviction_NullBufferOrZeroCount_EvictsNothing()
    {
        Assert.That(TrapRules.SelectEvictionIndex(null, 0, 3), Is.EqualTo(TrapRules.NoEviction));
        Assert.That(TrapRules.SelectEvictionIndex(new[] { 5 }, 0, 3), Is.EqualTo(TrapRules.NoEviction));
    }
}
