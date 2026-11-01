using NUnit.Framework;
using UnityEngine;

public sealed class LockpickChannelRulesTests
{
    [TestCase(3f, true)]
    [TestCase(0.1f, true)]
    [TestCase(0f, false)]
    [TestCase(-1f, false)]
    [TestCase(float.NaN, false)]
    [TestCase(float.PositiveInfinity, false)]
    public void Duration_MustBeFiniteAndPositive(float seconds, bool expected)
    {
        Assert.That(LockpickChannelRules.IsValidDuration(seconds), Is.EqualTo(expected));
    }

    [Test]
    public void Movement_InterruptsForIntentOrDisplacement_ButNotStationaryInput()
    {
        Assert.That(LockpickChannelRules.HasMoved(Vector2.zero, Vector2.zero, Vector2.zero), Is.False);
        Assert.That(LockpickChannelRules.HasMoved(Vector2.zero, Vector2.zero, Vector2.right), Is.True);
        Assert.That(LockpickChannelRules.HasMoved(Vector2.zero, Vector2.up * 0.01f, Vector2.zero), Is.True);
    }
}
