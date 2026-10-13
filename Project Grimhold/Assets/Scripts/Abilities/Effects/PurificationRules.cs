/// <summary>Pure, deterministic rules of Purification. No Unity scene or Fusion state is read here.</summary>
internal static class PurificationRules
{
    /// <summary>The radius, the cast time and the protection must all be finite and greater than zero.</summary>
    public static bool TryValidateConfiguration(float radius, float castSeconds, float protectionSeconds,
        out string error)
    {
        if (!IsPositiveFinite(radius))
            error = "Purification radius must be finite and greater than zero.";
        else if (!IsPositiveFinite(castSeconds))
            error = "Purification cast seconds must be finite and greater than zero.";
        else if (!IsPositiveFinite(protectionSeconds))
            error = "Purification protection seconds must be finite and greater than zero.";
        else
            error = string.Empty;
        return error.Length == 0;
    }

    /// <summary>
    /// Particular target condition on top of the common ally rule: the ally has at least one active purifiable
    /// state, or it can receive the temporary protection. The second case is what allows preventive use.
    /// </summary>
    public static bool IsValidTarget(bool hasPurifiableState, bool canReceiveProtection) =>
        hasPurifiableState || canReceiveProtection;

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
}
