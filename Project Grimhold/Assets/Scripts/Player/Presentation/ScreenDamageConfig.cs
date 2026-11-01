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
