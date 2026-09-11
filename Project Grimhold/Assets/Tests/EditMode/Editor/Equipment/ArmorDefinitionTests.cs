#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Equipment
{
    public sealed class ArmorDefinitionTests
    {
        private const string DefinitionRoot = "Assets/Scriptable Objects/Loot/Definitions/";

        [TestCase("HeavyArmorHelmet", 16, 4, MaximumResourceType.Health, 5)]
        [TestCase("HeavyArmorBreastplate", 26, 6, MaximumResourceType.Health, 8)]
        [TestCase("HeavyArmorGauntlets", 10, 2, MaximumResourceType.Health, 3)]
        [TestCase("HeavyArmorLegPlate", 13, 3, MaximumResourceType.Health, 4)]
        [TestCase("LightArmorOpenSallet", 14, 6, MaximumResourceType.Health, 5)]
        [TestCase("LightArmorChainMailArmor", 22, 10, MaximumResourceType.Health, 8)]
        [TestCase("LightArmorGloves", 8, 4, MaximumResourceType.Health, 3)]
        [TestCase("LightArmorChainMailTrousers", 11, 5, MaximumResourceType.Health, 4)]
        [TestCase("ArmyRangerHat", 11, 9, MaximumResourceType.Stamina, 5)]
        [TestCase("ArmyRangerArmor", 18, 14, MaximumResourceType.Stamina, 8)]
        [TestCase("ArmyRangerGloves", 7, 5, MaximumResourceType.Stamina, 3)]
        [TestCase("ArmyRangerTrousers", 9, 7, MaximumResourceType.Stamina, 4)]
        [TestCase("ForestRangerHood", 9, 11, MaximumResourceType.Stamina, 5)]
        [TestCase("ForestRangerLeatherArmor", 14, 18, MaximumResourceType.Stamina, 8)]
        [TestCase("ForestRangerGloves", 5, 7, MaximumResourceType.Stamina, 3)]
        [TestCase("ForestRangerTrousers", 7, 9, MaximumResourceType.Stamina, 4)]
        [TestCase("ArcaneMageHat", 4, 16, MaximumResourceType.Mana, 8)]
        [TestCase("ArcaneMageGarb", 6, 26, MaximumResourceType.Mana, 12)]
        [TestCase("ArcaneMageGloves", 2, 10, MaximumResourceType.Mana, 4)]
        [TestCase("ArcaneMageTrousers", 3, 13, MaximumResourceType.Mana, 6)]
        [TestCase("FireMageHood", 6, 14, MaximumResourceType.Mana, 8)]
        [TestCase("FireMageGarb", 10, 22, MaximumResourceType.Mana, 12)]
        [TestCase("FireMageGloves", 4, 8, MaximumResourceType.Mana, 4)]
        [TestCase("FireMageTrousers", 5, 11, MaximumResourceType.Mana, 6)]
        public void ArmorLoot_ReferencesTheConfiguredMvpDefinition(
            string lootName,
            int physicalDefense,
            int magicalDefense,
            MaximumResourceType resource,
            int resourceAmount)
        {
            LootDefinition loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                $"{DefinitionRoot}{lootName}.asset");

            Assert.That(loot, Is.Not.Null, $"Missing loot definition for {lootName}.");
            Assert.That(loot.ArmorDefinition, Is.Not.Null, $"{lootName} has no ArmorDefinition.");
            Assert.That(loot.TryValidate(out string error), Is.True, error);
            Assert.That(loot.ArmorDefinition.PhysicalDefense, Is.EqualTo(physicalDefense));
            Assert.That(loot.ArmorDefinition.MagicalDefense, Is.EqualTo(magicalDefense));
            Assert.That(loot.ArmorDefinition.MaximumResourceModifier.Resource, Is.EqualTo(resource));
            Assert.That(loot.ArmorDefinition.MaximumResourceModifier.Amount, Is.EqualTo(resourceAmount));
        }

        [Test]
        public void EmptyArmorDefinition_IsRejected()
        {
            ArmorDefinition definition = ScriptableObject.CreateInstance<ArmorDefinition>();
            try
            {
                Assert.That(definition.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("no functional statistics"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void NegativeDefense_IsRejected()
        {
            ArmorDefinition definition = ScriptableObject.CreateInstance<ArmorDefinition>();
            try
            {
                SetPrivateField(definition, "_physicalDefense", -1);
                SetPrivateField(
                    definition,
                    "_maximumResourceModifier",
                    new MaximumResourceModifier(MaximumResourceType.Health, 1));

                Assert.That(definition.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("negative defense"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ArmorWithoutMaximumResourceModifier_IsRejected()
        {
            ArmorDefinition definition = ScriptableObject.CreateInstance<ArmorDefinition>();
            try
            {
                SetPrivateField(definition, "_physicalDefense", 1);

                Assert.That(definition.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("maximum resource modifier"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(MaximumResourceType.None, 1)]
        [TestCase(MaximumResourceType.Health, 0)]
        [TestCase((MaximumResourceType)999, 1)]
        public void InvalidMaximumResourceModifier_IsRejected(
            MaximumResourceType resource,
            int amount)
        {
            var modifier = new MaximumResourceModifier(resource, amount);

            Assert.That(modifier.TryValidate(out _), Is.False);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }
    }
}
#endif
