#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Equipment
{
    public sealed class WeaponOffensiveScalingTests
    {
        [TestCase(CharacterAttribute.Strength, 7)]
        [TestCase(CharacterAttribute.Dexterity, 11)]
        [TestCase(CharacterAttribute.Intelligence, 13)]
        public void TryResolveAttributeValue_ReturnsTheConfiguredOffensiveAttribute(
            CharacterAttribute attribute,
            int expected)
        {
            var scaling = new WeaponOffensiveScaling(attribute, 0.7f);
            CharacterAttributeState attributes = CreateAttributes(17, 19, 7, 11, 13, 23);

            Assert.That(scaling.TryResolveAttributeValue(attributes, out int value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void UnrelatedAttributeChanges_DoNotChangeTheResolvedValueOrDamage()
        {
            var scaling = new WeaponOffensiveScaling(CharacterAttribute.Dexterity, 0.7f);
            CharacterAttributeState first = CreateAttributes(1, 2, 3, 11, 5, 6);
            CharacterAttributeState second = CreateAttributes(20, 21, 22, 11, 24, 25);

            Assert.That(scaling.TryResolveAttributeValue(first, out int firstValue), Is.True);
            Assert.That(scaling.TryResolveAttributeValue(second, out int secondValue), Is.True);
            Assert.That(secondValue, Is.EqualTo(firstValue));
            Assert.That(
                WeaponDamageCalculator.Calculate(10f, secondValue, scaling.Coefficient),
                Is.EqualTo(WeaponDamageCalculator.Calculate(10f, firstValue, scaling.Coefficient)));
        }

        [TestCase(CharacterAttribute.Vitality)]
        [TestCase(CharacterAttribute.Resistance)]
        [TestCase(CharacterAttribute.Luck)]
        public void PositiveCoefficient_WithNonOffensiveAttribute_IsInvalid(CharacterAttribute attribute)
        {
            var scaling = new WeaponOffensiveScaling(attribute, 0.5f);

            Assert.That(scaling.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("not offensive"));
        }

        [Test]
        public void PositiveCoefficient_WithUnknownAttribute_IsInvalid()
        {
            var scaling = new WeaponOffensiveScaling((CharacterAttribute)999, 0.5f);

            Assert.That(scaling.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("not offensive"));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidCoefficient_IsRejected(float coefficient)
        {
            var scaling = new WeaponOffensiveScaling(CharacterAttribute.Strength, coefficient);

            Assert.That(scaling.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("finite and non-negative"));
        }

        [Test]
        public void ZeroCoefficient_ResolvesNoAttributeContribution()
        {
            var scaling = new WeaponOffensiveScaling((CharacterAttribute)999, 0f);
            CharacterAttributeState attributes = CreateAttributes(7, 11, 13, 17, 19, 23);

            Assert.That(scaling.TryValidate(out string error), Is.True, error);
            Assert.That(scaling.HasScaling, Is.False);
            Assert.That(scaling.TryResolveAttributeValue(attributes, out int value), Is.True);
            Assert.That(value, Is.Zero);
        }

        [Test]
        public void WeaponDefinition_WithInvalidOffensiveScaling_IsInvalid()
        {
            WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            MeleeAttackConfig attack = CreateValidMeleeConfig();
            try
            {
                SetPrivateField(weapon, "_primaryAttack", attack);
                SetValidWeaponStats(weapon);
                SetPrivateField(
                    weapon,
                    "_offensiveScaling",
                    new WeaponOffensiveScaling(CharacterAttribute.Vitality, 0.5f));

                Assert.That(weapon.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("invalid offensive scaling"));
            }
            finally
            {
                Object.DestroyImmediate(attack);
                Object.DestroyImmediate(weapon);
            }
        }

        private static CharacterAttributeState CreateAttributes(
            int vitality,
            int resistance,
            int strength,
            int dexterity,
            int intelligence,
            int luck)
        {
            Assert.That(
                CharacterAttributeState.TryCreate(
                    vitality,
                    resistance,
                    strength,
                    dexterity,
                    intelligence,
                    luck,
                    0,
                    out CharacterAttributeState state),
                Is.True);
            return state;
        }

        private static MeleeAttackConfig CreateValidMeleeConfig()
        {
            var config = ScriptableObject.CreateInstance<MeleeAttackConfig>();
            SetPrivateField(config, "_radius", 0.5f);
            SetPrivateField(config, "_maximumTargets", 1);
            SetPrivateField(config, "_targetLayerMask", new LayerMask { value = 1 });
            return config;
        }

        [Test]
        public void WeaponDefinition_WithEffectiveRangeBelowMeleeRadius_IsInvalid()
        {
            WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            MeleeAttackConfig attack = CreateValidMeleeConfig();
            try
            {
                SetPrivateField(weapon, "_primaryAttack", attack);
                SetValidWeaponStats(weapon);
                SetPrivateField(weapon, "_range", 0.49f);

                Assert.That(weapon.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("must be at least its melee radius"));
            }
            finally
            {
                Object.DestroyImmediate(attack);
                Object.DestroyImmediate(weapon);
            }
        }

        private static void SetValidWeaponStats(WeaponDefinition weapon)
        {
            SetPrivateField(weapon, "_baseDamage", 10f);
            SetPrivateField(weapon, "_attackIntervalSeconds", 0.5f);
            SetPrivateField(weapon, "_range", 1.5f);
            SetPrivateField(weapon, "_staminaCost", 1f);
            SetPrivateField(weapon, "_damageType", DamageType.Physical);
            SetPrivateField(weapon, "_knockbackForce", 0f);
        }

        private static void SetPrivateField(object target, string fieldName, object value) =>
            SetPrivateField(target, target.GetType(), fieldName, value);

        private static void SetPrivateField(
            object target,
            System.Type declaringType,
            string fieldName,
            object value)
        {
            FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {declaringType.Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }
    }
}
#endif
