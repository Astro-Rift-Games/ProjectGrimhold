using System;
using UnityEngine;

/// <summary>
/// Local resolved statistics consumed by one attack executor.
/// Equipment derives this locally. An accepted ranged shot copies only required values into
/// its network snapshot; the mutable executor and shared configuration are never replicated.
/// </summary>
[Serializable]
public struct AttackExecutionParameters
{
    [SerializeField, Min(0f)] private float _damage;
    [SerializeField] private DamageType _damageType;
    [SerializeField, Min(0f)] private float _cooldownSeconds;
    [SerializeField, Min(0f)] private float _range;
    [SerializeField, Min(0f)] private float _knockbackForce;

    [SerializeField, Min(0f)] private float _releaseDelaySeconds;

    public float ReleaseDelaySeconds => _releaseDelaySeconds;
    public float Damage => _damage;
    public DamageType DamageType => _damageType;
    public float CooldownSeconds => _cooldownSeconds;
    public float Range => _range;
    public float KnockbackForce => _knockbackForce;

    public AttackExecutionParameters(
        float damage,
        DamageType damageType,
        float cooldownSeconds,
        float range,
        float knockbackForce,
        float releaseDelaySeconds = 0f)
    {
        _damage = damage;
        _damageType = damageType;
        _cooldownSeconds = cooldownSeconds;
        _range = range;
        _knockbackForce = knockbackForce;
        _releaseDelaySeconds = releaseDelaySeconds;
    }

    public bool TryValidate(out string error)
    {
        if (!IsFinite(_releaseDelaySeconds) || _releaseDelaySeconds < 0f)
        {
            error = "Release delay must be finite and non-negative.";
            return false;
        }

        if (!IsFinite(_damage) || _damage <= 0f)
        {
            error = $"{nameof(Damage)} must be finite and greater than zero (current: {_damage}).";
            return false;
        }

        if (!IsFinite(_cooldownSeconds) || _cooldownSeconds < 0f)
        {
            error = $"{nameof(CooldownSeconds)} must be finite and non-negative (current: {_cooldownSeconds}).";
            return false;
        }

        if (!Enum.IsDefined(typeof(DamageType), _damageType))
        {
            error = $"{nameof(DamageType)} has an unsupported value (current: {(int)_damageType}).";
            return false;
        }

        if (!IsFinite(_range) || _range <= 0f)
        {
            error = $"{nameof(Range)} must be finite and greater than zero (current: {_range}).";
            return false;
        }

        if (!IsFinite(_knockbackForce) || _knockbackForce < 0f)
        {
            error = $"{nameof(KnockbackForce)} must be finite and non-negative (current: {_knockbackForce}).";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
