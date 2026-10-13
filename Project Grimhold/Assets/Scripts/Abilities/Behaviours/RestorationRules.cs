/// <summary>Pure, deterministic rules of Restoration. No Unity scene or Fusion state is read here.</summary>
internal static class RestorationRules
{
    /// <summary>The radius, the heal amount and the preparation must all be finite and greater than zero.</summary>
    public static bool TryValidateConfiguration(float radius, float healAmount, float preparationSeconds,
        out string error)
    {
        if (!IsPositiveFinite(radius))
            error = "Restoration radius must be finite and greater than zero.";
        else if (!IsPositiveFinite(healAmount))
            error = "Restoration heal amount must be finite and greater than zero.";
        else if (!IsPositiveFinite(preparationSeconds))
            error = "Restoration preparation seconds must be finite and greater than zero.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>
    /// Particular target condition on top of the common ally rule: alive, not Downed (a Downed player still
    /// counts as alive, and Restoration never heals nor revives it) and with missing Health.
    /// </summary>
    public static bool CanReceiveRestoration(bool isAlive, bool isDowned, float health, float maxHealth) =>
        isAlive && !isDowned && health < maxHealth;

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
}
