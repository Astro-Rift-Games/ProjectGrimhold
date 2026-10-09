using UnityEngine;

/// <summary>Pure, deterministic rules of Seismic Strike. No Unity scene or Fusion state is read here.</summary>
internal static class SeismicStrikeRules
{
    /// <summary>Distance below which a target counts as standing on the caster and has no radial direction.</summary>
    public const float MinimumRadialDistance = 0.0001f;

    /// <summary>
    /// Deterministic direction used when the target has no radial direction (it coincides with the caster or a
    /// position is not finite). Up is arbitrary but fixed, so every simulation computes the same push.
    /// </summary>
    public static readonly Vector2 FallbackDirection = Vector2.up;

    /// <summary>Damage and knockback may be zero; the radius and the preparation may not.</summary>
    public static bool TryValidateConfiguration(float radius, float damage, float knockbackForce,
        float preparationSeconds, out string error)
    {
        if (!IsPositiveFinite(radius))
            error = "Seismic Strike radius must be finite and greater than zero.";
        else if (!IsNonNegativeFinite(damage))
            error = "Seismic Strike damage must be finite and not negative.";
        else if (!IsNonNegativeFinite(knockbackForce))
            error = "Seismic Strike knockback force must be finite and not negative.";
        else if (!IsPositiveFinite(preparationSeconds))
            error = "Seismic Strike preparation seconds must be finite and greater than zero.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>
    /// Unit direction from the caster to the target, i.e. radially away from the shockwave center. A target
    /// without a usable radial direction is pushed along <see cref="FallbackDirection"/>.
    /// </summary>
    public static Vector2 ResolveKnockbackDirection(Vector2 casterPosition, Vector2 targetPosition)
    {
        Vector2 offset = targetPosition - casterPosition;
        float length = offset.magnitude;
        if (float.IsNaN(length) || float.IsInfinity(length) || length < MinimumRadialDistance) return FallbackDirection;
        return offset / length;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    private static bool IsNonNegativeFinite(float value) => value >= 0f && !float.IsInfinity(value);
}
