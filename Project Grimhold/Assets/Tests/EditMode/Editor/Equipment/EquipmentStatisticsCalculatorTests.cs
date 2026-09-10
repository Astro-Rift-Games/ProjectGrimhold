#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Equipment
{
    public sealed class EquipmentStatisticsCalculatorTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<ArmorDefinition> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < _created.Count; index++)
            {
                Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [Test]
        public void Calculate_NoArmor_ReturnsZeroSnapshot()
        {
            Assert.That(EquipmentStatisticsCalculator.TryCalculate(
                null, null, null, null,
                out EquipmentStatisticsModifiers modifiers,
                out EquipmentStatisticsCalculationFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(EquipmentStatisticsCalculationFailure.None));
            Assert.That(modifiers, Is.EqualTo(default(EquipmentStatisticsModifiers)));
        }

        [Test]
        public void Calculate_FourPieces_AggregatesDefensesAndResourceBonuses()
        {
            ArmorDefinition helmet = CreateArmor(1, 2, MaximumResourceType.Health, 10);
            ArmorDefinition armor = CreateArmor(3, 4, MaximumResourceType.Stamina, 20);
            ArmorDefinition gloves = CreateArmor(5, 6, MaximumResourceType.Mana, 30);
            ArmorDefinition boots = CreateArmor(7, 8, MaximumResourceType.Health, 40);

            Assert.That(EquipmentStatisticsCalculator.TryCalculate(
                helmet, armor, gloves, boots,
                out EquipmentStatisticsModifiers modifiers,
                out EquipmentStatisticsCalculationFailure failure), Is.True);

            Assert.That(failure, Is.EqualTo(EquipmentStatisticsCalculationFailure.None));
            Assert.That(modifiers.PhysicalDefense, Is.EqualTo(16));
            Assert.That(modifiers.MagicalDefense, Is.EqualTo(20));
            Assert.That(modifiers.MaximumHealthModifier, Is.EqualTo(50));
            Assert.That(modifiers.MaximumStaminaModifier, Is.EqualTo(20));
            Assert.That(modifiers.MaximumManaModifier, Is.EqualTo(30));
        }

        [Test]
        public void Calculate_RebuildsFromCurrentDefinitionsWithoutDrift()
        {
            ArmorDefinition previous = CreateArmor(10, 20, MaximumResourceType.Health, 30);
            ArmorDefinition replacement = CreateArmor(3, 4, MaximumResourceType.Mana, 5);

            Assert.That(EquipmentStatisticsCalculator.TryCalculate(
                previous, null, null, null, out EquipmentStatisticsModifiers first, out _), Is.True);
            Assert.That(EquipmentStatisticsCalculator.TryCalculate(
                replacement, null, null, null, out EquipmentStatisticsModifiers second, out _), Is.True);
            Assert.That(EquipmentStatisticsCalculator.TryCalculate(
                replacement, null, null, null, out EquipmentStatisticsModifiers repeated, out _), Is.True);

            Assert.That(second.PhysicalDefense, Is.EqualTo(3));
            Assert.That(second.MaximumHealthModifier, Is.Zero);
            Assert.That(second.MaximumManaModifier, Is.EqualTo(5));
            Assert.That(repeated, Is.EqualTo(second));
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void Calculate_DefenseOverflow_FailsExplicitly()
        {
            ArmorDefinition first = CreateArmor(int.MaxValue, 1, MaximumResourceType.Health, 1);
            ArmorDefinition second = CreateArmor(1, 1, MaximumResourceType.Health, 1);

            Assert.That(EquipmentStatisticsCalculator.TryCalculate(
                first, second, null, null,
                out EquipmentStatisticsModifiers modifiers,
                out EquipmentStatisticsCalculationFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(EquipmentStatisticsCalculationFailure.PhysicalDefenseOverflow));
            Assert.That(modifiers, Is.EqualTo(default(EquipmentStatisticsModifiers)));
        }

        [Test]
        public void Calculate_ResourceOverflow_FailsExplicitly()
        {
            ArmorDefinition first = CreateArmor(1, 1, MaximumResourceType.Mana, int.MaxValue);
            ArmorDefinition second = CreateArmor(1, 1, MaximumResourceType.Mana, 1);

            Assert.That(EquipmentStatisticsCalculator.TryCalculate(
                first, second, null, null,
                out _,
                out EquipmentStatisticsCalculationFailure failure), Is.False);

            Assert.That(failure, Is.EqualTo(
                EquipmentStatisticsCalculationFailure.MaximumManaModifierOverflow));
        }

        private ArmorDefinition CreateArmor(
            int physicalDefense,
            int magicalDefense,
            MaximumResourceType resource,
            int amount)
        {
            ArmorDefinition definition = ScriptableObject.CreateInstance<ArmorDefinition>();
            _created.Add(definition);
            SetField(definition, "_physicalDefense", physicalDefense);
            SetField(definition, "_magicalDefense", magicalDefense);
            SetField(definition, "_maximumResourceModifier", new MaximumResourceModifier(resource, amount));
            return definition;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
#endif
