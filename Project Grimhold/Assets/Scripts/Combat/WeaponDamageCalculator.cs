using System;

/// <summary>Calculates canonical runtime weapon damage from resolved scaling contributions.</summary>
public static class WeaponDamageCalculator
{
    public static bool TryCalculate(
        float baseDamage,
        in CharacterAttributeState attributes,
        in WeaponScalingContributions contributions,
        out float effectiveDamage) =>
        TryCalculate(baseDamage, 0, attributes, contributions, out effectiveDamage);

    /// <summary>
    /// Adds the flat installed-gem bonus to the base damage before scaling:
    /// floor((BaseDamage + GemDamageBonus) * (1 + sum of contributions)).
    /// </summary>
    public static bool TryCalculate(
        float baseDamage,
        int gemDamageBonus,
        in CharacterAttributeState attributes,
        in WeaponScalingContributions contributions,
        out float effectiveDamage)
    {
        effectiveDamage = 0f;
        if (!IsFinite(baseDamage) || baseDamage < 0f || gemDamageBonus < 0 ||
            !TryCalculateContribution(attributes, contributions.Primary, out float primary) ||
            !TryCalculateContribution(attributes, contributions.Secondary, out float secondary))
        {
            return false;
        }

        float unroundedDamage = (baseDamage + gemDamageBonus) * (1f + primary + secondary);
        if (!IsFinite(unroundedDamage))
        {
            return false;
        }

        effectiveDamage = MathF.Floor(unroundedDamage);
        return true;
    }

    private static bool TryCalculateContribution(
        in CharacterAttributeState attributes,
        in WeaponScalingContribution contribution,
        out float value)
    {
        value = 0f;
        if (!contribution.IsPresent)
        {
            return true;
        }

        if (!IsFinite(contribution.Coefficient) || contribution.Coefficient <= 0f ||
            !attributes.TryGetValue(contribution.Attribute, out int attributeValue))
        {
            return false;
        }

        value = attributeValue / 100f * contribution.Coefficient;
        return IsFinite(value);
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
