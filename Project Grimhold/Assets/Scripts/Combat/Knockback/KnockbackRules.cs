using UnityEngine;

/// <summary>Pure impact decisions and cumulative displacement math for Doc 18 §6.</summary>
public static class KnockbackRules
{
    /// <summary>Subtracts RE from original PE, sanitizing non-finite and negative inputs.</summary>
    public static float ComputeEffectivePower(float originalPower, float resistance)
        => Mathf.Max(0, Sanitize(originalPower) - Sanitize(resistance));

    /// <summary>Uses impact eligibility and strictly greater effective power to decide replacement.</summary>
    public static KnockbackDecision Resolve(bool wasEligibleAtImpact, float effectivePower,
        bool hasActive, float activeEffectivePower)
    {
        if (!wasEligibleAtImpact)
        {
            return KnockbackDecision.RejectedIneligible;
        }
        effectivePower = Sanitize(effectivePower);
        if (effectivePower <= 0)
        {
            return KnockbackDecision.Absorbed;
        }
        if (!hasActive)
        {
            return KnockbackDecision.Started;
        }
        return effectivePower > Sanitize(activeEffectivePower)
            ? KnockbackDecision.Replaced : KnockbackDecision.KeptActive;
    }

    /// <summary>Downed remains alive; only definitive defeat cancels authorized travel.</summary>
    public static bool ShouldContinue(bool isAlive) => isAlive;

    /// <summary>Caller supplies a unit direction and evaluated cumulative progress for adjacent ticks.</summary>
    public static Vector2 ComputeStep(Vector2 direction, float distance, float previousProgress, float currentProgress)
        => direction * Sanitize(distance) * (
            Mathf.Clamp01(Sanitize(currentProgress)) - Mathf.Clamp01(Sanitize(previousProgress)));

    /// <summary>Reports completion against a duration of at least one tick.</summary>
    public static bool IsComplete(int elapsedTicks, int durationTicks)
        => elapsedTicks >= Mathf.Max(1, durationTicks);

    private static float Sanitize(float value)
        => float.IsNaN(value) || float.IsInfinity(value) || value < 0 ? 0 : value;
}
