#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Combat
{
    public sealed class WeaponDamageCalculatorTests
    {
        [TestCase(100f, CharacterAttribute.Strength, 20, 0.7f, 114f)]
        [TestCase(22f, CharacterAttribute.Intelligence, 0, 0.85f, 22f)]
        [TestCase(40f, CharacterAttribute.Luck, 12, 0.55f, 42f)]
        public void TryCalculate_AppliesCanonicalMultiplicativeFormulaAndFloors(
            float baseDamage,
            CharacterAttribute attribute,
            int attributeValue,
            float scalingCoefficient,
            float expected)
        {
            CharacterAttributeState attributes = CreateAttributes(attribute, attributeValue);
            WeaponScalingContributions contributions = CreateContributions(attribute, scalingCoefficient);

            Assert.That(WeaponDamageCalculator.TryCalculate(
                baseDamage, attributes, contributions, out float result), Is.True);
            Assert.That(result, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void TryCalculate_WithNoContributionsReturnsFlooredBaseDamage()
        {
            const float baseDamage = 17.375f;
            CharacterAttributeState attributes = CreateAttributes(CharacterAttribute.Strength, 999);

            Assert.That(WeaponDamageCalculator.TryCalculate(
                baseDamage, attributes, default, out float result), Is.True);
            Assert.That(result, Is.EqualTo(17f));
        }

        [Test]
        public void TryCalculate_AddsPrimaryAndSecondaryInsideOneMultiplier()
        {
            CharacterAttributeState attributes = CreateAttributes(
                vitality: 10, resistance: 0, strength: 20, dexterity: 0, intelligence: 0, luck: 0);
            Assert.That(WeaponScalingContribution.TryCreate(
                CharacterAttribute.Strength, 0.7f, out WeaponScalingContribution primary), Is.True);
            Assert.That(WeaponScalingContribution.TryCreate(
                CharacterAttribute.Vitality, 0.4f, out WeaponScalingContribution secondary), Is.True);
            Assert.That(WeaponScalingContributions.TryCreate(
                primary, secondary, out WeaponScalingContributions contributions), Is.True);

            Assert.That(WeaponDamageCalculator.TryCalculate(
                100f, attributes, contributions, out float result), Is.True);
            Assert.That(result, Is.EqualTo(118f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void TryCalculate_InvalidBaseDamage_IsRejected(float baseDamage)
        {
            CharacterAttributeState attributes = CreateAttributes(CharacterAttribute.Strength, 20);

            Assert.That(WeaponDamageCalculator.TryCalculate(
                baseDamage, attributes, default, out float result), Is.False);
            Assert.That(result, Is.Zero);
        }

        [Test]
        public void TryCalculate_ResultOutsideFloatRange_IsRejected()
        {
            CharacterAttributeState attributes = CreateAttributes(
                CharacterAttribute.Strength,
                int.MaxValue);
            WeaponScalingContributions contributions = CreateContributions(
                CharacterAttribute.Strength,
                float.MaxValue);

            Assert.That(WeaponDamageCalculator.TryCalculate(
                float.MaxValue, attributes, contributions, out float result), Is.False);
            Assert.That(result, Is.Zero);
        }

        private static WeaponScalingContributions CreateContributions(
            CharacterAttribute attribute,
            float coefficient)
        {
            Assert.That(WeaponScalingContribution.TryCreate(
                attribute, coefficient, out WeaponScalingContribution contribution), Is.True);
            Assert.That(WeaponScalingContributions.TryCreate(
                contribution, default, out WeaponScalingContributions contributions), Is.True);
            return contributions;
        }

        private static CharacterAttributeState CreateAttributes(
            CharacterAttribute attribute,
            int value)
        {
            int vitality = attribute == CharacterAttribute.Vitality ? value : 0;
            int resistance = attribute == CharacterAttribute.Resistance ? value : 0;
            int strength = attribute == CharacterAttribute.Strength ? value : 0;
            int dexterity = attribute == CharacterAttribute.Dexterity ? value : 0;
            int intelligence = attribute == CharacterAttribute.Intelligence ? value : 0;
            int luck = attribute == CharacterAttribute.Luck ? value : 0;
            return CreateAttributes(vitality, resistance, strength, dexterity, intelligence, luck);
        }

        private static CharacterAttributeState CreateAttributes(
            int vitality,
            int resistance,
            int strength,
            int dexterity,
            int intelligence,
            int luck)
        {
            Assert.That(CharacterAttributeState.TryCreate(
                vitality, resistance, strength, dexterity, intelligence, luck, 0,
                out CharacterAttributeState attributes), Is.True);
            return attributes;
        }
    }
}
#endif
