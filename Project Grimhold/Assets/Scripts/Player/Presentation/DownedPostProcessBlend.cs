using UnityEngine;

/// <summary>
/// Pure, frame-driven evaluation of the local Downed black-and-white post-process. Holds only
/// presentation state (transition progress, smoothed reserve and revive progress) and never reads
/// or writes simulation. The presenter feeds it replicated Downed facts each frame.
/// </summary>
public sealed class DownedPostProcessBlend
{
    private readonly DownedPostProcessConfig _config;

    private float _transition;
    private float _reserve = 1f;
    private float _revive;

    public DownedPostProcessBlend(DownedPostProcessConfig config)
    {
        _config = config;
    }

    /// <summary>Drops the effect at once; used on unbind, session teardown and rebind.</summary>
    public void Reset()
    {
        _transition = 0f;
        _reserve = 1f;
        _revive = 0f;
    }

    /// <summary>
    /// Advances one frame. While Downed the reserve and revive progress are followed smoothly; once
    /// Downed ends (revive or definitive Defeat) they stay latched so the fade-out does not darken
    /// on the cleared reserve or lose the recovered colour abruptly. Invalid delta time changes nothing.
    /// </summary>
    public DownedPostProcessFrame Step(
        bool isDowned,
        float reserveFraction,
        bool isReviving,
        float reviveProgress,
        float deltaTime)
    {
        if (IsValidDelta(deltaTime))
        {
            Advance(isDowned, reserveFraction, isReviving, reviveProgress, deltaTime);
        }

        return Evaluate();
    }

    private void Advance(bool isDowned, float reserveFraction, bool isReviving, float reviveProgress, float deltaTime)
    {
        bool entering = isDowned && _transition <= 0f;
        float duration = isDowned ? _config.FadeInSeconds : _config.FadeOutSeconds;
        _transition = Mathf.MoveTowards(_transition, isDowned ? 1f : 0f, deltaTime / duration);

        if (!isDowned)
        {
            return;
        }

        float reserveTarget = Clamp01Finite(reserveFraction, _reserve);
        // A new cycle starts from its own reserve instead of easing from the previous cycle.
        _reserve = entering
            ? reserveTarget
            : Mathf.MoveTowards(_reserve, reserveTarget, deltaTime / _config.ReserveResponseSeconds);

        float reviveTarget = isReviving ? Clamp01Finite(reviveProgress, 0f) : 0f;
        _revive = Mathf.MoveTowards(_revive, reviveTarget, deltaTime / _config.ReviveResponseSeconds);
    }

    private DownedPostProcessFrame Evaluate()
    {
        float depletion = 1f - _reserve;
        return new DownedPostProcessFrame(
            EvaluateWeight(_transition),
            _config.Saturation * (1f - _revive * _config.ReviveColorRecovery),
            _config.Contrast,
            -depletion * _config.DepletedExposureDim,
            _config.VignetteColor,
            Mathf.Clamp01(_config.VignetteIntensity + depletion * _config.DepletedVignetteBoost),
            _config.VignetteSmoothness);
    }

    private float EvaluateWeight(float transition)
    {
        if (transition <= 0f)
        {
            return 0f;
        }

        if (transition >= 1f)
        {
            return 1f;
        }

        AnimationCurve curve = _config.BlendCurve;
        return curve != null && curve.length > 0 ? Mathf.Clamp01(curve.Evaluate(transition)) : transition;
    }

    private static bool IsValidDelta(float deltaTime) =>
        !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime) && deltaTime >= 0f;

    private static float Clamp01Finite(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
}
