using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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

    [Test]
    public void ExtinguishableProp_SwapsSpriteAndDisablesFlame()
    {
        var config = ScriptableObject.CreateInstance<DungeonLightPropConfig>();
        var litSettings = new DungeonLightPropPhaseSettings(Color.white, 1f, LightEffectPattern.Steady, 0f, isExtinguished: false);
        var unlitSettings = new DungeonLightPropPhaseSettings(Color.black, 0f, LightEffectPattern.Steady, 0f, isExtinguished: true);
        config.Initialize(litSettings, litSettings, litSettings, unlitSettings);

        var go = new GameObject("TorchProp");
        var light = go.AddComponent<Light2D>();
        var renderer = go.AddComponent<SpriteRenderer>();
        var flameGo = new GameObject("Flame");
        flameGo.transform.SetParent(go.transform);

        var litSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
        var unlitSprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 1, 1), Vector2.zero);

        var prop = go.AddComponent<DungeonLightProp>();
        prop.Configure(config, light, renderer, litSprite, unlitSprite, flameGo);

        // Normal phase: Lit
        prop.ApplyPhase(DungeonPressurePhase.Normal, immediate: true);
        Assert.That(prop.IsExtinguished, Is.False);
        Assert.That(renderer.sprite, Is.EqualTo(litSprite));
        Assert.That(flameGo.activeSelf, Is.True);
        Assert.That(prop.CurrentBaseIntensity, Is.EqualTo(1f).Within(0.001f));

        // Collapse phase: Extinguished
        prop.ApplyPhase(DungeonPressurePhase.Collapse, immediate: true);
        Assert.That(prop.IsExtinguished, Is.True);
        Assert.That(renderer.sprite, Is.EqualTo(unlitSprite));
        Assert.That(flameGo.activeSelf, Is.False);
        Assert.That(prop.CurrentBaseIntensity, Is.EqualTo(0f));

        // Return to Normal: Restores lit visuals
        prop.ApplyPhase(DungeonPressurePhase.Normal, immediate: true);
        Assert.That(prop.IsExtinguished, Is.False);
        Assert.That(renderer.sprite, Is.EqualTo(litSprite));
        Assert.That(flameGo.activeSelf, Is.True);
        Assert.That(prop.CurrentBaseIntensity, Is.EqualTo(1f).Within(0.001f));

        Object.DestroyImmediate(go);
    }
}
