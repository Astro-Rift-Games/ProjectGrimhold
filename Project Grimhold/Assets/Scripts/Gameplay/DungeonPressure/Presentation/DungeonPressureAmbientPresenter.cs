using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls global ambient lighting and camera-shake presentation for dungeon pressure phases.
/// Observes the replicated phase on <see cref="DungeonPressureController"/> and smoothly modulates
/// the scene's global light and local <see cref="DungeonLightProp"/> components.
/// Handles late-joining players, host migration, and offline preview cleanly without requiring
/// networked state.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonPressureAmbientPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The pressure controller to monitor. If null, automatically resolves from this GameObject or the active scene.")]
    [SerializeField] private DungeonPressureController _pressureController;

    [Tooltip("The scene's global 2D light. If null, automatically located in the scene.")]
    [SerializeField] private Light2D _globalLight;

    [Header("Configuration")]
    [Tooltip("Configuration asset defining global ambient lighting and screen shake per phase.")]
    [SerializeField] private DungeonAmbientConfig _config;

    private DungeonPressurePhase _lastPhase = DungeonPressurePhase.Normal;

    private Color _currentGlobalColor = Color.white;
    private float _currentGlobalIntensity = 1f;
    private Color _fromGlobalColor = Color.white;
    private float _fromGlobalIntensity = 1f;
    private Color _targetGlobalColor = Color.white;
    private float _targetGlobalIntensity = 1f;

    private float _globalTransitionDuration;
    private float _globalTransitionProgress = 1f;
    private float _periodicShakeTimer;
    private bool _hasResolvedInitialPhase;

    public DungeonPressurePhase CurrentPhase => _lastPhase;
    public Light2D GlobalLight => _globalLight;
    public DungeonAmbientConfig Config => _config;
    public DungeonPressureController PressureController => _pressureController;

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        InitializeComponents();
        if (_pressureController != null && !_hasResolvedInitialPhase)
        {
            ApplyInitialPhase(_pressureController.Phase);
        }
    }

    private void OnDisable()
    {
        ResetPresentation();
    }

    private void OnDestroy()
    {
        ResetPresentation();
    }

    public void Configure(DungeonPressureController controller, DungeonAmbientConfig config, Light2D globalLight = null)
    {
        _pressureController = controller;
        _config = config;
        if (globalLight != null)
        {
            _globalLight = globalLight;
        }

        InitializeComponents();
        DungeonPressurePhase initialPhase = _pressureController != null ? _pressureController.Phase : _lastPhase;
        ApplyInitialPhase(initialPhase);
    }

    private void InitializeComponents()
    {
        if (_pressureController == null)
        {
            _pressureController = GetComponent<DungeonPressureController>();
            if (_pressureController == null)
            {
                _pressureController = Object.FindAnyObjectByType<DungeonPressureController>();
            }
        }

        if (_globalLight == null)
        {
            LocateGlobalLight();
        }

        if (_globalLight != null && !_hasResolvedInitialPhase)
        {
            _currentGlobalColor = _globalLight.color;
            _currentGlobalIntensity = _globalLight.intensity;
            _targetGlobalColor = _currentGlobalColor;
            _targetGlobalIntensity = _currentGlobalIntensity;
        }
    }

    private void LocateGlobalLight()
    {
        Light2D[] lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Exclude);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].lightType == Light2D.LightType.Global)
            {
                _globalLight = lights[i];
                break;
            }
        }
    }

    public void ApplyInitialPhase(DungeonPressurePhase phase)
    {
        _lastPhase = phase;
        _periodicShakeTimer = 0f;
        _hasResolvedInitialPhase = true;

        if (_config == null)
        {
            return;
        }

        DungeonAmbientPhaseSettings settings = _config.GetSettings(phase);

        // Snap global light immediately without entry shake to handle Late Join and Host Migration cleanly
        _targetGlobalColor = settings.GlobalLightColor;
        _targetGlobalIntensity = settings.GlobalLightIntensity;
        _currentGlobalColor = _targetGlobalColor;
        _currentGlobalIntensity = _targetGlobalIntensity;
        _globalTransitionProgress = 1f;

        if (_globalLight != null)
        {
            _globalLight.color = _currentGlobalColor;
            _globalLight.intensity = _currentGlobalIntensity;
        }

        // Apply continuous shake if this phase defines a sustained baseline (e.g. Collapse)
        if (CameraShakeController.Instance != null)
        {
            CameraShakeController.Instance.SetContinuousShake(settings.ContinuousShakeIntensity);
        }

        // Notify props immediately
        DungeonLightPropRegistry.NotifyPhaseChanged(phase, immediate: true);
    }

    private void Update()
    {
        if (_pressureController == null)
        {
            InitializeComponents();
            if (_pressureController == null)
            {
                return;
            }
        }

        if (!_hasResolvedInitialPhase)
        {
            ApplyInitialPhase(_pressureController.Phase);
        }

        EvaluatePresentation(Time.deltaTime);
    }

    public void EvaluatePresentation(float deltaTime)
    {
        if (_pressureController != null)
        {
            DungeonPressurePhase newPhase = _pressureController.Phase;

            if (newPhase != _lastPhase)
            {
                OnPhaseChanged(newPhase);
                _lastPhase = newPhase;
            }
        }

        UpdatePeriodicShake(deltaTime);
        UpdateGlobalLightInterpolation(deltaTime);
    }

    private void OnPhaseChanged(DungeonPressurePhase newPhase)
    {
        if (_config == null)
        {
            return;
        }

        _periodicShakeTimer = 0f;
        DungeonAmbientPhaseSettings settings = _config.GetSettings(newPhase);

        // Only trigger entry shake when pressure escalates forward
        if (newPhase > _lastPhase && settings.EntryShakeIntensity > 0f)
        {
            CameraShakeController.Instance?.RequestShake(settings.EntryShakeIntensity, settings.EntryShakeDuration);
        }

        // Maintain continuous baseline shake throughout this phase
        CameraShakeController.Instance?.SetContinuousShake(settings.ContinuousShakeIntensity);

        // Setup smooth transition for global ambient light
        _targetGlobalColor = settings.GlobalLightColor;
        _targetGlobalIntensity = settings.GlobalLightIntensity;
        _globalTransitionDuration = settings.GlobalTransitionDuration;
        _fromGlobalColor = _currentGlobalColor;
        _fromGlobalIntensity = _currentGlobalIntensity;

        if (_globalTransitionDuration <= 0f)
        {
            _currentGlobalColor = _targetGlobalColor;
            _currentGlobalIntensity = _targetGlobalIntensity;
            _globalTransitionProgress = 1f;
            if (_globalLight != null)
            {
                _globalLight.color = _currentGlobalColor;
                _globalLight.intensity = _currentGlobalIntensity;
            }
        }
        else
        {
            _globalTransitionProgress = 0f;
        }

        // Notify all registered light props in the dungeon
        DungeonLightPropRegistry.NotifyPhaseChanged(newPhase, immediate: false);
    }

        private void UpdatePeriodicShake(float deltaTime)
    {
        if (_config == null)
        {
            return;
        }

        DungeonAmbientPhaseSettings settings = _config.GetSettings(_lastPhase);
        if (settings.PeriodicShakeIntervalSeconds <= 0f || settings.PeriodicShakeIntensity <= 0f)
        {
            return;
        }

        _periodicShakeTimer += deltaTime;
        if (_periodicShakeTimer >= settings.PeriodicShakeIntervalSeconds)
        {
            _periodicShakeTimer = 0f;
            CameraShakeController.Instance?.RequestShake(settings.PeriodicShakeIntensity, settings.PeriodicShakeDuration);
        }
    }

    private void UpdateGlobalLightInterpolation(float deltaTime)
    {
        if (_globalLight == null || _globalTransitionProgress >= 1f)
        {
            return;
        }

        float rate = _globalTransitionDuration > 0.001f ? (deltaTime / _globalTransitionDuration) : 1f;
        _globalTransitionProgress = Mathf.Clamp01(_globalTransitionProgress + rate);

        _currentGlobalColor = Color.Lerp(_fromGlobalColor, _targetGlobalColor, _globalTransitionProgress);
        _currentGlobalIntensity = Mathf.Lerp(_fromGlobalIntensity, _targetGlobalIntensity, _globalTransitionProgress);

        _globalLight.color = _currentGlobalColor;
        _globalLight.intensity = _currentGlobalIntensity;
    }

    private void ResetPresentation()
    {
        if (CameraShakeController.Instance != null)
        {
            CameraShakeController.Instance.SetContinuousShake(0f);
        }

        DungeonLightPropRegistry.Clear();
        _periodicShakeTimer = 0f;
        _hasResolvedInitialPhase = false;
    }
}
