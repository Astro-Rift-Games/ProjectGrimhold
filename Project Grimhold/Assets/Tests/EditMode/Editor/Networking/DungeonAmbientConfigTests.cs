using NUnit.Framework;
using UnityEngine;

public sealed class DungeonAmbientConfigTests
{
    [Test]
    public void GetSettings_ReturnsConfiguredPhaseSettings()
    {
        var config = ScriptableObject.CreateInstance<DungeonAmbientConfig>();

        var normal = new DungeonAmbientPhaseSettings(Color.white, 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f);
        var reinforcements = new DungeonAmbientPhaseSettings(Color.yellow, 0.8f, 2f, 0.1f, 0.5f, 0f, 0f, 0f, 0f);
        var critical = new DungeonAmbientPhaseSettings(new Color(1f, 0.5f, 0f), 0.6f, 2f, 0.3f, 1f, 0f, 0.15f, 0.8f, 25f);
        var collapse = new DungeonAmbientPhaseSettings(Color.red, 0.4f, 1.5f, 0.8f, 2f, 0.15f, 0.35f, 1.2f, 12f);

        config.Initialize(normal, reinforcements, critical, collapse);

        Assert.That(config.GetSettings(DungeonPressurePhase.Normal).GlobalLightColor, Is.EqualTo(Color.white));
        Assert.That(config.GetSettings(DungeonPressurePhase.Reinforcements).GlobalLightIntensity, Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(config.GetSettings(DungeonPressurePhase.CriticalPressure).PeriodicShakeIntervalSeconds, Is.EqualTo(25f).Within(0.001f));
        Assert.That(config.GetSettings(DungeonPressurePhase.Collapse).PeriodicShakeIntensity, Is.EqualTo(0.35f).Within(0.001f));
        Assert.That(config.GetSettings(DungeonPressurePhase.Collapse).ContinuousShakeIntensity, Is.EqualTo(0.15f).Within(0.001f));
    }

    [Test]
    public void GetSettings_InvalidPhase_DefaultsToNormal()
    {
        var config = ScriptableObject.CreateInstance<DungeonAmbientConfig>();
        var normal = new DungeonAmbientPhaseSettings(Color.cyan, 0.9f, 1f, 0.05f, 0.2f, 0f, 0f, 0f, 0f);
        config.Initialize(normal, normal, normal, normal);

        var result = config.GetSettings((DungeonPressurePhase)99);
        Assert.That(result.GlobalLightColor, Is.EqualTo(Color.cyan));
    }

    [Test]
    public void SettingsConstructor_ClampsValuesSafely()
    {
        var settings = new DungeonAmbientPhaseSettings(
            Color.white,
            -5f, // intensity negative -> 0
            -2f, // transition negative -> 0
            1.5f, // entry intensity > 1 -> 1
            -1f, // entry duration negative -> 0
            -0.5f, // continuous shake negative -> 0
            1.8f, // periodic intensity > 1 -> 1
            -3f, // periodic duration negative -> 0
            -10f, // periodic interval negative -> 0
            Color.black,
            1.5f, // vignette intensity > 1 -> 1
            0.001f, // smoothness < 0.01 -> 0.01
            true,
            -5f // transition duration < 0 -> 0
        );

        Assert.That(settings.GlobalLightIntensity, Is.EqualTo(0f));
        Assert.That(settings.GlobalTransitionDuration, Is.EqualTo(0f));
        Assert.That(settings.EntryShakeIntensity, Is.EqualTo(1f));
        Assert.That(settings.EntryShakeDuration, Is.EqualTo(0f));
        Assert.That(settings.ContinuousShakeIntensity, Is.EqualTo(0f));
        Assert.That(settings.PeriodicShakeIntensity, Is.EqualTo(1f));
        Assert.That(settings.PeriodicShakeDuration, Is.EqualTo(0f));
        Assert.That(settings.PeriodicShakeIntervalSeconds, Is.EqualTo(0f));
        Assert.That(settings.VignetteIntensity, Is.EqualTo(1f));
        Assert.That(settings.VignetteSmoothness, Is.EqualTo(0.01f));
        Assert.That(settings.VignetteRounded, Is.True);
        Assert.That(settings.VignetteTransitionDuration, Is.EqualTo(0f));
    }

    [Test]
    public void VignetteSettings_StoredAndRetrievedCorrectly()
    {
        var config = ScriptableObject.CreateInstance<DungeonAmbientConfig>();
        var bloodRed = new Color(0.35f, 0.03f, 0.03f, 1f);

        var collapse = new DungeonAmbientPhaseSettings(
            Color.red, 0.5f, 1.5f,
            0.8f, 2f, 0.1f,
            0.3f, 1f, 10f,
            bloodRed, 0.65f, 0.55f, false, 2.5f
        );

        config.Initialize(collapse, collapse, collapse, collapse);

        var retrieved = config.GetSettings(DungeonPressurePhase.Collapse);
        Assert.That(retrieved.VignetteColor, Is.EqualTo(bloodRed));
        Assert.That(retrieved.VignetteIntensity, Is.EqualTo(0.65f).Within(0.001f));
        Assert.That(retrieved.VignetteSmoothness, Is.EqualTo(0.55f).Within(0.001f));
        Assert.That(retrieved.VignetteRounded, Is.False);
        Assert.That(retrieved.VignetteTransitionDuration, Is.EqualTo(2.5f).Within(0.001f));
    }
}
