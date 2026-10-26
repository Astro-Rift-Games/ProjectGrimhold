using System;
using UnityEngine;

/// <summary>
/// Illumination and effect settings for a light prop during a specific dungeon pressure phase.
/// </summary>
[Serializable]
public struct DungeonLightPropPhaseSettings
{
    [Tooltip("Target color for the prop light during this phase.")]
    [SerializeField] private Color _color;

    [Tooltip("Target intensity for the prop light during this phase.")]
    [Min(0f)]
    [SerializeField] private float _intensity;

    [Tooltip("Effect pattern applied to the prop light (e.g. steady, torch flicker, pulse).")]
    [SerializeField] private LightEffectPattern _pattern;

    [Tooltip("Duration in seconds to transition from the previous phase.")]
    [Min(0f)]
    [SerializeField] private float _transitionDuration;

    [Tooltip("Whether the light source is extinguished during this phase (e.g. blown out by drafts or cave-in).")]
    [SerializeField] private bool _isExtinguished;

    public Color Color => _color;
    public float Intensity => _intensity;
    public LightEffectPattern Pattern => _pattern;
    public float TransitionDuration => _transitionDuration;
    public bool IsExtinguished => _isExtinguished;

    public DungeonLightPropPhaseSettings(Color color, float intensity, LightEffectPattern pattern, float transitionDuration)
        : this(color, intensity, pattern, transitionDuration, isExtinguished: false)
    {
    }

    public DungeonLightPropPhaseSettings(Color color, float intensity, LightEffectPattern pattern, float transitionDuration, bool isExtinguished)
    {
        _color = color;
        _intensity = Mathf.Max(0f, intensity);
        _pattern = pattern;
        _transitionDuration = Mathf.Max(0f, transitionDuration);
        _isExtinguished = isExtinguished;
    }
}
