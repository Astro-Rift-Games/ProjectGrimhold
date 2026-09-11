#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class EquipmentTooltipPresentationBuilderTests
    {
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

        [TestCase(0.25f, "FUE E")]
        [TestCase(0.40f, "FUE D")]
        [TestCase(0.55f, "FUE C")]
        [TestCase(0.70f, "FUE B")]
        [TestCase(0.85f, "FUE A")]
        [TestCase(1.00f, "FUE S")]
        public void Build_ExactLegacyCoefficientUsesCanonicalGrade(float coefficient, string expected)
        {
            LootDefinition loot = CreateWeaponLoot("Espada", coefficient);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(presentation.Title, Is.EqualTo("Espada"));
            Assert.That(presentation.Body, Does.Contain($"Escalado: {expected}"));
        }

        [Test]
        public void Build_NonCanonicalLegacyCoefficientPreservesNumericValue()
        {
            LootDefinition loot = CreateWeaponLoot("Espada irregular", 0.63f);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Body, Does.Contain("Escalado: FUE ×0,63"));
            Assert.That(presentation.Body, Does.Not.Contain("FUE B"));
            Assert.That(presentation.Body, Does.Not.Contain("FUE C"));
        }

        [Test]
        public void Build_WeaponIncludesDefinitionOwnedFunctionalStatistics()
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0f);
            WeaponDefinition weapon = loot.WeaponDefinition;
            SetPrivateField(weapon, "_attributeRequirements", new WeaponAttributeRequirements(10, 0, 5));

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Body, Does.Contain("Daño base: 30 (Físico)"));
            Assert.That(presentation.Body, Does.Contain("Intervalo: 0,75 s"));
            Assert.That(presentation.Body, Does.Contain("Alcance: 1,5"));
            Assert.That(presentation.Body, Does.Contain("Costo de Stamina: 8"));
            Assert.That(presentation.Body, Does.Contain("Manos: 1"));
            Assert.That(presentation.Body, Does.Contain("Requisito: FUE 10 · INT 5"));
            Assert.That(
                presentation.Body,
                Does.Contain($"Requisito: FUE 10 · INT 5{System.Environment.NewLine}Escalado: Sin escalado"));
            Assert.That(presentation.Body, Does.Contain("Escalado: Sin escalado"));
        }

        [TestCase(MaximumResourceType.Health, "Bono: +5 Vida máxima")]
        [TestCase(MaximumResourceType.Stamina, "Bono: +5 Stamina máxima")]
        [TestCase(MaximumResourceType.Mana, "Bono: +5 Mana máximo")]
        public void Build_ArmorIncludesDefenseAndDirectResourceBonus(
            MaximumResourceType resource,
            string expectedBonus)
        {
            ArmorDefinition armor = Create<ArmorDefinition>();
            SetPrivateField(armor, "_physicalDefense", 16);
            SetPrivateField(armor, "_magicalDefense", 4);
            SetPrivateField(armor, "_maximumResourceModifier", new MaximumResourceModifier(resource, 5));
            LootDefinition loot = CreateLoot("Casco", LootCategory.Helmet);
            SetPrivateField(loot, "_armorDefinition", armor);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(presentation.Body, Does.Contain("Defensa Física: 16"));
            Assert.That(presentation.Body, Does.Contain("Defensa Mágica: 4"));
            Assert.That(presentation.Body, Does.Contain(expectedBonus));
        }

        [Test]
        public void Build_CurrentNonEquipmentCategoryShowsNoStatisticsMessage()
        {
            LootDefinition loot = CreateLoot("Mineral", LootCategory.Material);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Title, Is.EqualTo("Mineral"));
            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.NoEquipmentStatistics));
            Assert.That(presentation.Body, Is.EqualTo("Sin estadísticas de equipamiento"));
        }

        [TestCase(LootCategory.Weapon)]
        [TestCase(LootCategory.Armor)]
        public void Build_MissingFunctionalDefinitionKeepsTitleAndReportsInvalidConfiguration(
            LootCategory category)
        {
            LootDefinition loot = CreateLoot("Objeto roto", category);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Title, Is.EqualTo("Objeto roto"));
            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.InvalidEquipmentConfiguration));
            Assert.That(presentation.Body, Is.EqualTo("Estadísticas no disponibles por configuración inválida"));
        }

        [Test]
        public void Build_UnresolvedDefinitionCannotShowTooltip()
        {
            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(null);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.UnresolvedDefinition));
            Assert.That(presentation.CanShow, Is.False);
            Assert.That(presentation.Title, Is.Empty);
        }

        private LootDefinition CreateWeaponLoot(string displayName, float coefficient)
        {
            MeleeAttackConfig attack = Create<MeleeAttackConfig>();
            SetPrivateField(attack, "_radius", 0.5f);
            SetPrivateField(attack, "_maximumTargets", 1);
            SetPrivateField(attack, "_targetLayerMask", new LayerMask { value = 1 });

            WeaponDefinition weapon = Create<WeaponDefinition>();
            SetPrivateField(weapon, "_baseDamage", 30f);
            SetPrivateField(weapon, "_attackIntervalSeconds", 0.75f);
            SetPrivateField(weapon, "_range", 1.5f);
            SetPrivateField(weapon, "_staminaCost", 8f);
            SetPrivateField(weapon, "_damageType", DamageType.Physical);
            SetPrivateField(weapon, "_knockbackForce", 0f);
            SetPrivateField(weapon, "_handedness", WeaponHandedness.OneHanded);
            SetPrivateField(weapon, "_primaryAttack", attack);
            SetPrivateField(weapon, "_naturalScalingAttribute", CharacterAttribute.Strength);
            SetPrivateField(
                weapon,
                "_offensiveScaling",
                new WeaponOffensiveScaling(CharacterAttribute.Strength, coefficient));

            LootDefinition loot = CreateLoot(displayName, LootCategory.Weapon);
            SetPrivateField(loot, "_weaponDefinition", weapon);
            return loot;
        }

        private LootDefinition CreateLoot(string displayName, LootCategory category)
        {
            LootDefinition loot = Create<LootDefinition>();
            SetPrivateField(loot, "_displayName", displayName);
            SetPrivateField(loot, "_category", category);
            return loot;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
