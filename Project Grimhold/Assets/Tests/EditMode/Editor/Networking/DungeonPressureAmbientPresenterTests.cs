using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class DungeonPressureAmbientPresenterTests
{
    private GameObject _cameraGo;
    private CameraShakeController _shakeController;
    private GameObject _presenterGo;
    private DungeonPressureAmbientPresenter _presenter;
    private GameObject _globalLightGo;
    private Light2D _globalLight;
    private DungeonAmbientConfig _config;

    [SetUp]
    public void SetUp()
    {
        DungeonLightPropRegistry.Clear();

        _cameraGo = new GameObject("TestCamera");
        _shakeController = _cameraGo.AddComponent<CameraShakeController>();
        var cameraConfig = ScriptableObject.CreateInstance<CameraShakeConfig>();
        _shakeController.Configure(cameraConfig);

        _globalLightGo = new GameObject("GlobalLight");
        _globalLight = _globalLightGo.AddComponent<Light2D>();
        _globalLight.lightType = Light2D.LightType.Global;
        _globalLight.color = Color.white;
        _globalLight.intensity = 1f;

        _config = ScriptableObject.CreateInstance<DungeonAmbientConfig>();
        var normal = new DungeonAmbientPhaseSettings(Color.white, 0.9f, 1f, 0f, 0f, 0f, 0f, 0f, 0f);
        var reinforcements = new DungeonAmbientPhaseSettings(Color.yellow, 0.75f, 2f, 0.1f, 0.5f, 0f, 0f, 0f, 0f);
        var critical = new DungeonAmbientPhaseSettings(new Color(1f, 0.5f, 0f), 0.6f, 2f, 0.25f, 1f, 0f, 0.15f, 0.8f, 25f);
        var collapse = new DungeonAmbientPhaseSettings(Color.red, 0.5f, 1.5f, 0.75f, 2f, 0.12f, 0.35f, 1.2f, 12f);
        _config.Initialize(normal, reinforcements, critical, collapse);

        _presenterGo = new GameObject("AmbientPresenter");
        _presenter = _presenterGo.AddComponent<DungeonPressureAmbientPresenter>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_presenterGo != null) Object.DestroyImmediate(_presenterGo);
        if (_globalLightGo != null) Object.DestroyImmediate(_globalLightGo);
        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
        DungeonLightPropRegistry.Clear();
    }

    [Test]
    public void InitialPhase_AppliesDirectlyWithoutTriggeringEntryShake()
    {
        _presenter.Configure(null, _config, _globalLight);

        Assert.That(_globalLight.color, Is.EqualTo(Color.white));
        Assert.That(_globalLight.intensity, Is.EqualTo(0.9f).Within(0.001f));
        Assert.That(_shakeController.EffectiveTrauma, Is.EqualTo(0f));
    }

    [Test]
    public void InitialPhase_LateJoinInCollapse_SetsContinuousShakeDirectly()
    {
        _presenter.Configure(null, _config, _globalLight);
        _presenter.ApplyInitialPhase(DungeonPressurePhase.Collapse);

        Assert.That(_globalLight.color, Is.EqualTo(Color.red));
        Assert.That(_globalLight.intensity, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(_shakeController.SustainedTrauma, Is.EqualTo(0.12f).Within(0.001f));
        Assert.That(_shakeController.EffectiveTrauma, Is.EqualTo(0.12f).Within(0.001f));
    }

    [Test]
    public void PeriodicShake_TriggersWhenIntervalElapses()
    {
        var customConfig = ScriptableObject.CreateInstance<DungeonAmbientConfig>();
        var normalWithPeriodicShake = new DungeonAmbientPhaseSettings(
            Color.white, 1f, 1f,
            0f, 0f, 0f, // entry and continuous: 0
            0.5f, 1.0f, 5.0f // periodic: 0.5 intensity, 1.0s duration, every 5.0s
        );
        customConfig.Initialize(normalWithPeriodicShake, normalWithPeriodicShake, normalWithPeriodicShake, normalWithPeriodicShake);

        _presenter.Configure(null, customConfig, _globalLight);

        // Before interval elapses
        _presenter.EvaluatePresentation(3.0f);
        Assert.That(_shakeController.EffectiveTrauma, Is.EqualTo(0f));

        // When interval elapses (3.0 + 2.1 = 5.1s >= 5.0s)
        _presenter.EvaluatePresentation(2.1f);
        Assert.That(_shakeController.EffectiveTrauma, Is.GreaterThan(0f));
    }
}
