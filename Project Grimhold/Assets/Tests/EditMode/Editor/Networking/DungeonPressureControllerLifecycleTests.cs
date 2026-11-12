using NUnit.Framework;
using UnityEngine;

public sealed class DungeonPressureControllerLifecycleTests
{
    private GameObject _controllerGo;
    private DungeonPressureController _controller;

    [SetUp]
    public void SetUp()
    {
        _controllerGo = new GameObject("TestDungeonPressureController");
        _controller = _controllerGo.AddComponent<DungeonPressureController>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_controllerGo != null)
        {
            Object.DestroyImmediate(_controllerGo);
        }
    }

    [Test]
    public void WhenNotSpawned_HasReadableStateIsFalse()
    {
        Assert.That(_controller.HasReadableState, Is.False);
    }

    [Test]
    public void WhenNotSpawned_TryGetPhase_ReturnsFalseAndNormal()
    {
        bool success = _controller.TryGetPhase(out DungeonPressurePhase phase);
        Assert.That(success, Is.False);
        Assert.That(phase, Is.EqualTo(DungeonPressurePhase.Normal));
    }

    [Test]
    public void WhenNotSpawned_RemainingSeconds_ReturnsZeroWithoutThrowing()
    {
        Assert.DoesNotThrow(() =>
        {
            int remaining = _controller.RemainingSeconds;
            Assert.That(remaining, Is.EqualTo(0));
        });
    }

    [Test]
    public void WhenNotSpawned_InstabilityThreshold_ReturnsDefaultWithoutThrowing()
    {
        Assert.DoesNotThrow(() =>
        {
            int threshold = _controller.InstabilityThreshold;
            Assert.That(threshold, Is.EqualTo(int.MaxValue));
        });
    }

    [Test]
    public void WhenNotSpawned_MinIntervalSeconds_ReturnsZeroWithoutThrowing()
    {
        Assert.DoesNotThrow(() =>
        {
            int minInterval = _controller.MinIntervalSeconds;
            Assert.That(minInterval, Is.EqualTo(0));
        });
    }
}
