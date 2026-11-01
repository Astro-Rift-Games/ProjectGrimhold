using UnityEngine;

[CreateAssetMenu(fileName = "ScreenDamageConfig", menuName = "Grimhold/Presentation/Screen Damage")]
public sealed class ScreenDamageConfig : ScriptableObject
{
    [SerializeField, Range(0f, 1f)] private float _healthyThreshold = .7f;
    [SerializeField, Range(0f, 1f)] private float _criticalThreshold = .4f;
    [SerializeField, Range(0f, 1f)] private float _agonyThreshold = .2f;
    [SerializeField] private AnimationCurve _severityCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField, Min(.01f)] private float _responseSeconds = .18f;
    [SerializeField, Min(.01f)] private float _flashInSeconds = .05f;
    [SerializeField, Min(.01f)] private float _flashOutSeconds = .2f;
    [SerializeField, Range(0f, 1f)] private float _flashStrength = .4f;
    [SerializeField, Range(0f, .3f)] private float _pulseStrength = .12f;
    [SerializeField, Min(.1f)] private float _slowHeartbeatHz = 1f;
    [SerializeField, Min(.1f)] private float _fastHeartbeatHz = 2.5f;
    [SerializeField] private Color _tint = Color.white;

    [Header("Peripheral scene sampling (below agony threshold)")]
    [SerializeField, Range(0f, 3f)] private float _blurPixels = 1.1f;
    [SerializeField, Range(0f, 2f)] private float _chromaticPixels = .65f;
    [Tooltip("Fraction of each screen axis kept clear around the center.")]
    [SerializeField, Range(.5f, .9f)] private float _clearCenter = .6f;
    [Tooltip("Clear radii in fractions of the viewport's shorter dimension.")]
    [SerializeField, Range(.05f, .25f)] private float _avatarClearRadius = .1f;
    [SerializeField, Range(.02f, .15f)] private float _cursorClearRadius = .05f;

    public float BlurPixels => Mathf.Clamp(_blurPixels, 0f, 3f);
    public float ChromaticPixels => Mathf.Clamp(_chromaticPixels, 0f, 2f);
    public float ClearCenter => Mathf.Clamp(_clearCenter, .5f, .9f);
    public float AvatarClearRadius => Mathf.Clamp(_avatarClearRadius, .05f, .25f);
    public float CursorClearRadius => Mathf.Clamp(_cursorClearRadius, .02f, .15f);

    public float HealthyThreshold => Mathf.Clamp(_healthyThreshold, .01f, 1f);
    public float CriticalThreshold => Mathf.Clamp(_criticalThreshold, .001f, HealthyThreshold);
    public float AgonyThreshold => Mathf.Clamp(_agonyThreshold, .001f, CriticalThreshold);
    public float ResponseSeconds => Mathf.Max(.01f, _responseSeconds);
    public float FlashInSeconds => Mathf.Max(.01f, _flashInSeconds);
    public float FlashOutSeconds => Mathf.Max(.01f, _flashOutSeconds);
    public float FlashStrength => Mathf.Clamp01(_flashStrength);
    public float PulseStrength => Mathf.Clamp(_pulseStrength, 0f, .3f);
    public Color Tint => _tint;
    public float Severity(float healthFraction) => Mathf.Clamp01(_severityCurve.Evaluate(
        Mathf.InverseLerp(HealthyThreshold, 0f, healthFraction)));
    public float HeartbeatHz(float healthFraction) => healthFraction >= CriticalThreshold ? 0f :
        Mathf.Lerp(_slowHeartbeatHz, _fastHeartbeatHz, Mathf.InverseLerp(CriticalThreshold, 0f, healthFraction));
}
