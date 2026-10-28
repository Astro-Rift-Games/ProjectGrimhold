using System;
using UnityEngine;

/// <summary>Deterministic knockback conversion; values are placeholders pending balance (Doc 18 §9).</summary>
[CreateAssetMenu(menuName = "Grimhold/Combat/Knockback Curve Config")]
public sealed class KnockbackCurveConfig : ScriptableObject
{
    [SerializeField, Min(0)] private float _distancePerEffectivePower = 0.1f;
    [SerializeField, Min(0)] private float _maxDistance = 3f;
    [SerializeField, Min(0)] private float _durationSecondsPerEffectivePower = 0.03f;
    [SerializeField, Min(0)] private float _minDurationSeconds = 0.1f;
    [SerializeField, Min(0)] private float _maxDurationSeconds = 0.5f;
    [SerializeField] private AnimationCurve _displacementProfile = new AnimationCurve(
        new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0));

    /// <summary>Converts positive effective PE to capped distance and a whole-tick duration.</summary>
    public bool TryResolveProfile(float effectivePower, float tickDeltaTime, out float distance, out int durationTicks)
    {
        distance = 0;
        durationTicks = 0;
        if (!IsFinite(effectivePower) || effectivePower <= 0 ||
            !IsFinite(tickDeltaTime) || tickDeltaTime <= 0 || !HasValidScalars() ||
            _displacementProfile == null || _displacementProfile.length < 2)
        {
            return false;
        }

        // Double intermediates avoid overflow for otherwise finite power and delta inputs.
        double seconds = Math.Max(_minDurationSeconds,
            Math.Min(_maxDurationSeconds, (double)effectivePower * _durationSecondsPerEffectivePower));
        // Tolerate float tick-delta representation error at exact tick boundaries.
        double ticks = Math.Max(1, Math.Ceiling(seconds / (double)tickDeltaTime - 1e-6));
        if (ticks > int.MaxValue)
        {
            return false;
        }
        distance = (float)Math.Min(_maxDistance, (double)effectivePower * _distancePerEffectivePower);
        durationTicks = (int)ticks;
        return true;
    }

    /// <summary>Evaluates cumulative displacement with exact endpoints and clamped progress.</summary>
    public float EvaluateProgress(float normalizedTime)
    {
        if (float.IsNaN(normalizedTime) || normalizedTime <= 0)
        {
            return 0;
        }
        if (normalizedTime >= 1)
        {
            return 1;
        }
        if (_displacementProfile == null)
        {
            return 0;
        }
        float progress = _displacementProfile.Evaluate(normalizedTime);
        return IsFinite(progress) ? Mathf.Clamp01(progress) : 0;
    }

    /// <summary>Validates finite conversion settings and normalized profile keys.</summary>
    public bool TryValidate(out string error)
    {
        if (!HasValidScalars())
        {
            error = "Knockback conversion values must be finite and nonnegative, with ordered duration bounds.";
            return false;
        }
        if (_displacementProfile == null || _displacementProfile.length < 2)
        {
            error = "Knockback requires a cumulative displacement profile with at least two keys.";
            return false;
        }
        for (int i = 0; i < _displacementProfile.length; i++)
        {
            Keyframe key = _displacementProfile[i];
            if (!IsFinite(key.time) || !IsFinite(key.value) || key.value < 0 || key.value > 1)
            {
                error = "Knockback profile keys must have finite times and normalized values.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private bool HasValidScalars()
        => IsNonnegative(_distancePerEffectivePower) && IsNonnegative(_maxDistance) &&
           IsNonnegative(_durationSecondsPerEffectivePower) && IsNonnegative(_minDurationSeconds) &&
           IsNonnegative(_maxDurationSeconds) && _minDurationSeconds <= _maxDurationSeconds;

    private static bool IsNonnegative(float value) => IsFinite(value) && value >= 0;
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
