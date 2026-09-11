using UnityEngine;

/// <summary>Pure directional and mitigation rules for raised shields.</summary>
public static class ShieldDefenseMath
{
    private const float MinimumDirectionSqrMagnitude = 0.000001f;
    private const float BoundaryTolerance = 0.000001f;

    public static bool TryMitigate(
        float incomingDamage,
        float damageReduction,
        float defensiveConeDegrees,
        Vector2 facingDirection,
        Vector2 attackDirection,
        out float mitigatedDamage)
    {
        mitigatedDamage = incomingDamage;
        if (!IsFinite(incomingDamage) || incomingDamage < 0f ||
            !IsFinite(damageReduction) || damageReduction <= 0f || damageReduction >= 1f ||
            !IsFinite(defensiveConeDegrees) || defensiveConeDegrees <= 0f || defensiveConeDegrees > 360f ||
            !TryNormalize(facingDirection, out Vector2 facing) ||
            !TryNormalize(-attackDirection, out Vector2 directionToImpactOrigin))
        {
            return false;
        }

        float minimumDot = Mathf.Cos(defensiveConeDegrees * 0.5f * Mathf.Deg2Rad);
        if (Vector2.Dot(facing, directionToImpactOrigin) + BoundaryTolerance < minimumDot)
        {
            return false;
        }

        mitigatedDamage = incomingDamage * (1f - damageReduction);
        return true;
    }

    private static bool TryNormalize(Vector2 value, out Vector2 normalized)
    {
        normalized = default;
        if (!IsFinite(value.x) || !IsFinite(value.y) ||
            value.sqrMagnitude < MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        normalized = value.normalized;
        return IsFinite(normalized.x) && IsFinite(normalized.y);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
