using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls the appearance, color, intensity, and temporal effect pattern (flicker, pulse)
/// of a 2D light prop (e.g. torch, brazier, candle) across dungeon pressure phases.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D))]
public sealed class DungeonLightProp : MonoBehaviour
{
    [Tooltip("The 2D light component to modulate. If null, automatically resolves from this GameObject.")]
    [SerializeField] private Light2D _light;

    [Tooltip("Configuration asset defining colors, intensities, and patterns per phase.")]
    [SerializeField] private DungeonLightPropConfig _config;

    private Color _currentColor = Color.white;
    private float _currentIntensity = 1f;

    private Color _fromColor = Color.white;
    private float _fromIntensity = 1f;
    private Color _targetColor = Color.white;
    private float _targetIntensity = 1f;

    private float _transitionDuration;
    private float _transitionProgress = 1f;
    private LightEffectPattern _currentPattern = LightEffectPattern.Steady;

    private float _noiseSeedX;
    private float _noiseSeedY;
    private bool _isInitialized;

    public Light2D LightComponent => _light;
    public DungeonLightPropConfig Config => _config;
    public LightEffectPattern CurrentPattern => _currentPattern;
    public Color CurrentBaseColor => _currentColor;
    public float CurrentBaseIntensity => _currentIntensity;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        DungeonLightPropRegistry.Register(this);
    }

    private void OnDisable()
    {
        DungeonLightPropRegistry.Unregister(this);
    }

    public void Configure(DungeonLightPropConfig config, Light2D light = null)
    {
        if (light != null)
        {
            _light = light;
        }

        _config = config;
        _isInitialized = false;
        InitializeIfNeeded();

        ApplyPhase(DungeonLightPropRegistry.CurrentPhase, immediate: true);
    }

    private void InitializeIfNeeded()
    {
        if (_isInitialized)
        {
            return;
        }

        if (_light == null)
        {
            _light = GetComponent<Light2D>();
        }

        // Independent noise offsets avoid multiple lights in the same room flickering in sync
        _noiseSeedX = Random.Range(0f, 1000f);
        _noiseSeedY = Random.Range(0f, 1000f);

        if (_light != null)
        {
            _currentColor = _light.color;
            _currentIntensity = _light.intensity;
            _targetColor = _currentColor;
            _targetIntensity = _currentIntensity;
        }

        if (_config != null)
        {
            ApplyPhase(DungeonLightPropRegistry.CurrentPhase, immediate: true);
        }

        _isInitialized = true;
    }

    public void ApplyPhase(DungeonPressurePhase phase, bool immediate)
    {
        if (_config == null)
        {
            return;
        }

        DungeonLightPropPhaseSettings settings = _config.GetSettings(phase);

        _targetColor = settings.Color;
        _targetIntensity = settings.Intensity;
        _currentPattern = settings.Pattern;
        _transitionDuration = settings.TransitionDuration;

        if (immediate || _transitionDuration <= 0f)
        {
            _currentColor = _targetColor;
            _currentIntensity = _targetIntensity;
            _fromColor = _targetColor;
            _fromIntensity = _targetIntensity;
            _transitionProgress = 1f;
        }
        else
        {
            _fromColor = _currentColor;
            _fromIntensity = _currentIntensity;
            _transitionProgress = 0f;
        }
    }

    private void Update()
    {
        if (_light == null)
        {
            return;
        }

        // Advance transition interpolation
        if (_transitionProgress < 1f)
        {
            float rate = _transitionDuration > 0.001f ? (Time.deltaTime / _transitionDuration) : 1f;
            _transitionProgress = Mathf.Clamp01(_transitionProgress + rate);
            _currentColor = Color.Lerp(_fromColor, _targetColor, _transitionProgress);
            _currentIntensity = Mathf.Lerp(_fromIntensity, _targetIntensity, _transitionProgress);
        }

        // Evaluate temporal effect pattern
        EvaluatePattern();
    }

    private void EvaluatePattern()
    {
        float time = Time.time;
        float finalIntensity = _currentIntensity;

        switch (_currentPattern)
        {
            case LightEffectPattern.Steady:
                // Constant illumination
                break;

            case LightEffectPattern.TorchFlicker:
                // Gentle organic flame flicker (+/- 8% variation)
                float torchNoise = Mathf.PerlinNoise(_noiseSeedX + time * 6f, _noiseSeedY);
                finalIntensity *= 1f + (torchNoise - 0.5f) * 0.16f;
                break;

            case LightEffectPattern.UnstableFlicker:
                // Agitated flicker with rapid micro-drops simulating wind/instability
                float fastNoise = Mathf.PerlinNoise(_noiseSeedX + time * 20f, 0f);
                float slowNoise = Mathf.PerlinNoise(0f, _noiseSeedY + time * 4f);
                float drop = fastNoise < 0.18f ? 0.35f : 1f;
                finalIntensity *= drop * (0.8f + slowNoise * 0.4f);
                break;

            case LightEffectPattern.Pulse:
                // Smooth sinusoidal pulsation
                float pulse = 0.8f + 0.2f * Mathf.Sin(time * 3f + _noiseSeedX);
                finalIntensity *= pulse;
                break;
        }

        _light.color = _currentColor;
        _light.intensity = Mathf.Max(0f, finalIntensity);
    }
}
