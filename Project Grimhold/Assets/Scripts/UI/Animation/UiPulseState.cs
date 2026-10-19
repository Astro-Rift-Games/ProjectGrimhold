using System;

/// <summary>
/// One-shot feedback pulse: a scale punch for success and a decaying horizontal shake for rejection.
/// Pure math, no Unity state; neutral values are scale 1 and offset 0.
/// </summary>
public sealed class UiPulseState
{
    private const int ShakeCycles = 3;

    private UiPulseKind _kind;
    private float _elapsed;
    private float _scaleAmplitude;
    private float _shakeAmplitude;

    public bool IsPlaying { get; private set; }
    public float Scale { get; private set; } = 1f;
    public float OffsetX { get; private set; }

    public void Play(UiPulseKind kind, float scaleAmplitude = 0.12f, float shakeAmplitude = 8f)
    {
        _kind = kind;
        _elapsed = 0f;
        _scaleAmplitude = scaleAmplitude;
        _shakeAmplitude = shakeAmplitude;
        IsPlaying = true;
        Scale = 1f;
        OffsetX = 0f;
    }

    public void Advance(float deltaTime, float duration)
    {
        if (!IsPlaying)
        {
            return;
        }

        _elapsed += deltaTime;
        float t = duration > 0f ? _elapsed / duration : 1f;
        if (t >= 1f)
        {
            IsPlaying = false;
            Scale = 1f;
            OffsetX = 0f;
            return;
        }

        float decay = 1f - t;
        if (_kind == UiPulseKind.Success)
        {
            Scale = 1f + _scaleAmplitude * (float)Math.Sin(Math.PI * t) * decay;
            OffsetX = 0f;
        }
        else
        {
            Scale = 1f;
            OffsetX = _shakeAmplitude * (float)Math.Sin(2.0 * Math.PI * ShakeCycles * t) * decay;
        }
    }
}
