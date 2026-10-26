using System;
using UnityEngine;

/// <summary>
/// Ambient visual, post-process vignette, camera-shake, and particle settings for the entire dungeon during a specific pressure phase.
/// </summary>
[Serializable]
public struct DungeonAmbientPhaseSettings
{
    [Header("Global 2D Lighting")]
    [Tooltip("Target color for the scene's global 2D light during this phase.")]
    [SerializeField] private Color _globalLightColor;

    [Tooltip("Target intensity for the scene's global 2D light during this phase.")]
    [Min(0f)]
    [SerializeField] private float _globalLightIntensity;

    [Tooltip("Duration in seconds to transition global light from the previous phase.")]
    [Min(0f)]
    [SerializeField] private float _globalTransitionDuration;

    [Header("Camera Shake - Phase Entry")]
    [Tooltip("Instantaneous shake trauma (0-1) requested when entering this phase.")]
    [Range(0f, 1f)]
    [SerializeField] private float _entryShakeIntensity;

    [Tooltip("Duration in seconds for the entry shake to decay.")]
    [Min(0f)]
    [SerializeField] private float _entryShakeDuration;

    [Header("Camera Shake - Continuous Sustained")]
    [Tooltip("Continuous sustained trauma floor (0-1) held while in this phase (e.g. constant subtle tremor).")]
    [Range(0f, 1f)]
    [SerializeField] private float _continuousShakeIntensity;

    [Header("Camera Shake - Periodic Shakes")]
    [Tooltip("Instantaneous shake trauma (0-1) applied periodically during this phase.")]
    [Range(0f, 1f)]
    [SerializeField] private float _periodicShakeIntensity;

    [Tooltip("Duration in seconds of each periodic shake.")]
    [Min(0f)]
    [SerializeField] private float _periodicShakeDuration;

    [Tooltip("Interval in seconds between periodic shakes during this phase. Values <= 0 disable periodic shakes.")]
    [Min(0f)]
    [SerializeField] private float _periodicShakeIntervalSeconds;

    [Tooltip("Optional sound effect played when a periodic shake triggers (e.g. subterranean rumble or distant cave-in).")]
    [SerializeField] private CustomClip _periodicShakeSfx;

    [Header("Falling Dust & Debris Particles")]
    [Tooltip("Emission rate (particles/sec) of ambient falling ceiling dust during this phase.")]
    [Min(0f)]
    [SerializeField] private float _dustEmissionRate;

    [Tooltip("Emission rate (particles/sec) of falling rock debris/pebbles during this phase.")]
    [Min(0f)]
    [SerializeField] private float _debrisEmissionRate;

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
    public CustomClip PeriodicShakeSfx => _periodicShakeSfx;
    public float DustEmissionRate => _dustEmissionRate;
    public float DebrisEmissionRate => _debrisEmissionRate;
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
        float vignetteTransitionDuration = 0f,
        CustomClip periodicShakeSfx = default,
        float dustEmissionRate = 0f,
        float debrisEmissionRate = 0f)
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
        _periodicShakeSfx = periodicShakeSfx;

        _dustEmissionRate = Mathf.Max(0f, dustEmissionRate);
        _debrisEmissionRate = Mathf.Max(0f, debrisEmissionRate);

        _vignetteColor = vignetteColor;
        _vignetteIntensity = Mathf.Clamp01(vignetteIntensity);
        _vignetteSmoothness = Mathf.Clamp(vignetteSmoothness, 0.01f, 1f);
        _vignetteRounded = vignetteRounded;
        _vignetteTransitionDuration = Mathf.Max(0f, vignetteTransitionDuration);
    }
}
