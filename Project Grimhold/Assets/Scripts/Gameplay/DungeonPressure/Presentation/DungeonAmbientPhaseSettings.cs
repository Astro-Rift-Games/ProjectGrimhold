using System;
using UnityEngine;

/// <summary>
/// Ambient visual, post-process vignette, and camera-shake settings for the entire dungeon during a specific pressure phase.
/// </summary>
[Serializable]
public struct DungeonAmbientPhaseSettings
{
    [Header("Global Ambient Light")]
    [Tooltip("Target color for the global ambient light during this phase.")]
    [SerializeField] private Color _globalLightColor;

    [Tooltip("Target intensity for the global ambient light during this phase.")]
    [Min(0f)]
    [SerializeField] private float _globalLightIntensity;

    [Tooltip("Duration in seconds to smoothly transition global light from the previous phase.")]
    [Min(0f)]
    [SerializeField] private float _globalTransitionDuration;

    [Header("Camera Shake - Entry")]
    [Tooltip("Instantaneous camera shake trauma requested upon entering this phase.")]
    [Range(0f, 1f)]
    [SerializeField] private float _entryShakeIntensity;

    [Tooltip("Duration in seconds over which entry shake trauma decays.")]
    [Min(0f)]
    [SerializeField] private float _entryShakeDuration;

    [Header("Camera Shake - Continuous")]
    [Tooltip("Sustained baseline trauma floor maintained throughout this phase (e.g. constant tremor during Collapse).")]
    [Range(0f, 1f)]
    [SerializeField] private float _continuousShakeIntensity;

    [Header("Camera Shake - Periodic")]
    [Tooltip("Periodic camera shake trauma triggered at regular intervals during this phase. 0 to disable.")]
    [Range(0f, 1f)]
    [SerializeField] private float _periodicShakeIntensity;

    [Tooltip("Duration in seconds of each periodic shake.")]
    [Min(0f)]
    [SerializeField] private float _periodicShakeDuration;

    [Tooltip("Interval in seconds between periodic shakes during this phase. Values <= 0 disable periodic shakes.")]
    [Min(0f)]
    [SerializeField] private float _periodicShakeIntervalSeconds;

    [Header("Vignette Post-Process (URP Volume)")]
    [Tooltip("Target color for the vignette overlay during this phase (e.g. black or deep crimson).")]
    [SerializeField] private Color _vignetteColor;

    [Tooltip("Target intensity for the vignette effect during this phase (0 = disabled/transparent, 1 = maximum edge darkness).")]
    [Range(0f, 1f)]
    [SerializeField] private float _vignetteIntensity;

    [Tooltip("Smoothness of the falloff transition from screen edges toward center (0.01 = sharp edge, 1 = wide translucent gradient).")]
    [Range(0.01f, 1f)]
    [SerializeField] private float _vignetteSmoothness;

    [Tooltip("Whether the vignette is circular (true) or oval fitting screen aspect ratio (false).")]
    [SerializeField] private bool _vignetteRounded;

    [Tooltip("Duration in seconds to smoothly transition vignette parameters to this phase. If <= 0, uses global transition duration.")]
    [Min(0f)]
    [SerializeField] private float _vignetteTransitionDuration;

    public Color GlobalLightColor => _globalLightColor;
    public float GlobalLightIntensity => _globalLightIntensity;
    public float GlobalTransitionDuration => _globalTransitionDuration;
    public float EntryShakeIntensity => _entryShakeIntensity;
    public float EntryShakeDuration => _entryShakeDuration;
    public float ContinuousShakeIntensity => _continuousShakeIntensity;
    public float PeriodicShakeIntensity => _periodicShakeIntensity;
    public float PeriodicShakeDuration => _periodicShakeDuration;
    public float PeriodicShakeIntervalSeconds => _periodicShakeIntervalSeconds;
    public Color VignetteColor => _vignetteColor;
    public float VignetteIntensity => _vignetteIntensity;
    public float VignetteSmoothness => _vignetteSmoothness;
    public bool VignetteRounded => _vignetteRounded;
    public float VignetteTransitionDuration => _vignetteTransitionDuration;

    public DungeonAmbientPhaseSettings(
        Color globalLightColor,
        float globalLightIntensity,
        float globalTransitionDuration,
        float entryShakeIntensity,
        float entryShakeDuration,
        float continuousShakeIntensity)
        : this(globalLightColor, globalLightIntensity, globalTransitionDuration, entryShakeIntensity, entryShakeDuration, continuousShakeIntensity, 0f, 0f, 0f)
    {
    }

    public DungeonAmbientPhaseSettings(
        Color globalLightColor,
        float globalLightIntensity,
        float globalTransitionDuration,
        float entryShakeIntensity,
        float entryShakeDuration,
        float continuousShakeIntensity,
        float periodicShakeIntensity,
        float periodicShakeDuration,
        float periodicShakeIntervalSeconds)
        : this(
            globalLightColor,
            globalLightIntensity,
            globalTransitionDuration,
            entryShakeIntensity,
            entryShakeDuration,
            continuousShakeIntensity,
            periodicShakeIntensity,
            periodicShakeDuration,
            periodicShakeIntervalSeconds,
            Color.black,
            0f,
            0.4f,
            false,
            0f)
    {
    }

    public DungeonAmbientPhaseSettings(
        Color globalLightColor,
        float globalLightIntensity,
        float globalTransitionDuration,
        float entryShakeIntensity,
        float entryShakeDuration,
        float continuousShakeIntensity,
        float periodicShakeIntensity,
        float periodicShakeDuration,
        float periodicShakeIntervalSeconds,
        Color vignetteColor,
        float vignetteIntensity,
        float vignetteSmoothness = 0.4f,
        bool vignetteRounded = false,
        float vignetteTransitionDuration = 0f)
    {
        _globalLightColor = globalLightColor;
        _globalLightIntensity = Mathf.Max(0f, globalLightIntensity);
        _globalTransitionDuration = Mathf.Max(0f, globalTransitionDuration);
        _entryShakeIntensity = Mathf.Clamp01(entryShakeIntensity);
        _entryShakeDuration = Mathf.Max(0f, entryShakeDuration);
        _continuousShakeIntensity = Mathf.Clamp01(continuousShakeIntensity);
        _periodicShakeIntensity = Mathf.Clamp01(periodicShakeIntensity);
        _periodicShakeDuration = Mathf.Max(0f, periodicShakeDuration);
        _periodicShakeIntervalSeconds = Mathf.Max(0f, periodicShakeIntervalSeconds);

        _vignetteColor = vignetteColor;
        _vignetteIntensity = Mathf.Clamp01(vignetteIntensity);
        _vignetteSmoothness = Mathf.Clamp(vignetteSmoothness, 0.01f, 1f);
        _vignetteRounded = vignetteRounded;
        _vignetteTransitionDuration = Mathf.Max(0f, vignetteTransitionDuration);
    }
}
