using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class WeaponCatalogTests
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";
        private const string TablePath =
            "Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset";
        private const string RecoveryConfigurationPath =
            "Assets/Resources/LocalProfilePersistenceConfiguration.asset";
        private const string ControllerPath = "Assets/Animations/Player/Character.controller";
        private const string WeaponArtRoot = "Assets/Art/Weapons/";

        private static readonly string[] WeaponIds =
        {
            "arming_sword",
            "compound_bow",
            "great_hammer",
            "light_crossbow",
            "long_bow",
            "long_sword",
            "magic_cinquedea",
            "magic_staff",
            "magic_sword",
            "magic_wand",
            "rapier",
            "rondel_dagger",
            "spell_book",
            "zweihander"
        };

        private static readonly string[] EquipmentIds = WeaponIds
            .Concat(new[] { "shield" })
            .ToArray();

        private static readonly Dictionary<string, ulong> DefaultLootWeights = new Dictionary<string, ulong>
        {
            ["arming_sword"] = 6,
            ["compound_bow"] = 1,
            ["great_hammer"] = 1,
            ["light_crossbow"] = 8,
            ["long_bow"] = 8,
            ["long_sword"] = 3,
            ["magic_cinquedea"] = 12,
            ["magic_staff"] = 3,
            ["magic_sword"] = 6,
            ["magic_wand"] = 10,
            ["rapier"] = 6,
            ["rondel_dagger"] = 12,
            ["spell_book"] = 10,
            ["zweihander"] = 3,
            ["shield"] = 5
        };

        private static readonly string[] LegacyIds =
        {
            "recovery_sword",
            "training_sword",
            "longsword",
            "greatsword",
            "wand",
            "staff",
            "spellbook",
            "training_shield"
        };

        private LootDefinitionCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
        }

        [Test]
        public void Catalog_ContainsOnlyCanonicalWeaponIdentities()
        {
            Assert.That(_catalog.TryValidate(out string error), Is.True, error);

            var actualEquipmentIds = new List<string>();
            for (int index = 0; index < _catalog.DefinitionCount; index++)
            {
                Assert.That(_catalog.TryGetByIndex(index, out LootDefinition definition), Is.True);
                if (definition.Category == LootCategory.Weapon || definition.Category == LootCategory.Shield)
                {
                    actualEquipmentIds.Add(definition.Id);
                }
            }

            Assert.That(actualEquipmentIds, Is.EquivalentTo(EquipmentIds));

            foreach (string id in EquipmentIds)
            {
                Assert.That(_catalog.TryGet(id, out LootDefinition definition), Is.True, id);
                Assert.That(definition, Is.Not.Null, id);
                Assert.That(definition.TryValidate(out error), Is.True, $"{id}: {error}");
                Assert.That(definition.ExtractionValuePerUnit, Is.EqualTo(10), id);
                Assert.That(definition.SellValuePerUnit, Is.Zero, id);
                Assert.That(definition.DefaultPickupQuantity, Is.EqualTo(1), id);
                Assert.That(definition.Rarity, Is.EqualTo(LootRarity.Common), id);
                Assert.That(AssetDatabase.GetAssetPath(definition.Icon), Does.StartWith(WeaponArtRoot), id);
                Assert.That(AssetDatabase.GetAssetPath(definition.WorldSprite), Does.StartWith(WeaponArtRoot), id);
                Assert.That(definition.Icon.name, Does.EndWith("_0"), id);
                Assert.That(definition.WorldSprite.name, Does.EndWith("_0"), id);
            }

            foreach (string legacyId in LegacyIds)
            {
                Assert.That(_catalog.TryGet(legacyId, out _), Is.False, legacyId);
            }
        }

        [Test]
        public void Weapons_MatchApprovedCombatConfiguration()
        {
            AssertWeapon("arming_sword", 30f, 1f, 1.5f, 15f, 5f, DamageType.Physical,
                WeaponHandedness.OneHanded, CharacterAttribute.Strength, 5, 0, 0,
                typeof(MeleeAttackConfig));
            AssertWeapon("rapier", 30f, 1f, 1.5f, 15f, 5f, DamageType.Physical,
                WeaponHandedness.OneHanded, CharacterAttribute.Strength, 5, 0, 0,
                typeof(MeleeAttackConfig));
            AssertWeapon("magic_sword", 30f, 1f, 1.5f, 15f, 5f, DamageType.Magical,
                WeaponHandedness.OneHanded, CharacterAttribute.Strength, 5, 0, 0,
                typeof(MeleeAttackConfig));
            AssertWeapon("long_sword", 45f, 1.4f, 2f, 22f, 10f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Strength, 10, 0, 0,
                typeof(MeleeAttackConfig));
            AssertWeapon("zweihander", 45f, 1.4f, 2f, 22f, 10f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Strength, 10, 0, 0,
                typeof(MeleeAttackConfig));
            AssertWeapon("great_hammer", 60f, 1.8f, 1.5f, 28f, 15f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Strength, 15, 0, 0,
                typeof(MeleeAttackConfig));
            AssertWeapon("rondel_dagger", 18f, 0.55f, 1f, 10f, 0f, DamageType.Physical,
                WeaponHandedness.OneHanded, CharacterAttribute.Dexterity, 0, 5, 0,
                typeof(MeleeAttackConfig));
            AssertWeapon("magic_cinquedea", 18f, 0.55f, 1f, 10f, 0f, DamageType.Magical,
                WeaponHandedness.OneHanded, CharacterAttribute.Dexterity, 0, 5, 0,
                typeof(MeleeAttackConfig));
            AssertWeapon("long_bow", 28f, 0.9f, 6f, 14f, 0f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Dexterity, 0, 10, 0,
                typeof(RangedAttackConfig));
            // GD 09 section 6: Ballesta, DES 15, 50 damage, 1.60 s, 8.0 tiles, 10 stamina, Physical, two-handed.
            AssertWeapon("light_crossbow", 50f, 1.6f, 8f, 10f, 0f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Dexterity, 0, 15, 0,
                typeof(RangedAttackConfig));
            AssertWeapon("compound_bow", 56f, 1.8f, 12f, 28f, 0f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Dexterity, 0, 10, 0,
                typeof(RangedAttackConfig));
            AssertWeapon("magic_wand", 22f, 0.7f, 5f, 10f, 0f, DamageType.Magical,
                WeaponHandedness.OneHanded, CharacterAttribute.Intelligence, 0, 0, 5,
                typeof(RangedAttackConfig));
            AssertWeapon("magic_staff", 45f, 1.4f, 7f, 22f, 0f, DamageType.Magical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Intelligence, 0, 0, 15,
                typeof(RangedAttackConfig));
            // GD 09 sections 5-6: Libro de hechizos, INT 10, 34 damage, 1.10 s, 2.5 tiles, 15 stamina, Magical, one-handed.
            AssertWeapon("spell_book", 34f, 1.1f, 2.5f, 15f, 0f, DamageType.Magical,
                WeaponHandedness.OneHanded, CharacterAttribute.Intelligence, 0, 0, 10,
                typeof(MeleeAttackConfig));
        }

        // GD 09 gives the Spellbook an "area / proximity" identity but no numbers: it hits several targets in a
        // wider circle than the shared single-target melee config, which other weapons keep using untouched.
        // Radius and target count are provisional balance values.
        [Test]
        public void Spellbook_UsesItsOwnAreaMeleeConfigWiderThanTheSharedOne()
        {
            Assert.That(_catalog.TryGet("spell_book", out LootDefinition loot), Is.True);
            WeaponDefinition weapon = loot.WeaponDefinition;
            var area = weapon.PrimaryAttack as MeleeAttackConfig;
            var shared = AssetDatabase.LoadAssetAtPath<MeleeAttackConfig>(
                "Assets/Scriptable Objects/PlayerMeleeAttackConfig.asset");
            Assert.That(area, Is.Not.Null);
            Assert.That(shared, Is.Not.Null);
            Assert.That(area, Is.Not.SameAs(shared));
            Assert.That(AssetDatabase.GetAssetPath(area), Is.EqualTo(
                "Assets/Scriptable Objects/SpellbookMeleeAttackConfig.asset"));
            Assert.That(area.TryValidate(out string error), Is.True, error);
            Assert.That(area.Radius, Is.GreaterThan(shared.Radius));
            Assert.That(area.MaximumTargets, Is.GreaterThan(1));
            Assert.That(area.TargetLayerMask.value, Is.EqualTo(shared.TargetLayerMask.value));
            Assert.That(weapon.Range, Is.GreaterThanOrEqualTo(area.Radius));
            // The shared config keeps its single-target contract for the seven weapons that use it.
            Assert.That(shared.Radius, Is.EqualTo(0.5f));
            Assert.That(shared.MaximumTargets, Is.EqualTo(1));
        }

        [Test]
        public void DefaultLootTable_ContainsEveryEquipmentDefinitionOnceAtConfiguredWeight()
        {
            LootContainerContentTable table =
                AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(TablePath);
            Assert.That(table, Is.Not.Null);

            Assert.That(table.MinimumDistinctStacks, Is.EqualTo(2));
            Assert.That(table.MaximumDistinctStacks, Is.EqualTo(3));
            Assert.That(table.AllowEmpty, Is.False);
            Assert.That(table.Entries.Count, Is.EqualTo(42));

            foreach (string id in EquipmentIds)
            {
                LootContainerContentTableEntry[] entries = table.Entries
                    .Where(entry => entry.Definition != null && entry.Definition.Id == id)
                    .ToArray();
                Assert.That(entries, Has.Length.EqualTo(1), id);
                Assert.That(entries[0].Weight, Is.EqualTo(DefaultLootWeights[id]), id);
                Assert.That(entries[0].MinimumAmount, Is.EqualTo(1), id);
                Assert.That(entries[0].MaximumAmount, Is.EqualTo(1), id);
            }
        }

        [Test]
        public void DefaultLootTable_UsesConfiguredConsumableWeightsAndAmounts()
        {
            LootContainerContentTable table =
                AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(TablePath);
            Assert.That(table, Is.Not.Null);

            AssertTableEntry(table, "coins", 50, 3, 8);
            AssertTableEntry(table, "bone", 40, 1, 3);
            AssertTableEntry(table, "healthpotion", 30, 1, 2);
        }

        [Test]
        public void EnemyLootTable_ContainsRequestedCataloguedEntriesAndBounds()
        {
            LootContainerContentTable table = AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(
                "Assets/Scriptable Objects/Loot/Tables/EnemyLootContainerContentTable.asset");
            Assert.That(table, Is.Not.Null);
            Assert.That(table.MinimumDistinctStacks, Is.EqualTo(1));
            Assert.That(table.MaximumDistinctStacks, Is.EqualTo(2));
            Assert.That(table.AllowEmpty, Is.False);

            AssertTableEntry(table, "coins", 45, 1, 4);
            AssertTableEntry(table, "bone", 35, 1, 2);
            AssertTableEntry(table, "healthpotion", 20, 1, 1);
            AssertTableEntry(table, "magic_cinquedea", 6, 1, 1);
            AssertTableEntry(table, "rondel_dagger", 6, 1, 1);
            AssertTableEntry(table, "magic_wand", 5, 1, 1);
            AssertTableEntry(table, "long_bow", 4, 1, 1);
            AssertTableEntry(table, "arming_sword", 3, 1, 1);
            AssertTableEntry(table, "rapier", 3, 1, 1);
            AssertTableEntry(table, "magic_sword", 3, 1, 1);
            AssertTableEntry(table, "long_sword", 1, 1, 1);
            AssertTableEntry(table, "zweihander", 1, 1, 1);
            AssertTableEntry(table, "magic_staff", 1, 1, 1);
            AssertTableEntry(table, "compound_bow", 1, 1, 1);
            AssertTableEntry(table, "great_hammer", 1, 1, 1);
            AssertTableEntry(table, "shield", 2, 1, 1);

            foreach (string id in ArmorIds)
            {
                AssertTableEntry(table, id, 1, 1, 1);
            }

            Assert.That(table.Entries.Count, Is.EqualTo(40));
            Assert.That(table.Entries.Select(entry => entry.Definition.Id).Distinct().Count(),
                Is.EqualTo(table.Entries.Count));
            Assert.That(_catalog.TryValidate(out string catalogError), Is.True, catalogError);
            Assert.That(LootContainerContentTableValidation.TryCreateSnapshot(
                table,
                _catalog,
                NetworkLootContainer.DefaultWorldSlotCapacity,
                NetworkLootContainer.MaxDistinctLootTypes,
                out ValidatedLootContainerContentSnapshot snapshot,
                out string validationError), Is.True, validationError);
            Assert.That(LootContainerContentTableValidation.HasAdditionalStackCapacity(
                snapshot,
                out string capacityError), Is.True, capacityError);
        }

        [Test]
        public void RecoveryConfiguration_UsesLootableArmingSword()
        {
            LocalProfilePersistenceConfiguration configuration =
                AssetDatabase.LoadAssetAtPath<LocalProfilePersistenceConfiguration>(RecoveryConfigurationPath);
            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.RecoveryWeaponLootId.Value, Is.EqualTo("arming_sword"));
            Assert.That(_catalog.TryGet("arming_sword", out _), Is.True);

            LootContainerContentTable table =
                AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(TablePath);
            Assert.That(table.Entries.Count(entry =>
                entry.Definition != null && entry.Definition.Id == "arming_sword"), Is.EqualTo(1));
        }

        [Test]
        public void CatalogWeapons_UseOnlyTheGenericAttackRoute()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null);
            AnimatorControllerLayer attackLayer =
                controller.layers.Single(layer => layer.name == "RightHand");

            Assert.That(controller.parameters.Any(parameter => parameter.name == "WeaponAnimationCategory"), Is.False);
            AnimatorStateTransition route = attackLayer.stateMachine.anyStateTransitions.Single();
            Assert.That(route.destinationState.name, Is.EqualTo("Attack"));
            Assert.That(route.conditions.Any(condition => condition.parameter == "HasGenericAttack" &&
                condition.mode == AnimatorConditionMode.If), Is.True);
            Assert.That(typeof(WeaponDefinition).Assembly.GetType("WeaponAnimationCategory"), Is.Null);

            foreach (string id in WeaponIds)
            {
                Assert.That(_catalog.TryGet(id, out LootDefinition definition), Is.True, id);
                Assert.That(definition.WeaponDefinition.Presentation.HasGenericAttack, Is.True, id);
                string weaponAsset = System.IO.File.ReadAllText(
                    AssetDatabase.GetAssetPath(definition.WeaponDefinition));
                Assert.That(weaponAsset, Does.Not.Contain("_animationCategory"), id);
            }
        }

        private void AssertWeapon(
            string id,
            float damage,
            float interval,
            float range,
            float stamina,
            float knockback,
            DamageType damageType,
            WeaponHandedness handedness,
            CharacterAttribute naturalAttribute,
            int strength,
            int dexterity,
            int intelligence,
            Type attackConfigType)
        {
            Assert.That(_catalog.TryGet(id, out LootDefinition loot), Is.True, id);
            Assert.That(loot.Category, Is.EqualTo(LootCategory.Weapon), id);
            WeaponDefinition weapon = loot.WeaponDefinition;
            Assert.That(weapon, Is.Not.Null, id);
            Assert.That(weapon.TryValidate(out string error), Is.True, $"{id}: {error}");
            Assert.That(weapon.BaseDamage, Is.EqualTo(damage).Within(0.001f), id);
            Assert.That(weapon.AttackIntervalSeconds, Is.EqualTo(interval).Within(0.001f), id);
            Assert.That(weapon.Range, Is.EqualTo(range).Within(0.001f), id);
            Assert.That(weapon.StaminaCost, Is.EqualTo(stamina).Within(0.001f), id);
            Assert.That(weapon.KnockbackForce, Is.EqualTo(knockback).Within(0.001f), id);
            Assert.That(weapon.DamageType, Is.EqualTo(damageType), id);
            Assert.That(weapon.Handedness, Is.EqualTo(handedness), id);
            Assert.That(weapon.NaturalScalingAttribute, Is.EqualTo(naturalAttribute), id);
            Assert.That(weapon.OffensiveScaling.HasScaling, Is.False, id);
            Assert.That(weapon.AttributeRequirements.MinimumStrength, Is.EqualTo(strength), id);
            Assert.That(weapon.AttributeRequirements.MinimumDexterity, Is.EqualTo(dexterity), id);
            Assert.That(weapon.AttributeRequirements.MinimumIntelligence, Is.EqualTo(intelligence), id);
            Assert.That(weapon.PrimaryAttack.GetType(), Is.EqualTo(attackConfigType), id);
        }

        private static readonly string[] ArmorIds =
        {
            "placeholder_helmet", "placeholder_armor", "placeholder_gloves", "placeholder_boots",
            "army_ranger_hat", "army_ranger_armor", "army_ranger_gloves", "army_ranger_trousers",
            "heavy_armor_helmet", "heavy_armor_breastplate", "heavy_armor_gauntlets", "heavy_armor_leg_plate",
            "light_armor_open_sallet", "light_armor_chain_mail_armor", "light_armor_gloves",
            "light_armor_chain_mail_trousers", "forest_ranger_hood", "forest_ranger_leather_armor",
            "forest_ranger_gloves", "forest_ranger_trousers", "fire_mage_hood", "fire_mage_garb",
            "fire_mage_gloves", "fire_mage_trousers"
        };

        private void AssertTableEntry(
            LootContainerContentTable table,
            string id,
            ulong expectedWeight,
            int expectedMinimumAmount,
            int expectedMaximumAmount)
        {
            LootContainerContentTableEntry[] entries = table.Entries
                .Where(entry => entry.Definition != null && entry.Definition.Id == id)
                .ToArray();
            Assert.That(entries, Has.Length.EqualTo(1), id);
            Assert.That(entries[0].Weight, Is.EqualTo(expectedWeight), id);
            Assert.That(entries[0].MinimumAmount, Is.EqualTo(expectedMinimumAmount), id);
            Assert.That(entries[0].MaximumAmount, Is.EqualTo(expectedMaximumAmount), id);
            Assert.That(_catalog.TryGet(id, out LootDefinition catalogDefinition), Is.True, id);
            Assert.That(entries[0].Definition, Is.SameAs(catalogDefinition), id);
        }
    }
}
