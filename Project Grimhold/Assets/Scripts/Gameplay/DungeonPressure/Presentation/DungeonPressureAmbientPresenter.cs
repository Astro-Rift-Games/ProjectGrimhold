using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls global ambient lighting, camera-shake, and post-process vignette presentation for dungeon pressure phases.
/// Observes the replicated phase on <see cref="DungeonPressureController"/> and smoothly modulates
/// the scene's global light, local <see cref="DungeonLightProp"/> components, and native URP Volume vignette.
/// Handles late-joining players, host migration, and offline preview cleanly without requiring
/// networked state.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DungeonPressureAmbientPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The pressure controller to monitor. If null, automatically resolves from this GameObject or the active scene.")]
    [SerializeField] private DungeonPressureController _pressureController;

    [Tooltip("The scene's global 2D light. If null, automatically located in the scene.")]
    [SerializeField] private Light2D _globalLight;

    [Tooltip("The URP Volume holding the post-process Vignette. If null, automatically located in the scene.")]
    [SerializeField] private Volume _volume;

    [Header("Configuration")]
    [Tooltip("Configuration asset defining global ambient lighting, screen shake, and vignette per phase.")]
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

    // Post-Process Vignette (URP Volume)
    private Vignette _vignette;
    private Color _currentVignetteColor = Color.black;
    private float _currentVignetteIntensity;
    private float _currentVignetteSmoothness = 0.4f;
    private Color _fromVignetteColor = Color.black;
    private float _fromVignetteIntensity;
    private float _fromVignetteSmoothness = 0.4f;
    private Color _targetVignetteColor = Color.black;
    private float _targetVignetteIntensity;
    private float _targetVignetteSmoothness = 0.4f;
    private bool _targetVignetteRounded;
    private float _vignetteTransitionDuration;
    private float _vignetteTransitionProgress = 1f;

    public DungeonPressurePhase CurrentPhase => _lastPhase;
    public Light2D GlobalLight => _globalLight;
    public Volume AmbientVolume => _volume;
    public Vignette VignetteComponent => _vignette;
    public DungeonAmbientConfig Config => _config;
    public DungeonPressureController PressureController => _pressureController;

    public Color CurrentVignetteColor => _currentVignetteColor;
    public float CurrentVignetteIntensity => _currentVignetteIntensity;
    public float CurrentVignetteSmoothness => _currentVignetteSmoothness;
    public bool CurrentVignetteRounded => _targetVignetteRounded;

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

    public void Configure(
        DungeonPressureController controller,
        DungeonAmbientConfig config,
        Light2D globalLight = null,
        Volume volume = null)
    {
        _pressureController = controller;
        _config = config;
        if (globalLight != null)
        {
            _globalLight = globalLight;
        }
        if (volume != null)
        {
            _volume = volume;
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

        if (_volume == null)
        {
            _volume = GetComponent<Volume>();
            if (_volume == null)
            {
                _volume = Object.FindAnyObjectByType<Volume>();
            }
        }

        InitializeVignette();

        if (_globalLight != null && !_hasResolvedInitialPhase)
        {
            _currentGlobalColor = _globalLight.color;
            _currentGlobalIntensity = _globalLight.intensity;
            _targetGlobalColor = _currentGlobalColor;
            _targetGlobalIntensity = _currentGlobalIntensity;
        }
    }

    private void InitializeVignette()
    {
        if (_volume == null)
        {
            return;
        }

        VolumeProfile profile = Application.isPlaying ? _volume.profile : _volume.sharedProfile;
        if (profile != null)
        {
            if (!profile.TryGet(out _vignette))
            {
                _vignette = profile.Add<Vignette>(overrides: true);
            }
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

        // Snap post-process vignette immediately
        _targetVignetteColor = settings.VignetteColor;
        _targetVignetteIntensity = settings.VignetteIntensity;
        _targetVignetteSmoothness = settings.VignetteSmoothness;
        _targetVignetteRounded = settings.VignetteRounded;

        _currentVignetteColor = _targetVignetteColor;
        _currentVignetteIntensity = _targetVignetteIntensity;
        _currentVignetteSmoothness = _targetVignetteSmoothness;
        _vignetteTransitionProgress = 1f;

        ApplyVignetteToVolume();

        // Notify props immediately
        DungeonLightPropRegistry.NotifyPhaseChanged(phase, immediate: true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

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
        UpdateVignetteInterpolation(deltaTime);
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

        // Setup smooth transition for post-process vignette
        _targetVignetteColor = settings.VignetteColor;
        _targetVignetteIntensity = settings.VignetteIntensity;
        _targetVignetteSmoothness = settings.VignetteSmoothness;
        _targetVignetteRounded = settings.VignetteRounded;

        float vignetteDuration = settings.VignetteTransitionDuration > 0f
            ? settings.VignetteTransitionDuration
            : settings.GlobalTransitionDuration;

        _vignetteTransitionDuration = vignetteDuration;
        _fromVignetteColor = _currentVignetteColor;
        _fromVignetteIntensity = _currentVignetteIntensity;
        _fromVignetteSmoothness = _currentVignetteSmoothness;

        if (_vignetteTransitionDuration <= 0f)
        {
            _currentVignetteColor = _targetVignetteColor;
            _currentVignetteIntensity = _targetVignetteIntensity;
            _currentVignetteSmoothness = _targetVignetteSmoothness;
            _vignetteTransitionProgress = 1f;
            ApplyVignetteToVolume();
        }
        else
        {
            _vignetteTransitionProgress = 0f;
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

    private void UpdateVignetteInterpolation(float deltaTime)
    {
        if (_vignetteTransitionProgress >= 1f)
        {
            return;
        }

        float rate = _vignetteTransitionDuration > 0.001f ? (deltaTime / _vignetteTransitionDuration) : 1f;
        _vignetteTransitionProgress = Mathf.Clamp01(_vignetteTransitionProgress + rate);

        _currentVignetteColor = Color.Lerp(_fromVignetteColor, _targetVignetteColor, _vignetteTransitionProgress);
        _currentVignetteIntensity = Mathf.Lerp(_fromVignetteIntensity, _targetVignetteIntensity, _vignetteTransitionProgress);
        _currentVignetteSmoothness = Mathf.Lerp(_fromVignetteSmoothness, _targetVignetteSmoothness, _vignetteTransitionProgress);

        ApplyVignetteToVolume();
    }

    private void ApplyVignetteToVolume()
    {
        if (_vignette == null)
        {
            InitializeVignette();
            if (_vignette == null)
            {
                return;
            }
        }

        bool active = _currentVignetteIntensity > 0.001f || _targetVignetteIntensity > 0.001f;
        _vignette.active = active;
        _vignette.color.overrideState = active;
        _vignette.color.value = _currentVignetteColor;

        _vignette.intensity.overrideState = active;
        _vignette.intensity.value = _currentVignetteIntensity;

        _vignette.smoothness.overrideState = active;
        _vignette.smoothness.value = _currentVignetteSmoothness;

        _vignette.rounded.overrideState = active;
        _vignette.rounded.value = _targetVignetteRounded;
    }

    public void ResetPresentation()
    {
        if (CameraShakeController.Instance != null)
        {
            CameraShakeController.Instance.SetContinuousShake(0f);
        }

        _currentVignetteIntensity = 0f;
        _targetVignetteIntensity = 0f;
        _vignetteTransitionProgress = 1f;
        ApplyVignetteToVolume();

        DungeonLightPropRegistry.Clear();
        _periodicShakeTimer = 0f;
        _hasResolvedInitialPhase = false;
    }

#if UNITY_EDITOR
    [ContextMenu("Preview/Phase: Normal")]
    public void PreviewNormal() => PreviewPhase(DungeonPressurePhase.Normal);

    [ContextMenu("Preview/Phase: Reinforcements")]
    public void PreviewReinforcements() => PreviewPhase(DungeonPressurePhase.Reinforcements);

    [ContextMenu("Preview/Phase: Critical Pressure")]
    public void PreviewCriticalPressure() => PreviewPhase(DungeonPressurePhase.CriticalPressure);

    [ContextMenu("Preview/Phase: Collapse")]
    public void PreviewCollapse() => PreviewPhase(DungeonPressurePhase.Collapse);

    [ContextMenu("Preview/Clear Preview")]
    public void ClearPreview()
    {
        InitializeComponents();
        if (_config != null)
        {
            ApplyInitialPhase(DungeonPressurePhase.Normal);
        }
        ResetPresentation();
        UnityEditor.SceneView.RepaintAll();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }

    public void PreviewPhase(DungeonPressurePhase phase)
    {
        InitializeComponents();
        ApplyInitialPhase(phase);
        UnityEditor.SceneView.RepaintAll();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif
}
