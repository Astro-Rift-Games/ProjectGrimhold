using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    /// <summary>
    /// Validates the shipped placeholder weapon set. Every weapon must resolve its own
    /// <see cref="WeaponDefinition"/> and presentation from the shared catalog identity, without
    /// introducing a weapon subtype, per-weapon presenter branch or replicated presentation state.
    /// </summary>
    public sealed class PlaceholderWeaponCatalogTests
    {
        private const string CatalogPath =
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";

        private const string ContentTablePath =
            "Assets/Scriptable Objects/Loot/Tables/DefaultLootContainerContentTable.asset";

        private const string RecoverySword = "recovery_sword";
        private const string TrainingSword = "training_sword";
        private const string Longsword = "longsword";
        private const string Greatsword = "greatsword";
        private const string Wand = "wand";
        private const string Staff = "staff";
        private const string Spellbook = "spellbook";

        private static readonly string[] MeleeWeaponIds = { RecoverySword, Longsword, Greatsword };
        private static readonly string[] RangedWeaponIds = { Wand, Staff, Spellbook };

        /// <summary>
        /// Catalog entries serialized before the placeholder weapon set was added, in their
        /// original order. New content must be appended, never inserted or reordered.
        /// </summary>
        private static readonly string[] PreExistingCatalogOrder =
        {
            "bone",
            "coins",
            "healthpotion",
            TrainingSword,
            RecoverySword
        };

        private LootDefinitionCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, $"Missing catalog asset at '{CatalogPath}'.");
        }

        [Test]
        public void Catalog_IsValidAndResolvesEveryPlaceholderWeapon()
        {
            Assert.That(_catalog.TryValidate(out string error), Is.True, error);

            foreach (string lootId in AllWeaponIds())
            {
                Assert.That(
                    _catalog.TryGet(lootId, out LootDefinition definition),
                    Is.True,
                    $"Catalog cannot resolve weapon '{lootId}'.");
                Assert.That(definition.Category, Is.EqualTo(LootCategory.Weapon), lootId);
                Assert.That(definition.WeaponDefinition, Is.Not.Null, lootId);
                Assert.That(
                    definition.WeaponDefinition.TryValidate(out string weaponError),
                    Is.True,
                    weaponError);
                Assert.That(definition.WorldSprite, Is.Not.Null, lootId);
                Assert.That(definition.Icon, Is.Not.Null, lootId);
            }
        }

        [Test]
        public void WeaponAssets_ExposeTheConfiguredMvpAttributeRequirements()
        {
            AssertRequirements(RecoverySword, 0, 0, 0);
            AssertRequirements(TrainingSword, 0, 0, 0);
            AssertRequirements(Longsword, 5, 0, 0);
            AssertRequirements(Greatsword, 10, 0, 0);
            AssertRequirements(Wand, 0, 0, 5);
            AssertRequirements(Spellbook, 0, 0, 10);
            AssertRequirements(Staff, 0, 0, 15);
        }

        [Test]
        public void WeaponAssets_ExposeTheConfiguredMvpOffensiveScaling()
        {
            AssertNoScaling(RecoverySword);
            AssertNoScaling(TrainingSword);
            AssertScaling(Longsword, CharacterAttribute.Strength, 0.70f);
            AssertScaling(Greatsword, CharacterAttribute.Strength, 0.55f);
            AssertScaling(Wand, CharacterAttribute.Intelligence, 0.70f);
            AssertScaling(Spellbook, CharacterAttribute.Intelligence, 0.55f);
            AssertScaling(Staff, CharacterAttribute.Intelligence, 0.85f);
        }

        [Test]
        public void Catalog_ContainsNoDuplicateLootIdOrDefinitionReference()
        {
            var seenIds = new HashSet<string>();
            var seenDefinitions = new HashSet<LootDefinition>();

            foreach (LootDefinition definition in ReadSerializedDefinitions())
            {
                Assert.That(definition, Is.Not.Null, "Catalog contains a null definition entry.");
                Assert.That(seenIds.Add(definition.Id), Is.True, $"Duplicate loot id '{definition.Id}'.");
                Assert.That(seenDefinitions.Add(definition), Is.True, $"Duplicate reference '{definition.name}'.");
            }
        }

        [Test]
        public void Catalog_PreservesThePreExistingEntryOrder()
        {
            List<LootDefinition> definitions = ReadSerializedDefinitions();

            Assert.That(
                definitions.Count,
                Is.GreaterThanOrEqualTo(PreExistingCatalogOrder.Length),
                "Catalog entries were removed.");

            for (int index = 0; index < PreExistingCatalogOrder.Length; index++)
            {
                Assert.That(
                    definitions[index].Id,
                    Is.EqualTo(PreExistingCatalogOrder[index]),
                    $"Catalog entry {index} changed. New weapons must be appended, not inserted.");
            }
        }

        [Test]
        public void MeleeWeapons_ResolveAValidMeleeAttackConfiguration()
        {
            foreach (string lootId in MeleeWeaponIds)
            {
                AttackConfig attack = ResolveWeapon(lootId).PrimaryAttack;
                Assert.That(attack, Is.InstanceOf<MeleeAttackConfig>(), lootId);
                Assert.That(attack.TryValidate(out string error), Is.True, error);
            }
        }

        [Test]
        public void RangedWeapons_ResolveAValidRangedAttackConfiguration()
        {
            foreach (string lootId in RangedWeaponIds)
            {
                AttackConfig attack = ResolveWeapon(lootId).PrimaryAttack;
                Assert.That(attack, Is.InstanceOf<RangedAttackConfig>(), lootId);
                Assert.That(attack.TryValidate(out string error), Is.True, error);
            }
        }

        [Test]
        public void EveryWeapon_OwnsItsOwnWeaponDefinitionAsset()
        {
            var seen = new Dictionary<WeaponDefinition, string>();

            foreach (string lootId in AllWeaponIds())
            {
                WeaponDefinition weapon = ResolveWeapon(lootId);
                Assert.That(
                    seen.TryGetValue(weapon, out string owner),
                    Is.False,
                    $"'{lootId}' shares WeaponDefinition '{weapon.name}' with '{owner}'.");
                seen.Add(weapon, lootId);
            }
        }

        /// <summary>
        /// The presenter must stay identity agnostic, so each silhouette needs its own static
        /// presentation triple instead of a branch inside <see cref="PlayerWeaponPresenter"/>.
        /// </summary>
        [Test]
        public void EveryWeapon_OwnsADistinctPresentationConfiguration()
        {
            var seen = new Dictionary<(Vector2, Vector2, float), string>();

            foreach (string lootId in AllWeaponIds())
            {
                if (lootId == TrainingSword)
                {
                    // Training Sword deliberately keeps the Recovery Sword presentation.
                    continue;
                }

                WeaponDefinition.PresentationConfig presentation = ResolveWeapon(lootId).Presentation;
                Assert.That(presentation.TryValidate(out string error), Is.True, error);

                var key = (presentation.StanceOffset, presentation.GripPoint, presentation.AngleCorrection);
                Assert.That(
                    seen.TryGetValue(key, out string owner),
                    Is.False,
                    $"'{lootId}' repeats the presentation configuration of '{owner}'.");
                seen.Add(key, lootId);
            }
        }

        /// <summary>
        /// The spellbook silhouette is not blade aligned, so it must not reuse the sword angle
        /// correction. It proves the generic presentation can host a non-sword weapon.
        /// </summary>
        [Test]
        public void Spellbook_DoesNotReuseTheSwordAngleCorrection()
        {
            float swordAngle = ResolveWeapon(RecoverySword).Presentation.AngleCorrection;
            float spellbookAngle = ResolveWeapon(Spellbook).Presentation.AngleCorrection;

            Assert.That(spellbookAngle, Is.Not.EqualTo(swordAngle).Within(0.001f));
        }

        [Test]
        public void RecoverySword_RemainsAUsableRecoveryWeaponWithoutEconomicValue()
        {
            LocalProfilePersistenceConfiguration configuration =
                Resources.Load<LocalProfilePersistenceConfiguration>(
                    "LocalProfilePersistenceConfiguration");

            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.RecoveryWeaponLootId.Value, Is.EqualTo(RecoverySword));
            Assert.That(
                PreparedEquipmentLoadout.IsUsableWeaponDefinition(
                    configuration.RecoveryWeaponLootId,
                    configuration.LootCatalog),
                Is.True);

            Assert.That(_catalog.TryGet(RecoverySword, out LootDefinition definition), Is.True);
            Assert.That(definition.ExtractionValuePerUnit, Is.Zero);
            Assert.That(definition.SellValuePerUnit, Is.Zero);
        }

        /// <summary>
        /// Loot containers are the only acquisition route available during development, so the
        /// content table must expose every equippable placeholder weapon. Recovery Sword stays out
        /// of it: Town grants it through the recovery policy instead.
        /// </summary>
        [Test]
        public void LootContainerContentTable_ExposesEveryEquippablePlaceholderWeapon()
        {
            var table = AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(ContentTablePath);
            Assert.That(table, Is.Not.Null);

            var tableIds = new HashSet<string>();
            SerializedProperty entries = new SerializedObject(table).FindProperty("_entries");
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                var definition = entry.FindPropertyRelative("_definition").objectReferenceValue as LootDefinition;

                Assert.That(definition, Is.Not.Null, $"Loot table entry {index} has no definition.");
                Assert.That(tableIds.Add(definition.Id), Is.True, $"Duplicate loot table entry '{definition.Id}'.");
                Assert.That(entry.FindPropertyRelative("_weight").intValue, Is.GreaterThan(0), definition.Id);
                Assert.That(entry.FindPropertyRelative("_minimumAmount").intValue, Is.GreaterThan(0), definition.Id);
                Assert.That(
                    entry.FindPropertyRelative("_maximumAmount").intValue,
                    Is.GreaterThanOrEqualTo(entry.FindPropertyRelative("_minimumAmount").intValue),
                    definition.Id);
            }

            foreach (string lootId in new[] { Longsword, Greatsword, Wand, Staff, Spellbook })
            {
                Assert.That(tableIds, Does.Contain(lootId), $"'{lootId}' is not obtainable from loot containers.");
            }

            Assert.That(
                tableIds,
                Does.Not.Contain(RecoverySword),
                "Recovery Sword must stay out of loot distribution and remain a Town recovery grant.");
        }

        [Test]
        public void WeaponConfiguration_AddsNoReplicatedPresentationState()
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo field in typeof(WeaponDefinition.PresentationConfig).GetFields(flags))
            {
                Assert.That(
                    field.GetCustomAttribute<Fusion.NetworkedAttribute>(),
                    Is.Null,
                    $"'{field.Name}' must remain static configuration, not replicated state.");
            }

            Assert.That(
                typeof(Fusion.NetworkBehaviour).IsAssignableFrom(typeof(PlayerWeaponPresenter)),
                Is.False,
                $"{nameof(PlayerWeaponPresenter)} must stay a presentation-only MonoBehaviour.");
        }

        private static IEnumerable<string> AllWeaponIds()
        {
            foreach (string lootId in MeleeWeaponIds)
            {
                yield return lootId;
            }

            foreach (string lootId in RangedWeaponIds)
            {
                yield return lootId;
            }

            yield return TrainingSword;
        }

        private WeaponDefinition ResolveWeapon(string lootId)
        {
            Assert.That(_catalog.TryGet(lootId, out LootDefinition definition), Is.True, lootId);
            Assert.That(definition.WeaponDefinition, Is.Not.Null, lootId);
            return definition.WeaponDefinition;
        }

        private void AssertRequirements(
            string lootId,
            int strength,
            int dexterity,
            int intelligence)
        {
            WeaponAttributeRequirements actual = ResolveWeapon(lootId).AttributeRequirements;
            Assert.That(
                actual,
                Is.EqualTo(new WeaponAttributeRequirements(strength, dexterity, intelligence)),
                lootId);
        }

        private void AssertNoScaling(string lootId)
        {
            WeaponOffensiveScaling scaling = ResolveWeapon(lootId).OffensiveScaling;
            Assert.That(scaling.TryValidate(out string error), Is.True, error);
            Assert.That(scaling.HasScaling, Is.False, lootId);
            Assert.That(scaling.Coefficient, Is.Zero, lootId);
        }

        private void AssertScaling(
            string lootId,
            CharacterAttribute attribute,
            float coefficient)
        {
            WeaponOffensiveScaling scaling = ResolveWeapon(lootId).OffensiveScaling;
            Assert.That(scaling.TryValidate(out string error), Is.True, error);
            Assert.That(scaling.HasScaling, Is.True, lootId);
            Assert.That(scaling.Attribute, Is.EqualTo(attribute), lootId);
            Assert.That(scaling.Coefficient, Is.EqualTo(coefficient).Within(0.0001f), lootId);
        }

        private List<LootDefinition> ReadSerializedDefinitions()
        {
            SerializedProperty definitions = new SerializedObject(_catalog).FindProperty("_definitions");
            var resolved = new List<LootDefinition>(definitions.arraySize);
            for (int index = 0; index < definitions.arraySize; index++)
            {
                resolved.Add(definitions.GetArrayElementAtIndex(index).objectReferenceValue as LootDefinition);
            }

            return resolved;
        }
    }
}
