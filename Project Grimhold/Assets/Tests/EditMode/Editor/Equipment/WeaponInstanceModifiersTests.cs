#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;

namespace Tests.EditMode.Equipment
{
    public sealed class WeaponInstanceModifiersTests
    {
        [TestCase(WeaponScalingGrade.E, 0.25f)]
        [TestCase(WeaponScalingGrade.D, 0.40f)]
        [TestCase(WeaponScalingGrade.C, 0.55f)]
        [TestCase(WeaponScalingGrade.B, 0.70f)]
        [TestCase(WeaponScalingGrade.A, 0.85f)]
        [TestCase(WeaponScalingGrade.S, 1.00f)]
        public void PresentModifier_ResolvesCanonicalCoefficient(
            WeaponScalingGrade grade,
            float expected)
        {
            Assert.That(
                WeaponScalingModifier.TryCreate(
                    CharacterAttribute.Strength,
                    grade,
                    out WeaponScalingModifier modifier,
                    out string error),
                Is.True,
                error);
            Assert.That(modifier.TryGetCoefficient(out float coefficient), Is.True);
            Assert.That(coefficient, Is.EqualTo(expected));
        }

        [Test]
        public void TryCreateModifier_WithNoneGrade_IsRejectedAsAbsent()
        {
            Assert.That(
                WeaponScalingModifier.TryCreate(
                    CharacterAttribute.Strength,
                    WeaponScalingGrade.None,
                    out WeaponScalingModifier modifier,
                    out string error),
                Is.False);
            Assert.That(modifier.IsPresent, Is.False);
            Assert.That(error, Does.Contain("E through S"));
        }

        [Test]
        public void Normalize_NoneGrade_DiscardsIgnoredAttribute()
        {
            WeaponScalingModifier serialized = CreateSerializedModifier(
                CharacterAttribute.Luck,
                WeaponScalingGrade.None);

            WeaponScalingModifier normalized = serialized.Normalize();

            Assert.That(normalized.IsPresent, Is.False);
            Assert.That(normalized.Attribute, Is.EqualTo(default(CharacterAttribute)));
        }

        [Test]
        public void TryCreate_WithMatchingPrimaryAndDistinctSecondary_IsValid()
        {
            WeaponScalingModifier primary = CreateModifier(CharacterAttribute.Strength, WeaponScalingGrade.B);
            WeaponScalingModifier secondary = CreateModifier(CharacterAttribute.Luck, WeaponScalingGrade.D);

            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    primary,
                    secondary,
                    CharacterAttribute.Strength,
                    out WeaponInstanceModifiers modifiers,
                    out string error),
                Is.True,
                error);
            Assert.That(modifiers.HasPrimary, Is.True);
            Assert.That(modifiers.HasSecondary, Is.True);
        }

        [TestCase(CharacterAttribute.Vitality)]
        [TestCase(CharacterAttribute.Resistance)]
        [TestCase(CharacterAttribute.Luck)]
        public void Resolver_TranslatesPrimaryAndSecondaryToRuntimeContributions(
            CharacterAttribute secondaryAttribute)
        {
            WeaponScalingModifier primary = CreateModifier(CharacterAttribute.Strength, WeaponScalingGrade.B);
            WeaponScalingModifier secondary = CreateModifier(secondaryAttribute, WeaponScalingGrade.D);
            Assert.That(WeaponInstanceModifiers.TryCreate(
                primary,
                secondary,
                CharacterAttribute.Strength,
                out WeaponInstanceModifiers modifiers,
                out string error), Is.True, error);

            Assert.That(WeaponScalingContributionsResolver.TryResolve(
                modifiers,
                CharacterAttribute.Strength,
                out WeaponScalingContributions contributions), Is.True);
            Assert.That(contributions.Primary.Attribute, Is.EqualTo(CharacterAttribute.Strength));
            Assert.That(contributions.Primary.Coefficient, Is.EqualTo(0.70f));
            Assert.That(contributions.Secondary.Attribute, Is.EqualTo(secondaryAttribute));
            Assert.That(contributions.Secondary.Coefficient, Is.EqualTo(0.40f));
        }

        [Test]
        public void TryCreate_WithPrimaryDifferentFromNaturalAttribute_IsRejected()
        {
            WeaponScalingModifier primary = CreateModifier(CharacterAttribute.Dexterity, WeaponScalingGrade.B);

            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    primary,
                    default,
                    CharacterAttribute.Strength,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("must match natural attribute"));
        }

        [Test]
        public void TryValidate_WithSecondaryButNoPrimary_IsRejected()
        {
            WeaponScalingModifier secondary = CreateModifier(CharacterAttribute.Luck, WeaponScalingGrade.D);
            WeaponInstanceModifiers serialized = CreateSerializedModifiers(default, secondary);

            Assert.That(
                serialized.TryValidate(CharacterAttribute.Strength, out string error),
                Is.False);
            Assert.That(error, Does.Contain("without a primary"));
        }

        [Test]
        public void TryCreate_WithRepeatedAttribute_IsRejected()
        {
            WeaponScalingModifier primary = CreateModifier(CharacterAttribute.Strength, WeaponScalingGrade.B);
            WeaponScalingModifier secondary = CreateModifier(CharacterAttribute.Strength, WeaponScalingGrade.D);

            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    primary,
                    secondary,
                    CharacterAttribute.Strength,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("same attribute"));
        }

        [Test]
        public void SerializedUnknownGrade_IsRejected()
        {
            WeaponScalingModifier primary = CreateSerializedModifier(
                CharacterAttribute.Strength,
                (WeaponScalingGrade)999);
            WeaponInstanceModifiers serialized = CreateSerializedModifiers(primary, default);

            Assert.That(
                serialized.TryValidate(CharacterAttribute.Strength, out string error),
                Is.False);
            Assert.That(error, Does.Contain("E through S"));
        }

        private static WeaponScalingModifier CreateModifier(
            CharacterAttribute attribute,
            WeaponScalingGrade grade)
        {
            Assert.That(
                WeaponScalingModifier.TryCreate(
                    attribute,
                    grade,
                    out WeaponScalingModifier modifier,
                    out string error),
                Is.True,
                error);
            return modifier;
        }

        private static WeaponScalingModifier CreateSerializedModifier(
            CharacterAttribute attribute,
            WeaponScalingGrade grade)
        {
            object boxed = default(WeaponScalingModifier);
            SetPrivateField(boxed, typeof(WeaponScalingModifier), "_attribute", attribute);
            SetPrivateField(boxed, typeof(WeaponScalingModifier), "_grade", grade);
            return (WeaponScalingModifier)boxed;
        }

        private static WeaponInstanceModifiers CreateSerializedModifiers(
            WeaponScalingModifier primary,
            WeaponScalingModifier secondary)
        {
            object boxed = default(WeaponInstanceModifiers);
            SetPrivateField(boxed, typeof(WeaponInstanceModifiers), "_primary", primary);
            SetPrivateField(boxed, typeof(WeaponInstanceModifiers), "_secondary", secondary);
            return (WeaponInstanceModifiers)boxed;
        }

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
