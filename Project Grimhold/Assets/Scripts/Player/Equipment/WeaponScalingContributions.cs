using System;

/// <summary>Resolved runtime weapon scaling with at most one primary and one secondary contribution.</summary>
public readonly struct WeaponScalingContributions : IEquatable<WeaponScalingContributions>
{
    public WeaponScalingContribution Primary { get; }
    public WeaponScalingContribution Secondary { get; }
    public bool HasPrimary => Primary.IsPresent;
    public bool HasSecondary => Secondary.IsPresent;

    private WeaponScalingContributions(
        in WeaponScalingContribution primary,
        in WeaponScalingContribution secondary)
    {
        Primary = primary;
        Secondary = secondary;
    }

    public static bool TryCreate(
        in WeaponScalingContribution primary,
        in WeaponScalingContribution secondary,
        out WeaponScalingContributions contributions)
    {
        contributions = default;
        if (secondary.IsPresent && !primary.IsPresent ||
            primary.IsPresent && secondary.IsPresent && primary.Attribute == secondary.Attribute)
        {
            return false;
        }

        contributions = new WeaponScalingContributions(primary, secondary);
        return true;
    }

    public bool Equals(WeaponScalingContributions other) =>
        Primary.Equals(other.Primary) && Secondary.Equals(other.Secondary);

    public override bool Equals(object obj) =>
        obj is WeaponScalingContributions other && Equals(other);

    public override int GetHashCode() => (Primary.GetHashCode() * 397) ^ Secondary.GetHashCode();
}
