using System;
using UnityEngine;

/// <summary>
/// Local resolved statistics consumed by one attack executor.
/// This value is runtime/configuration data only and is never replicated or persisted.
/// </summary>
[Serializable]
public struct AttackExecutionParameters
{
    [SerializeField, Min(0f)] private float _damage;
    [SerializeField] private DamageType _damageType;
    [SerializeField, Min(0f)] private float _cooldownSeconds;
    [SerializeField, Min(0f)] private float _range;
    [SerializeField, Min(0f)] private float _knockbackForce;

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
        float knockbackForce)
    {
        _damage = damage;
        _damageType = damageType;
        _cooldownSeconds = cooldownSeconds;
        _range = range;
        _knockbackForce = knockbackForce;
    }

    public bool TryValidate(out string error)
    {
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
