#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class TownAbilitiesBuilderTests
    {
        private static readonly AbilityId Charge = new("charge");
        private static readonly AbilityId Trap = new("trap");
        private static readonly AbilityId Arcane = new("arcane_projectile");
        private static readonly AbilityId LifeDrain = new("life_drain");

        private readonly List<Object> _created = new();
        private AbilityDefinition[] _definitions;

        [SetUp]
        public void SetUp()
        {
            _definitions = new[]
            {
                Define("charge", "Charge", "Dash forward.", AbilityResourceType.Stamina, 20, 8f,
                    new CharacterAttributeRequirement(CharacterAttribute.Strength, 10)),
                Define("trap", "Trap", "Place a trap.", AbilityResourceType.Stamina, 15, 6f,
                    new CharacterAttributeRequirement(CharacterAttribute.Dexterity, 10)),
                Define("arcane_projectile", "Arcane Projectile", "Fire a bolt.", AbilityResourceType.Mana, 15, 5f,
                    new CharacterAttributeRequirement(CharacterAttribute.Intelligence, 10)),
                Define("life_drain", "Life Drain", "Drain life.", AbilityResourceType.Mana, 35, 20f,
                    new CharacterAttributeRequirement(CharacterAttribute.Vitality, 7),
                    new CharacterAttributeRequirement(CharacterAttribute.Intelligence, 7))
            };
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [Test]
        public void TryBuild_NullDefinitions_ReturnsFalse()
        {
            bool built = TownAbilitiesBuilder.TryBuild(
                null, new[] { Charge }, default, AbilityTestFactory.CreateAttributes(), out TownAbilitiesPresentation presentation);

            Assert.That(built, Is.False);
            Assert.That(presentation.Entries, Is.Empty);
        }

        [Test]
        public void TryBuild_EmptyRepertoire_YieldsNoEntriesAndEmptySlots()
        {
            TownAbilitiesPresentation presentation = Build(new AbilityId[0], default, AbilityTestFactory.CreateAttributes());

            Assert.That(presentation.Entries, Is.Empty);
            Assert.That(presentation.Slot1.HasValue, Is.False);
            Assert.That(presentation.Slot2.HasValue, Is.False);
            Assert.That(presentation.Filtered(TownAbilitiesFilter.All), Is.Empty);
        }

        [Test]
        public void TryBuild_NullUnlockedRepertoire_IsTreatedAsEmpty()
        {
            TownAbilitiesPresentation presentation = Build(null, default, AbilityTestFactory.CreateAttributes());

            Assert.That(presentation.Entries, Is.Empty);
        }

        [Test]
        public void TryBuild_ListsOnlyUnlockedAbilitiesInCatalogOrder()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { LifeDrain, Charge, Arcane }, default, AbilityTestFactory.CreateAttributes());

            Assert.That(presentation.Entries, Has.Count.EqualTo(3));
            Assert.That(presentation.Entries[0].Id, Is.EqualTo(Charge));
            Assert.That(presentation.Entries[1].Id, Is.EqualTo(Arcane));
            Assert.That(presentation.Entries[2].Id, Is.EqualTo(LifeDrain));
        }

        [Test]
        public void TryBuild_UnlockedIdMissingFromCatalog_IsIgnored()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { new AbilityId("not_in_catalog"), Trap }, default, AbilityTestFactory.CreateAttributes());

            Assert.That(presentation.Entries, Has.Count.EqualTo(1));
            Assert.That(presentation.Entries[0].Id, Is.EqualTo(Trap));
        }

        [Test]
        public void TryBuild_EntryExposesDisplayAndCostData()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { Arcane }, default, AbilityTestFactory.CreateAttributes(intelligence: 10));

            TownAbilityEntry entry = presentation.Entries[0];
            Assert.That(entry.DisplayName, Is.EqualTo("Arcane Projectile"));
            Assert.That(entry.Description, Is.EqualTo("Fire a bolt."));
            Assert.That(entry.Icon, Is.Null);
            Assert.That(entry.Resource, Is.EqualTo(AbilityResourceType.Mana));
            Assert.That(entry.Cost, Is.EqualTo(15));
            Assert.That(entry.CooldownSeconds, Is.EqualTo(5f));
        }

        [Test]
        public void TryBuild_UnlockedWithRequirementsMetAndNotEquipped_IsAvailable()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { Charge }, default, AbilityTestFactory.CreateAttributes(strength: 10));

            TownAbilityEntry entry = presentation.Entries[0];
            Assert.That(entry.State, Is.EqualTo(TownAbilityEntryState.Available));
            Assert.That(entry.HasEquippedSlot, Is.False);
        }

        [Test]
        public void TryBuild_UnlockedWithRequirementsNotMet_IsRequirementsNotMetIndependentOfUnlock()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { Charge }, default, AbilityTestFactory.CreateAttributes(strength: 9));

            Assert.That(presentation.Entries, Has.Count.EqualTo(1));
            Assert.That(presentation.Entries[0].State, Is.EqualTo(TownAbilityEntryState.RequirementsNotMet));
        }

        [Test]
        public void TryBuild_PreparedAbility_IsEquippedAndFillsItsSlot()
        {
            var prepared = new PreparedAbilityLoadout(default, Charge);

            TownAbilitiesPresentation presentation = Build(
                new[] { Charge, Trap }, prepared, AbilityTestFactory.CreateAttributes(strength: 10, dexterity: 10));

            TownAbilityEntry charge = presentation.Entries[0];
            Assert.That(charge.State, Is.EqualTo(TownAbilityEntryState.Equipped));
            Assert.That(charge.HasEquippedSlot, Is.True);
            Assert.That(charge.EquippedSlot, Is.EqualTo(UniversalAbilitySlot.Slot2));
            Assert.That(presentation.Entries[1].State, Is.EqualTo(TownAbilityEntryState.Available));
            Assert.That(presentation.Slot1.HasValue, Is.False);
            Assert.That(presentation.Slot2.HasValue, Is.True);
            Assert.That(presentation.Slot2.Value.Id, Is.EqualTo(Charge));
        }

        [Test]
        public void TryBuild_BothSlotsFilled_ExposesBothEntries()
        {
            var prepared = new PreparedAbilityLoadout(Trap, Arcane);

            TownAbilitiesPresentation presentation = Build(
                new[] { Charge, Trap, Arcane },
                prepared,
                AbilityTestFactory.CreateAttributes(strength: 10, dexterity: 10, intelligence: 10));

            Assert.That(presentation.Slot1.Value.Id, Is.EqualTo(Trap));
            Assert.That(presentation.Slot1.Value.EquippedSlot, Is.EqualTo(UniversalAbilitySlot.Slot1));
            Assert.That(presentation.Slot2.Value.Id, Is.EqualTo(Arcane));
            Assert.That(presentation.Slot2.Value.EquippedSlot, Is.EqualTo(UniversalAbilitySlot.Slot2));
        }

        [Test]
        public void TryBuild_PreparedIdNotInCatalog_IsIgnored()
        {
            var prepared = new PreparedAbilityLoadout(new AbilityId("not_in_catalog"), default);

            TownAbilitiesPresentation presentation = Build(
                new[] { Charge }, prepared, AbilityTestFactory.CreateAttributes(strength: 10));

            Assert.That(presentation.Slot1.HasValue, Is.False);
            Assert.That(presentation.Entries[0].State, Is.EqualTo(TownAbilityEntryState.Available));
        }

        [Test]
        public void TryBuild_PreparedIdNotUnlocked_IsIgnored()
        {
            var prepared = new PreparedAbilityLoadout(Trap, default);

            TownAbilitiesPresentation presentation = Build(
                new[] { Charge }, prepared, AbilityTestFactory.CreateAttributes(strength: 10, dexterity: 10));

            Assert.That(presentation.Slot1.HasValue, Is.False);
            Assert.That(presentation.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryBuild_SingleRequirement_ReportsCurrentVersusRequired()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { Charge }, default, AbilityTestFactory.CreateAttributes(strength: 6));

            IReadOnlyList<TownAbilityRequirementCheck> checks = presentation.Entries[0].Requirements;
            Assert.That(checks, Has.Count.EqualTo(1));
            Assert.That(checks[0].Attribute, Is.EqualTo(CharacterAttribute.Strength));
            Assert.That(checks[0].Required, Is.EqualTo(10));
            Assert.That(checks[0].Current, Is.EqualTo(6));
            Assert.That(checks[0].IsMet, Is.False);
        }

        [Test]
        public void TryBuild_RequirementAtExactMinimum_IsMet()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { Charge }, default, AbilityTestFactory.CreateAttributes(strength: 10));

            Assert.That(presentation.Entries[0].Requirements[0].IsMet, Is.True);
        }

        [Test]
        public void TryBuild_DualRequirement_ChecksEachAttributeIndependently()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { LifeDrain }, default, AbilityTestFactory.CreateAttributes(vitality: 7, intelligence: 3));

            TownAbilityEntry entry = presentation.Entries[0];
            Assert.That(entry.Requirements, Has.Count.EqualTo(2));
            Assert.That(entry.Requirements[0].Attribute, Is.EqualTo(CharacterAttribute.Vitality));
            Assert.That(entry.Requirements[0].Current, Is.EqualTo(7));
            Assert.That(entry.Requirements[0].IsMet, Is.True);
            Assert.That(entry.Requirements[1].Attribute, Is.EqualTo(CharacterAttribute.Intelligence));
            Assert.That(entry.Requirements[1].Current, Is.EqualTo(3));
            Assert.That(entry.Requirements[1].IsMet, Is.False);
            Assert.That(entry.State, Is.EqualTo(TownAbilityEntryState.RequirementsNotMet));
        }

        [Test]
        public void TryBuild_DualRequirementBothMet_IsAvailable()
        {
            TownAbilitiesPresentation presentation = Build(
                new[] { LifeDrain }, default, AbilityTestFactory.CreateAttributes(vitality: 7, intelligence: 8));

            Assert.That(presentation.Entries[0].State, Is.EqualTo(TownAbilityEntryState.Available));
        }

        [Test]
        public void Filtered_All_ReturnsEveryEntry()
        {
            TownAbilitiesPresentation presentation = BuildAllUnlocked();

            Assert.That(presentation.Filtered(TownAbilitiesFilter.All), Has.Count.EqualTo(4));
        }

        [Test]
        public void Filtered_Stamina_ReturnsOnlyStaminaEntriesInOrder()
        {
            TownAbilitiesPresentation presentation = BuildAllUnlocked();

            IReadOnlyList<TownAbilityEntry> filtered = presentation.Filtered(TownAbilitiesFilter.Stamina);

            Assert.That(filtered, Has.Count.EqualTo(2));
            Assert.That(filtered[0].Id, Is.EqualTo(Charge));
            Assert.That(filtered[1].Id, Is.EqualTo(Trap));
        }

        [Test]
        public void Filtered_Mana_ReturnsOnlyManaEntriesInOrder()
        {
            TownAbilitiesPresentation presentation = BuildAllUnlocked();

            IReadOnlyList<TownAbilityEntry> filtered = presentation.Filtered(TownAbilitiesFilter.Mana);

            Assert.That(filtered, Has.Count.EqualTo(2));
            Assert.That(filtered[0].Id, Is.EqualTo(Arcane));
            Assert.That(filtered[1].Id, Is.EqualTo(LifeDrain));
        }

        [Test]
        public void TryBuild_DefinitionWithoutAuthoredName_FallsBackToId()
        {
            AbilityDefinition unnamed = AbilityTestFactory.CreateDefinition("trap");
            _created.Add(unnamed);

            bool built = TownAbilitiesBuilder.TryBuild(
                new[] { unnamed },
                new[] { Trap },
                default,
                AbilityTestFactory.CreateAttributes(),
                out TownAbilitiesPresentation presentation);

            Assert.That(built, Is.True);
            Assert.That(presentation.Entries[0].DisplayName, Is.EqualTo("trap"));
        }

        private TownAbilitiesPresentation BuildAllUnlocked() =>
            Build(
                new[] { Charge, Trap, Arcane, LifeDrain },
                default,
                AbilityTestFactory.CreateAttributes(strength: 10, dexterity: 10, vitality: 7, intelligence: 10));

        private TownAbilitiesPresentation Build(
            IReadOnlyCollection<AbilityId> unlocked,
            PreparedAbilityLoadout prepared,
            CharacterAttributeState attributes)
        {
            bool built = TownAbilitiesBuilder.TryBuild(
                _definitions, unlocked, prepared, attributes, out TownAbilitiesPresentation presentation);
            Assert.That(built, Is.True);
            return presentation;
        }

        private AbilityDefinition Define(
            string id,
            string displayName,
            string description,
            AbilityResourceType resource,
            int cost,
            float cooldown,
            params CharacterAttributeRequirement[] requirements)
        {
            AbilityDefinition definition = AbilityTestFactory.CreateDefinition(id, resource, cost, cooldown, requirements);
            AbilityTestFactory.SetPrivateField(definition, "_displayName", displayName);
            AbilityTestFactory.SetPrivateField(definition, "_description", description);
            _created.Add(definition);
            return definition;
        }
    }
}
#endif
