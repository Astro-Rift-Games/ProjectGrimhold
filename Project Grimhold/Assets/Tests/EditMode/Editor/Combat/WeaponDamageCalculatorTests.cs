#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Combat
{
    public sealed class WeaponDamageCalculatorTests
    {
        [Test]
        public void PlainTemplateProjection_ProductionWeaponsPreserveLegacyDamageUntilRaidTransport()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            CharacterAttributeState attributes = CreateAttributes(10, 20, 30, 40, 50, 60);
            int weapons = 0;
            for (int i = 0; i < catalog.DefinitionCount; i++)
            {
                Assert.That(catalog.TryGetByIndex(i, out LootDefinition loot), Is.True);
                if (loot.Category != LootCategory.Weapon) continue;
                weapons++;
                WeaponDefinition weapon = loot.WeaponDefinition;
                Assert.That(weapon, Is.Not.Null);
                EquipmentInstance plain = EquipmentInstance.PlainCache(weapon);
                Assert.That(plain.HasIdentity, Is.False);
                Assert.That(plain.Modifiers.HasValue, Is.True);
                var modifiers = plain.Modifiers.Value;
                Assert.That(modifiers.GemSocketCount, Is.Zero);
                Assert.That(WeaponScalingContributionsResolver.TryResolve(weapon.OffensiveScaling,
                    out var legacy), Is.True);
                Assert.That(WeaponDamageCalculator.TryCalculate(weapon.BaseDamage, attributes, legacy,
                    out float previousDamage), Is.True);
                Assert.That(WeaponScalingContributionsResolver.TryResolve(modifiers, weapon.NaturalScalingAttribute,
                    out var contributions), Is.True);
                Assert.That(WeaponGemDamageBonusResolver.TryResolve(modifiers, catalog, out int bonus), Is.True);
                Assert.That(WeaponDamageCalculator.TryCalculate(weapon.BaseDamage, bonus, attributes, contributions,
                    out float damage), Is.True);
                Assert.That(damage, Is.EqualTo(previousDamage), loot.LootId.Value);
            }
            Assert.That(weapons, Is.GreaterThan(0));
        }

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

        [TestCase(30f, 10, 20, 0.85f, 46f)]
        [TestCase(30f, 5, 0, 0.85f, 35f)]
        [TestCase(17.5f, 5, 0, 0f, 22f)]
        public void TryCalculate_AddsGemDamageBonusToBaseBeforeScaling(
            float baseDamage,
            int gemDamageBonus,
            int strength,
            float scalingCoefficient,
            float expected)
        {
            CharacterAttributeState attributes = CreateAttributes(CharacterAttribute.Strength, strength);
            WeaponScalingContributions contributions = scalingCoefficient > 0f
                ? CreateContributions(CharacterAttribute.Strength, scalingCoefficient)
                : default;

            Assert.That(WeaponDamageCalculator.TryCalculate(
                baseDamage, gemDamageBonus, attributes, contributions, out float result), Is.True);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void TryCalculate_WithZeroGemBonus_MatchesTheOverloadWithoutGems()
        {
            CharacterAttributeState attributes = CreateAttributes(CharacterAttribute.Strength, 20);
            WeaponScalingContributions contributions = CreateContributions(CharacterAttribute.Strength, 0.7f);

            Assert.That(WeaponDamageCalculator.TryCalculate(
                100f, 0, attributes, contributions, out float withGems), Is.True);
            Assert.That(WeaponDamageCalculator.TryCalculate(
                100f, attributes, contributions, out float withoutGems), Is.True);
            Assert.That(withGems, Is.EqualTo(withoutGems));
        }

        [Test]
        public void TryCalculate_NegativeGemBonus_IsRejected()
        {
            CharacterAttributeState attributes = CreateAttributes(CharacterAttribute.Strength, 20);

            Assert.That(WeaponDamageCalculator.TryCalculate(
                30f, -1, attributes, default, out float result), Is.False);
            Assert.That(result, Is.EqualTo(0f));
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
