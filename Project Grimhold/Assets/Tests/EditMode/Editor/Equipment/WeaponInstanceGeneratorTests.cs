#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Equipment
{
    public sealed class WeaponInstanceGeneratorTests
    {
        private const int GeneratedWeaponCount = 1000;
        private static readonly LootId FirstGem = new("RES_ATTUNEMENT_GEM");
        private static readonly LootId SecondGem = new("RES_TEST_GEM");
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _createdObjects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(_createdObjects[index]);
            }
            _createdObjects.Clear();
        }

        [TestCase(CharacterAttribute.Strength)]
        [TestCase(CharacterAttribute.Dexterity)]
        [TestCase(CharacterAttribute.Intelligence)]
        public void TryGenerate_OneThousandWeapons_AllRespectLegalCombinationsAndSocketLimits(
            CharacterAttribute naturalAttribute)
        {
            LootDefinition template = CreateWeaponTemplate(naturalAttribute);
            WeaponInstanceGenerationRules rules = CreateRules(5000, 5000, 5000);
            var socketCounts = new HashSet<int>();
            var grades = new HashSet<WeaponScalingGrade>();
            var secondaryAttributes = new HashSet<CharacterAttribute>();
            bool sawEmptySocket = false;
            bool sawOccupiedSocket = false;
            bool sawUnscaled = false;

            for (int seed = 0; seed < GeneratedWeaponCount; seed++)
            {
                Assert.That(
                    WeaponInstanceGenerator.TryGenerate(
                        template,
                        (ulong)seed,
                        rules,
                        out WeaponInstanceModifiers instance,
                        out string error),
                    Is.True,
                    error);

                Assert.That(instance.TryValidate(naturalAttribute, out string validationError), Is.True, validationError);
                Assert.That(instance.GemSocketCount, Is.InRange(0, WeaponInstanceModifiers.MaxGemSocketCount));
                socketCounts.Add(instance.GemSocketCount);

                if (instance.HasPrimary)
                {
                    Assert.That(instance.Primary.Attribute, Is.EqualTo(naturalAttribute));
                    grades.Add(instance.Primary.Grade);
                }
                else
                {
                    sawUnscaled = true;
                    Assert.That(instance.HasSecondary, Is.False);
                }

                if (instance.HasSecondary)
                {
                    Assert.That(instance.Secondary.Attribute, Is.Not.EqualTo(instance.Primary.Attribute));
                    secondaryAttributes.Add(instance.Secondary.Attribute);
                    grades.Add(instance.Secondary.Grade);
                }

                for (int index = 0; index < instance.GemSocketCount; index++)
                {
                    Assert.That(instance.TryGetGemSocket(index, out WeaponGemSocket socket), Is.True);
                    if (socket.IsOccupied)
                    {
                        sawOccupiedSocket = true;
                        Assert.That(socket.GemLootId, Is.EqualTo(FirstGem).Or.EqualTo(SecondGem));
                    }
                    else
                    {
                        sawEmptySocket = true;
                    }
                }
            }

            Assert.That(socketCounts, Is.EquivalentTo(new[] { 0, 1, 2 }));
            Assert.That(grades, Has.Count.EqualTo(6), "Every grade from E through S should be reachable.");
            Assert.That(secondaryAttributes, Has.Count.EqualTo(5));
            Assert.That(secondaryAttributes, Has.No.Member(naturalAttribute));
            Assert.That(sawUnscaled, Is.True);
            Assert.That(sawEmptySocket, Is.True);
            Assert.That(sawOccupiedSocket, Is.True);
        }

        [Test]
        public void TryGenerate_SameSeed_ProducesSameInstance()
        {
            LootDefinition template = CreateWeaponTemplate(CharacterAttribute.Dexterity);
            WeaponInstanceGenerationRules rules = CreateRules(5000, 5000, 5000);

            for (ulong seed = 0; seed < 50; seed++)
            {
                Assert.That(WeaponInstanceGenerator.TryGenerate(template, seed, rules, out WeaponInstanceModifiers first, out _), Is.True);
                Assert.That(WeaponInstanceGenerator.TryGenerate(template, seed, rules, out WeaponInstanceModifiers second, out _), Is.True);
                AssertSameInstance(first, second);
            }
        }

        [Test]
        public void TryGenerate_WithZeroChances_ProducesUnmodifiedInstance()
        {
            LootDefinition template = CreateWeaponTemplate(CharacterAttribute.Strength);
            Assert.That(
                WeaponInstanceGenerationRules.TryCreate(
                    0,
                    10000,
                    new[] { 1, 1, 1, 1, 1, 1 },
                    new[] { 1, 0, 0 },
                    10000,
                    new[] { FirstGem },
                    out WeaponInstanceGenerationRules rules,
                    out string rulesError),
                Is.True,
                rulesError);

            for (ulong seed = 0; seed < 100; seed++)
            {
                Assert.That(WeaponInstanceGenerator.TryGenerate(template, seed, rules, out WeaponInstanceModifiers instance, out string error), Is.True, error);
                Assert.That(instance.HasPrimary, Is.False);
                Assert.That(instance.HasSecondary, Is.False);
                Assert.That(instance.GemSocketCount, Is.Zero);
            }
        }

        [Test]
        public void TryGenerate_NonWeaponTemplate_IsRejected()
        {
            LootDefinition template = CreateLoot(LootCategory.Resource);

            Assert.That(
                WeaponInstanceGenerator.TryGenerate(template, 1, CreateRules(5000, 5000, 5000), out WeaponInstanceModifiers instance, out string error),
                Is.False);
            Assert.That(instance.GemSocketCount, Is.Zero);
            Assert.That(error, Does.Contain("weapon template"));
        }

        [Test]
        public void TryGenerate_NullTemplate_IsRejected()
        {
            Assert.That(
                WeaponInstanceGenerator.TryGenerate(null, 1, CreateRules(5000, 5000, 5000), out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("weapon template"));
        }

        [TestCase(CharacterAttribute.Vitality)]
        [TestCase(CharacterAttribute.Resistance)]
        [TestCase(CharacterAttribute.Luck)]
        public void TryGenerate_TemplateWithNonOffensiveNaturalAttribute_IsRejected(CharacterAttribute naturalAttribute)
        {
            LootDefinition template = CreateWeaponTemplate(naturalAttribute);

            Assert.That(
                WeaponInstanceGenerator.TryGenerate(template, 1, CreateRules(5000, 5000, 5000), out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("natural attribute"));
        }

        [Test]
        public void TryGenerate_DefaultRules_AreRejected()
        {
            LootDefinition template = CreateWeaponTemplate(CharacterAttribute.Strength);

            Assert.That(
                WeaponInstanceGenerator.TryGenerate(template, 1, default, out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("grade weights"));
        }

        [TestCase(-1, 0, 0, "primary scaling chance")]
        [TestCase(10001, 0, 0, "primary scaling chance")]
        [TestCase(0, -1, 0, "secondary scaling chance")]
        [TestCase(0, 10001, 0, "secondary scaling chance")]
        [TestCase(0, 0, -1, "gem install chance")]
        [TestCase(0, 0, 10001, "gem install chance")]
        public void TryCreateRules_WithChanceOutsideBasisPoints_IsRejected(
            int primaryChance,
            int secondaryChance,
            int gemChance,
            string expectedError)
        {
            Assert.That(
                WeaponInstanceGenerationRules.TryCreate(
                    primaryChance,
                    secondaryChance,
                    new[] { 1, 1, 1, 1, 1, 1 },
                    new[] { 1, 1, 1 },
                    gemChance,
                    new[] { FirstGem },
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain(expectedError));
        }

        [Test]
        public void TryCreateRules_WithInvalidGradeWeights_IsRejected()
        {
            Assert.That(CreateRulesWith(new[] { 1, 1, 1 }, new[] { 1, 1, 1 }, new[] { FirstGem }, out string lengthError), Is.False);
            Assert.That(lengthError, Does.Contain("grade weights"));
            Assert.That(CreateRulesWith(new[] { 1, 1, 1, 1, 1, -1 }, new[] { 1, 1, 1 }, new[] { FirstGem }, out string negativeError), Is.False);
            Assert.That(negativeError, Does.Contain("grade weights"));
            Assert.That(CreateRulesWith(new[] { 0, 0, 0, 0, 0, 0 }, new[] { 1, 1, 1 }, new[] { FirstGem }, out string emptyError), Is.False);
            Assert.That(emptyError, Does.Contain("grade weights"));
        }

        [Test]
        public void TryCreateRules_WithInvalidSocketWeights_IsRejected()
        {
            Assert.That(CreateRulesWith(new[] { 1, 1, 1, 1, 1, 1 }, new[] { 1, 1 }, new[] { FirstGem }, out string lengthError), Is.False);
            Assert.That(lengthError, Does.Contain("gem socket count weights"));
            Assert.That(CreateRulesWith(new[] { 1, 1, 1, 1, 1, 1 }, new[] { 0, 0, 0 }, new[] { FirstGem }, out string emptyError), Is.False);
            Assert.That(emptyError, Does.Contain("gem socket count weights"));
        }

        [Test]
        public void TryCreateRules_WithGemChanceAndNoValidGem_IsRejected()
        {
            Assert.That(CreateRulesWith(new[] { 1, 1, 1, 1, 1, 1 }, new[] { 1, 1, 1 }, new LootId[0], out string emptyError), Is.False);
            Assert.That(emptyError, Does.Contain("gem pool"));
            Assert.That(CreateRulesWith(new[] { 1, 1, 1, 1, 1, 1 }, new[] { 1, 1, 1 }, new[] { default(LootId) }, out string invalidError), Is.False);
            Assert.That(invalidError, Does.Contain("gem pool"));
        }

        private static bool CreateRulesWith(int[] gradeWeights, int[] socketWeights, LootId[] gemPool, out string error) =>
            WeaponInstanceGenerationRules.TryCreate(5000, 5000, gradeWeights, socketWeights, 5000, gemPool, out _, out error);

        private static WeaponInstanceGenerationRules CreateRules(int primaryChance, int secondaryChance, int gemChance)
        {
            Assert.That(
                WeaponInstanceGenerationRules.TryCreate(
                    primaryChance,
                    secondaryChance,
                    new[] { 30, 25, 20, 13, 8, 4 },
                    new[] { 50, 35, 15 },
                    gemChance,
                    new[] { FirstGem, SecondGem },
                    out WeaponInstanceGenerationRules rules,
                    out string error),
                Is.True,
                error);
            return rules;
        }

        private static void AssertSameInstance(in WeaponInstanceModifiers expected, in WeaponInstanceModifiers actual)
        {
            Assert.That(actual.Primary.Attribute, Is.EqualTo(expected.Primary.Attribute));
            Assert.That(actual.Primary.Grade, Is.EqualTo(expected.Primary.Grade));
            Assert.That(actual.Secondary.Attribute, Is.EqualTo(expected.Secondary.Attribute));
            Assert.That(actual.Secondary.Grade, Is.EqualTo(expected.Secondary.Grade));
            Assert.That(actual.GemSocketCount, Is.EqualTo(expected.GemSocketCount));
            for (int index = 0; index < expected.GemSocketCount; index++)
            {
                expected.TryGetGemSocket(index, out WeaponGemSocket expectedSocket);
                actual.TryGetGemSocket(index, out WeaponGemSocket actualSocket);
                Assert.That(actualSocket.IsOccupied, Is.EqualTo(expectedSocket.IsOccupied));
                Assert.That(actualSocket.GemLootId, Is.EqualTo(expectedSocket.GemLootId));
            }
        }

        private LootDefinition CreateWeaponTemplate(CharacterAttribute naturalAttribute)
        {
            WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            _createdObjects.Add(weapon);
            SetPrivateField(weapon, "_naturalScalingAttribute", naturalAttribute);

            LootDefinition loot = CreateLoot(LootCategory.Weapon);
            SetPrivateField(loot, "_weaponDefinition", weapon);
            return loot;
        }

        private LootDefinition CreateLoot(LootCategory category)
        {
            LootDefinition loot = ScriptableObject.CreateInstance<LootDefinition>();
            _createdObjects.Add(loot);
            SetPrivateField(loot, "_category", category);
            return loot;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
