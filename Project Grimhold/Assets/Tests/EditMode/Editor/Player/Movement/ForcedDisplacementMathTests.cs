using NUnit.Framework;
using UnityEngine;

public sealed class ForcedDisplacementMathTests
{
    private const float Epsilon = 0.0001f;

    [Test]
    public void TryValidate_ValidRequest_ReturnsNormalizedDirection()
    {
        bool accepted = ForcedDisplacementMath.TryValidate(new Vector2(3f, 4f), 5f, out Vector2 direction);

        Assert.That(accepted, Is.True);
        Assert.That(direction.x, Is.EqualTo(0.6f).Within(Epsilon));
        Assert.That(direction.y, Is.EqualTo(0.8f).Within(Epsilon));
    }

    [Test]
    public void TryValidate_ZeroDirection_Rejects()
    {
        Assert.That(ForcedDisplacementMath.TryValidate(Vector2.zero, 5f, out _), Is.False);
    }

    [Test]
    public void TryValidate_NonFiniteDirection_Rejects()
    {
        Assert.That(ForcedDisplacementMath.TryValidate(new Vector2(float.NaN, 1f), 5f, out _), Is.False);
        Assert.That(ForcedDisplacementMath.TryValidate(new Vector2(1f, float.PositiveInfinity), 5f, out _), Is.False);
    }

    [Test]
    public void TryValidate_NonPositiveSpeed_Rejects()
    {
        Assert.That(ForcedDisplacementMath.TryValidate(Vector2.right, 0f, out _), Is.False);
        Assert.That(ForcedDisplacementMath.TryValidate(Vector2.right, -1f, out _), Is.False);
    }

    [Test]
    public void TryValidate_NonFiniteSpeed_Rejects()
    {
        Assert.That(ForcedDisplacementMath.TryValidate(Vector2.right, float.NaN, out _), Is.False);
        Assert.That(ForcedDisplacementMath.TryValidate(Vector2.right, float.PositiveInfinity, out _), Is.False);
    }

    [Test]
    public void ComputeStep_DirectionSpeedAndDelta_ReturnsTheirProduct()
    {
        Vector2 step = ForcedDisplacementMath.ComputeStep(Vector2.up, 10f, 0.02f);

        Assert.That(step.x, Is.EqualTo(0f).Within(Epsilon));
        Assert.That(step.y, Is.EqualTo(0.2f).Within(Epsilon));
    }

    [Test]
    public void IsBlocked_FullStepApplied_IsNotBlocked()
    {
        Assert.That(ForcedDisplacementMath.IsBlocked(new Vector2(0.2f, 0f), new Vector2(0.2f, 0f)), Is.False);
    }

    [Test]
    public void IsBlocked_NothingApplied_IsBlocked()
    {
        Assert.That(ForcedDisplacementMath.IsBlocked(new Vector2(0.2f, 0f), Vector2.zero), Is.True);
    }

    [Test]
    public void IsBlocked_PartialStepBeyondEpsilon_IsBlocked()
    {
        Assert.That(ForcedDisplacementMath.IsBlocked(new Vector2(0.2f, 0f), new Vector2(0.1f, 0f)), Is.True);
    }

    [Test]
    public void IsBlocked_ShortfallWithinEpsilon_IsNotBlocked()
    {
        Assert.That(ForcedDisplacementMath.IsBlocked(new Vector2(0.2f, 0f), new Vector2(0.19999f, 0f)), Is.False);
    }

    [Test]
    public void IsBlocked_SlidingAlongAWallOnly_IsBlocked()
    {
        // Full-length lateral slide, no progress along the forced direction.
        Assert.That(ForcedDisplacementMath.IsBlocked(new Vector2(0.2f, 0f), new Vector2(0f, 0.2f)), Is.True);
    }

    [Test]
    public void IsBlocked_NoRequestedStep_IsNotBlocked()
    {
        Assert.That(ForcedDisplacementMath.IsBlocked(Vector2.zero, Vector2.zero), Is.False);
    }

    [Test]
    public void ShouldRemainActive_AliveAndNotDowned_Remains()
    {
        Assert.That(ForcedDisplacementMath.ShouldRemainActive(true, false), Is.True);
    }

    [Test]
    public void ShouldRemainActive_Dead_Ends()
    {
        Assert.That(ForcedDisplacementMath.ShouldRemainActive(false, false), Is.False);
    }

    [Test]
    public void ShouldRemainActive_Downed_Ends()
    {
        Assert.That(ForcedDisplacementMath.ShouldRemainActive(true, true), Is.False);
    }
}
