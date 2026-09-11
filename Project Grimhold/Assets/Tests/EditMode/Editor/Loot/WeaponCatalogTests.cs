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
            "long_bow",
            "long_sword",
            "magic_cinquedea",
            "magic_staff",
            "magic_sword",
            "magic_wand",
            "rapier",
            "rondel_dagger",
            "zweihander"
        };

        private static readonly string[] EquipmentIds = WeaponIds
            .Concat(new[] { "shield" })
            .ToArray();

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
                WeaponAnimationCategory.ArmingSword, typeof(MeleeAttackConfig));
            AssertWeapon("rapier", 30f, 1f, 1.5f, 15f, 5f, DamageType.Physical,
                WeaponHandedness.OneHanded, CharacterAttribute.Strength, 5, 0, 0,
                WeaponAnimationCategory.Rapier, typeof(MeleeAttackConfig));
            AssertWeapon("magic_sword", 30f, 1f, 1.5f, 15f, 5f, DamageType.Magical,
                WeaponHandedness.OneHanded, CharacterAttribute.Strength, 5, 0, 0,
                WeaponAnimationCategory.ArmingSword, typeof(MeleeAttackConfig));
            AssertWeapon("long_sword", 45f, 1.4f, 2f, 22f, 10f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Strength, 10, 0, 0,
                WeaponAnimationCategory.ArmingSword, typeof(MeleeAttackConfig));
            AssertWeapon("zweihander", 45f, 1.4f, 2f, 22f, 10f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Strength, 10, 0, 0,
                WeaponAnimationCategory.ArmingSword, typeof(MeleeAttackConfig));
            AssertWeapon("rondel_dagger", 18f, 0.55f, 1f, 10f, 0f, DamageType.Physical,
                WeaponHandedness.OneHanded, CharacterAttribute.Dexterity, 0, 5, 0,
                WeaponAnimationCategory.RondelDagger, typeof(MeleeAttackConfig));
            AssertWeapon("magic_cinquedea", 18f, 0.55f, 1f, 10f, 0f, DamageType.Magical,
                WeaponHandedness.OneHanded, CharacterAttribute.Dexterity, 0, 5, 0,
                WeaponAnimationCategory.RondelDagger, typeof(MeleeAttackConfig));
            AssertWeapon("long_bow", 28f, 0.9f, 6f, 14f, 0f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Dexterity, 0, 10, 0,
                WeaponAnimationCategory.MagicWand, typeof(RangedAttackConfig));
            AssertWeapon("compound_bow", 56f, 1.8f, 12f, 28f, 0f, DamageType.Physical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Dexterity, 0, 10, 0,
                WeaponAnimationCategory.MagicWand, typeof(RangedAttackConfig));
            AssertWeapon("magic_wand", 22f, 0.7f, 5f, 10f, 0f, DamageType.Magical,
                WeaponHandedness.OneHanded, CharacterAttribute.Intelligence, 0, 0, 5,
                WeaponAnimationCategory.MagicWand, typeof(RangedAttackConfig));
            AssertWeapon("magic_staff", 45f, 1.4f, 7f, 22f, 0f, DamageType.Magical,
                WeaponHandedness.TwoHanded, CharacterAttribute.Intelligence, 0, 0, 15,
                WeaponAnimationCategory.MagicWand, typeof(RangedAttackConfig));
        }

        [Test]
        public void DefaultLootTable_ContainsEveryEquipmentDefinitionOnceAtUniformWeight()
        {
            LootContainerContentTable table =
                AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(TablePath);
            Assert.That(table, Is.Not.Null);

            foreach (string id in EquipmentIds)
            {
                LootContainerContentTableEntry[] entries = table.Entries
                    .Where(entry => entry.Definition != null && entry.Definition.Id == id)
                    .ToArray();
                Assert.That(entries, Has.Length.EqualTo(1), id);
                Assert.That(entries[0].Weight, Is.EqualTo(6), id);
                Assert.That(entries[0].MinimumAmount, Is.EqualTo(1), id);
                Assert.That(entries[0].MaximumAmount, Is.EqualTo(1), id);
            }
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
        public void CatalogWeapons_UseOnlyAnimatorSupportedAttackCategories()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null);
            AnimatorControllerLayer attackLayer =
                controller.layers.Single(layer => layer.name == "RightHand");

            var supported = new HashSet<WeaponAnimationCategory>();
            foreach (AnimatorStateTransition transition in attackLayer.stateMachine.anyStateTransitions)
            {
                AnimatorCondition condition = transition.conditions.SingleOrDefault(candidate =>
                    candidate.parameter == "WeaponAnimationCategory");
                if (!string.IsNullOrEmpty(condition.parameter))
                {
                    supported.Add((WeaponAnimationCategory)(int)condition.threshold);
                }
            }

            Assert.That(supported, Is.EquivalentTo(new[]
            {
                WeaponAnimationCategory.ArmingSword,
                WeaponAnimationCategory.Rapier,
                WeaponAnimationCategory.RondelDagger,
                WeaponAnimationCategory.MagicWand
            }));

            foreach (string id in WeaponIds)
            {
                _catalog.TryGet(id, out LootDefinition definition);
                Assert.That(supported, Does.Contain(definition.WeaponDefinition.Presentation.AnimationCategory), id);
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
            WeaponAnimationCategory animationCategory,
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
            Assert.That(weapon.Presentation.AnimationCategory, Is.EqualTo(animationCategory), id);
            Assert.That(weapon.PrimaryAttack.GetType(), Is.EqualTo(attackConfigType), id);
        }
    }
}
