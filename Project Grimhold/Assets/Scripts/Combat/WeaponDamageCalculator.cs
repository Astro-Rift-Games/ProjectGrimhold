/// <summary>Calculates the runtime damage produced by one weapon and one resolved attribute value.</summary>
public static class WeaponDamageCalculator
{
    public static float Calculate(float baseDamage, int attributeValue, float scalingCoefficient) =>
        baseDamage + attributeValue * scalingCoefficient;
}
