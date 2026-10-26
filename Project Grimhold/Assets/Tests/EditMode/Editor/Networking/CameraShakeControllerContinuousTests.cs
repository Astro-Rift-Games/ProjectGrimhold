using NUnit.Framework;
using UnityEngine;

public sealed class CameraShakeControllerContinuousTests
{
    private GameObject _cameraGo;
    private CameraShakeController _controller;

    [SetUp]
    public void SetUp()
    {
        _cameraGo = new GameObject("TestCamera");
        _controller = _cameraGo.AddComponent<CameraShakeController>();

        var config = ScriptableObject.CreateInstance<CameraShakeConfig>();
        _controller.Configure(config);
    }

    [TearDown]
    public void TearDown()
    {
        CameraShakeController.AccessibilityScale = 1f;
        if (_cameraGo != null)
        {
            Object.DestroyImmediate(_cameraGo);
        }
    }

    [Test]
    public void SetContinuousShake_ClampsIntensityToUnitInterval()
    {
        _controller.SetContinuousShake(-0.5f);
        Assert.That(_controller.SustainedTrauma, Is.EqualTo(0f));

        _controller.SetContinuousShake(1.5f);
        Assert.That(_controller.SustainedTrauma, Is.EqualTo(1f));

        _controller.SetContinuousShake(0.25f);
        Assert.That(_controller.SustainedTrauma, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void EffectiveTrauma_ReturnsMaxBetweenTraumaAndSustained()
    {
        _controller.SetContinuousShake(0.2f);
        Assert.That(_controller.EffectiveTrauma, Is.EqualTo(0.2f).Within(0.001f));

        // Add instantaneous trauma higher than sustained
        _controller.RequestShake(0.6f, 1f);
        Assert.That(_controller.EffectiveTrauma, Is.EqualTo(0.6f).Within(0.001f));

        // When continuous shake is cleared
        _controller.SetContinuousShake(0f);
        Assert.That(_controller.SustainedTrauma, Is.EqualTo(0f));
    }

    [Test]
    public void AccessibilityScale_ClampsBetweenZeroAndOne()
    {
        CameraShakeController.AccessibilityScale = -0.5f;
        Assert.That(CameraShakeController.AccessibilityScale, Is.EqualTo(0f));

        CameraShakeController.AccessibilityScale = 1.5f;
        Assert.That(CameraShakeController.AccessibilityScale, Is.EqualTo(1f));

        CameraShakeController.AccessibilityScale = 0.5f;
        Assert.That(CameraShakeController.AccessibilityScale, Is.EqualTo(0.5f).Within(0.001f));
    }
}
