using NUnit.Framework;
using UnityEngine;

public sealed class DungeonLightPropTests
{
    [TearDown]
    public void TearDown()
    {
        DungeonLightPropRegistry.Clear();
    }

    [Test]
    public void LightPropConfig_GetSettings_ReturnsConfiguredSettings()
    {
        var config = ScriptableObject.CreateInstance<DungeonLightPropConfig>();

        var normal = new DungeonLightPropPhaseSettings(Color.white, 1.2f, LightEffectPattern.TorchFlicker, 1f);
        var reinforcements = new DungeonLightPropPhaseSettings(Color.yellow, 1.0f, LightEffectPattern.TorchFlicker, 2f);
        var critical = new DungeonLightPropPhaseSettings(Color.magenta, 0.8f, LightEffectPattern.UnstableFlicker, 2f);
        var collapse = new DungeonLightPropPhaseSettings(Color.red, 0.5f, LightEffectPattern.UnstableFlicker, 1.5f);

        config.Initialize(normal, reinforcements, critical, collapse);

        Assert.That(config.GetSettings(DungeonPressurePhase.Normal).Pattern, Is.EqualTo(LightEffectPattern.TorchFlicker));
        Assert.That(config.GetSettings(DungeonPressurePhase.CriticalPressure).Pattern, Is.EqualTo(LightEffectPattern.UnstableFlicker));
        Assert.That(config.GetSettings(DungeonPressurePhase.Collapse).Intensity, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(config.GetSettings((DungeonPressurePhase)123).Pattern, Is.EqualTo(LightEffectPattern.TorchFlicker));
    }

    [Test]
    public void LightPropSettings_ClampsNegativeValues()
    {
        var settings = new DungeonLightPropPhaseSettings(Color.red, -10f, LightEffectPattern.Pulse, -5f);

        Assert.That(settings.Intensity, Is.EqualTo(0f));
        Assert.That(settings.TransitionDuration, Is.EqualTo(0f));
        Assert.That(settings.Pattern, Is.EqualTo(LightEffectPattern.Pulse));
    }

    [Test]
    public void Registry_TracksAndCleansPropsCorrectly()
    {
        DungeonLightPropRegistry.Clear();
        Assert.That(DungeonLightPropRegistry.Count, Is.EqualTo(0));

        var go1 = new GameObject("TestProp1");
        var prop1 = go1.AddComponent<DungeonLightProp>();

        Assert.That(DungeonLightPropRegistry.Count, Is.EqualTo(1));

        var go2 = new GameObject("TestProp2");
        var prop2 = go2.AddComponent<DungeonLightProp>();

        Assert.That(DungeonLightPropRegistry.Count, Is.EqualTo(2));

        DungeonLightPropRegistry.NotifyPhaseChanged(DungeonPressurePhase.CriticalPressure, immediate: true);
        Assert.That(DungeonLightPropRegistry.CurrentPhase, Is.EqualTo(DungeonPressurePhase.CriticalPressure));

        // Destroying an object cleans up on unregister
        Object.DestroyImmediate(go1);
        Assert.That(DungeonLightPropRegistry.Count, Is.EqualTo(1));

        Object.DestroyImmediate(go2);
        Assert.That(DungeonLightPropRegistry.Count, Is.EqualTo(0));
    }
}
