using UnityEngine;

/// <summary>Pure, deterministic rules of Trap. No Unity scene or Fusion state is read here.</summary>
internal static class TrapRules
{
    /// <summary>Returned by <see cref="SelectEvictionIndex"/> when the caster still has room for another trap.</summary>
    public const int NoEviction = -1;

    /// <summary>Periodic damage may be zero; every other value must be positive.</summary>
    public static bool TryValidateConfiguration(float placementDistance, float lifetimeSeconds, int maxPerCaster,
        float triggerRadius, float immobilizeSeconds, float periodicDamage, float tickIntervalSeconds, out string error)
    {
        if (!IsPositiveFinite(placementDistance))
            error = "Trap placement distance must be finite and greater than zero.";
        else if (!IsPositiveFinite(lifetimeSeconds))
            error = "Trap lifetime seconds must be finite and greater than zero.";
        else if (maxPerCaster < 1)
            error = "Trap limit per caster must be at least one.";
        else if (!IsPositiveFinite(triggerRadius))
            error = "Trap trigger radius must be finite and greater than zero.";
        else if (!IsPositiveFinite(immobilizeSeconds))
            error = "Trap immobilize seconds must be finite and greater than zero.";
        else if (!IsNonNegativeFinite(periodicDamage))
            error = "Trap periodic damage must be finite and not negative.";
        else if (!IsPositiveFinite(tickIntervalSeconds))
            error = "Trap tick interval seconds must be finite and greater than zero.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>
    /// The ground probe needs a positive footprint radius and at least one blocking layer; an empty mask would
    /// silently accept every position, so it is a configuration error.
    /// </summary>
    public static bool TryValidatePlacementProbe(float clearanceRadius, int blockingMask, out string error)
    {
        if (!IsPositiveFinite(clearanceRadius))
            error = "Trap placement clearance radius must be finite and greater than zero.";
        else if (blockingMask == 0)
            error = "Trap ground-blocking layer mask must not be empty.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>
    /// Position ahead of the caster along the captured aim. Returns false, with a zero position, when the origin,
    /// the aim or the distance is unusable. Whether that position is valid ground is decided by the caller's
    /// physics query, never here.
    /// </summary>
    public static bool TryResolvePlacement(Vector2 origin, Vector2 aimDirection, float distance, out Vector2 position)
    {
        position = Vector2.zero;
        if (!IsFinite(origin) || !IsFinite(aimDirection) || !IsPositiveFinite(distance)) return false;
        float length = aimDirection.magnitude;
        if (length < Mathf.Epsilon) return false;
        position = origin + aimDirection / length * distance;
        return true;
    }

    /// <summary>
    /// Index (into the first <paramref name="count"/> entries) of the trap the caster must lose to make room for a
    /// new one: the one with the least remaining lifetime, the first listed on a tie. Returns
    /// <see cref="NoEviction"/> while the caster is below its limit.
    /// </summary>
    public static int SelectEvictionIndex(int[] remainingTicks, int count, int maxPerCaster)
    {
        if (remainingTicks == null || count <= 0 || maxPerCaster < 1 || count < maxPerCaster) return NoEviction;
        int lowest = 0;
        for (int i = 1; i < count; i++)
            if (remainingTicks[i] < remainingTicks[lowest]) lowest = i;
        return lowest;
    }

    private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    private static bool IsNonNegativeFinite(float value) => value >= 0f && !float.IsInfinity(value);
}
