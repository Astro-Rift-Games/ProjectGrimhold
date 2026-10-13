/// <summary>Pure, deterministic rules of Empower. No Unity scene or Fusion state is read here.</summary>
internal static class EmpowerRules
{
    /// <summary>The radius, the damage bonus fraction and the duration must all be finite and greater than zero.</summary>
    public static bool TryValidateConfiguration(float radius, float damageBonusFraction, float durationSeconds,
        out string error)
    {
        if (!IsPositiveFinite(radius))
        {
            error = "Empower radius must be finite and greater than zero.";
            return false;
        }

        return TryValidateEffect(damageBonusFraction, durationSeconds, out error);
    }

    /// <summary>Validates what an application carries: a positive finite bonus fraction and duration.</summary>
    public static bool TryValidateEffect(float damageBonusFraction, float durationSeconds, out string error)
    {
        if (!IsPositiveFinite(damageBonusFraction))
            error = "Empower damage bonus fraction must be finite and greater than zero.";
        else if (!IsPositiveFinite(durationSeconds))
            error = "Empower duration seconds must be finite and greater than zero.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>
    /// Basic weapon attack damage multiplier: 1 plus the bonus fraction while the effect runs, otherwise exactly 1.
    /// An unusable stored bonus also resolves to 1 rather than corrupting damage.
    /// </summary>
    public static float ResolveDamageMultiplier(bool isActive, float bonusFraction) =>
        isActive && IsPositiveFinite(bonusFraction) ? 1f + bonusFraction : 1f;

    /// <summary>
    /// A new application refreshes the effect: its magnitude is set, never added to the one already running,
    /// so repeated applications can never stack.
    /// </summary>
    public static float ResolveRefreshedBonus(float previousBonusFraction, float newBonusFraction) =>
        newBonusFraction;

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
}
