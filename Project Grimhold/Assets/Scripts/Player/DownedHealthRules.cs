using UnityEngine;

/// <summary>
/// Pure deterministic rules for the Downed health reserve.
/// The reserve is independent from maximum Health and never reads Vitality.
/// </summary>
internal static class DownedHealthRules
{
    /// <summary>Creates the full reserve for a new Downed cycle from the configured value.</summary>
    internal static bool TryCreateReserve(float configuredReserve, out float reserve)
    {
        if (!IsFinite(configuredReserve) || configuredReserve <= 0f)
        {
            reserve = 0f;
            return false;
        }

        reserve = configuredReserve;
        return true;
    }

    /// <summary>Applies continuous drain. Invalid rate or delta leave the reserve unchanged.</summary>
    internal static float Drain(float current, float ratePerSecond, float deltaTime)
    {
        float sanitized = Sanitize(current);
        if (!IsFinite(ratePerSecond) || !IsFinite(deltaTime) ||
            ratePerSecond < 0f || deltaTime < 0f)
        {
            return sanitized;
        }

        return Mathf.Max(0f, sanitized - ratePerSecond * deltaTime);
    }

    /// <summary>Applies mitigated damage scaled by the multiplier. Excess is discarded.</summary>
    internal static float ApplyDamage(float current, float mitigatedDamage, float multiplier)
    {
        float sanitized = Sanitize(current);
        if (!IsFinite(mitigatedDamage) || !IsFinite(multiplier) ||
            mitigatedDamage < 0f || multiplier < 0f)
        {
            return sanitized;
        }

        return Mathf.Max(0f, sanitized - mitigatedDamage * multiplier);
    }

    internal static bool IsDepleted(float current)
    {
        return Sanitize(current) <= 0f;
    }

    private static float Sanitize(float value)
    {
        return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
