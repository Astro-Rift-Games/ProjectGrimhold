using UnityEngine;

/// <summary>Local presentation clock; observes confirmed health, never advances gameplay.</summary>
public sealed class ScreenDamageFeedbackState
{
    private readonly ScreenDamageConfig _config;
    private bool _baseline;
    private float _health, _maximum, _severity, _phase;
    private float _flashAge = float.PositiveInfinity;

    public float Severity { get; private set; }
    public float Opacity { get; private set; }
    public float Flash { get; private set; }

    public ScreenDamageFeedbackState(ScreenDamageConfig config) => _config = config;

    public void Step(float health, float maximum, bool active, float deltaTime, float intensity)
    {
        if (!active || !Finite(health) || !Finite(maximum) || maximum <= 0f || health <= 0f)
        {
            Reset();
            return;
        }
        float dt = Finite(deltaTime) ? Mathf.Max(0, deltaTime) : 0;
        // A maximum-health change is not an impact. Late join/rebind starts a silent baseline.
        if (_baseline && health < _health && Mathf.Approximately(maximum, _maximum)) _flashAge = 0;
        else _flashAge += dt;
        _health = health;
        _maximum = maximum;
        _baseline = true;
        float fraction = Mathf.Clamp01(health / maximum);
        float target = _config.Severity(fraction);
        _severity = Mathf.Lerp(_severity, target, 1f - Mathf.Exp(-dt / _config.ResponseSeconds));
        float hz = _config.HeartbeatHz(fraction);
        _phase = Mathf.Repeat(_phase + dt * hz, 1f);
        Flash = _flashAge < _config.FlashInSeconds ? _flashAge / _config.FlashInSeconds :
            Mathf.Clamp01(1f - (_flashAge - _config.FlashInSeconds) / _config.FlashOutSeconds);
        // Smooth onset at 40%; a heartbeat only modulates existing low-health spray.
        float pulse = hz > 0 ? Mathf.InverseLerp(_config.CriticalThreshold, _config.AgonyThreshold, fraction) *
            _config.PulseStrength * (.5f + .5f * Mathf.Cos(_phase * Mathf.PI * 2f)) : 0f;
        Severity = Mathf.Clamp01(_severity + Flash * _config.FlashStrength);
        float strength = Finite(intensity) ? Mathf.Clamp01(intensity) : 0f;
        Opacity = Mathf.Clamp01((1f - pulse) * strength);
        if (Severity < .0001f) Opacity = 0f;
    }

    public void Reset()
    {
        _baseline = false;
        _health = _maximum = _severity = _phase = Severity = Opacity = Flash = 0;
        _flashAge = float.PositiveInfinity;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
