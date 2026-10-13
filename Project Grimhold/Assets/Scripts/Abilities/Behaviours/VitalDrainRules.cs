using System;

/// <summary>Pure Vital Drain arithmetic; only confirmed actual damage funds healing.</summary>
internal static class VitalDrainRules
{
    public static bool TryValidateConfiguration(float radius, float damage, float drainProportion,
        float preparationSeconds, out string error)
    {
        if (!IsPositiveFinite(radius)) error = "Vital Drain radius must be finite and greater than zero.";
        else if (!IsNonNegativeFinite(damage)) error = "Vital Drain damage must be finite and not negative.";
        else if (!IsNonNegativeFinite(drainProportion)) error = "Vital Drain proportion must be finite and not negative.";
        else if (!IsPositiveFinite(preparationSeconds)) error = "Vital Drain preparation must be finite and greater than zero.";
        else error = string.Empty;
        return error.Length == 0;
    }

    public static double AddConfirmedDamage(double total, in DamageResult result) =>
        result.IsApplied && IsPositiveFinite(result.AppliedDamage) ? total + result.AppliedDamage : total;

    public static float CalculateHealing(double totalAppliedDamage, float drainProportion, float health, float maximumHealth)
    {
        if (!(totalAppliedDamage > 0d) || double.IsInfinity(totalAppliedDamage) ||
            !IsPositiveFinite(drainProportion) || !IsNonNegativeFinite(health) || !IsPositiveFinite(maximumHealth)) return 0f;
        // Double intermediates avoid overflowing float when many targets or large tuning values are summed.
        double missingHealth = Math.Max(0d, (double)maximumHealth - health);
        return (float)Math.Min(missingHealth, totalAppliedDamage * drainProportion);
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
    private static bool IsNonNegativeFinite(float value) => value >= 0f && !float.IsInfinity(value);
}
