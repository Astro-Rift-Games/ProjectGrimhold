/// <summary>Pure adapters from authored scaling contracts to the common runtime representation.</summary>
public static class WeaponScalingContributionsResolver
{
    public static bool TryResolve(
        in WeaponOffensiveScaling legacy,
        out WeaponScalingContributions contributions)
    {
        contributions = default;
        if (!legacy.TryValidate(out _))
        {
            return false;
        }

        if (!legacy.HasScaling)
        {
            return true;
        }

        return WeaponScalingContribution.TryCreate(
                legacy.Attribute,
                legacy.Coefficient,
                out WeaponScalingContribution primary) &&
            WeaponScalingContributions.TryCreate(primary, default, out contributions);
    }

    /// <summary>
    /// Translates the future instance contract without making it an active runtime source.
    /// </summary>
    public static bool TryResolve(
        in WeaponInstanceModifiers instanceModifiers,
        CharacterAttribute naturalAttribute,
        out WeaponScalingContributions contributions)
    {
        contributions = default;
        if (!instanceModifiers.TryValidate(naturalAttribute, out _))
        {
            return false;
        }

        if (!TryResolveModifier(instanceModifiers.Primary, out WeaponScalingContribution primary) ||
            !TryResolveModifier(instanceModifiers.Secondary, out WeaponScalingContribution secondary))
        {
            return false;
        }

        return WeaponScalingContributions.TryCreate(primary, secondary, out contributions);
    }

    private static bool TryResolveModifier(
        in WeaponScalingModifier modifier,
        out WeaponScalingContribution contribution)
    {
        contribution = default;
        if (!modifier.IsPresent)
        {
            return true;
        }

        return modifier.TryGetCoefficient(out float coefficient) &&
            WeaponScalingContribution.TryCreate(modifier.Attribute, coefficient, out contribution);
    }
}
