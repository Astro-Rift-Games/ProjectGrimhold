#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class TownAbilityTextTests
    {
        private static TownAbilityEntry Entry(
            string id = "trap",
            string name = "Trap",
            AbilityResourceType resource = AbilityResourceType.Stamina,
            int cost = 15,
            float cooldown = 6f,
            TownAbilityEntryState state = TownAbilityEntryState.Available,
            params TownAbilityRequirementCheck[] requirements) =>
            new(
                new AbilityId(id),
                name,
                "description",
                null,
                resource,
                cost,
                cooldown,
                new List<TownAbilityRequirementCheck>(requirements),
                state,
                false,
                UniversalAbilitySlot.Slot1);

        [TestCase(CharacterAttribute.Vitality, "VIT")]
        [TestCase(CharacterAttribute.Resistance, "RES")]
        [TestCase(CharacterAttribute.Strength, "STR")]
        [TestCase(CharacterAttribute.Dexterity, "DEX")]
        [TestCase(CharacterAttribute.Intelligence, "INT")]
        [TestCase(CharacterAttribute.Luck, "LUCK")]
        public void AttributeCode_UsesTheShortCodes(CharacterAttribute attribute, string expected)
        {
            Assert.That(TownAbilityText.AttributeCode(attribute), Is.EqualTo(expected));
        }

        [Test]
        public void Requirement_SingleAttribute_ShowsCodeAndMinimum()
        {
            TownAbilityEntry entry = Entry(
                requirements: new TownAbilityRequirementCheck(CharacterAttribute.Dexterity, 10, 3));

            Assert.That(TownAbilityText.Requirement(entry), Is.EqualTo("DEX 10"));
        }

        [Test]
        public void Requirement_DualAttributes_AreJoinedWithPlus()
        {
            TownAbilityEntry entry = Entry(
                requirements: new[]
                {
                    new TownAbilityRequirementCheck(CharacterAttribute.Vitality, 7, 0),
                    new TownAbilityRequirementCheck(CharacterAttribute.Intelligence, 7, 0)
                });

            Assert.That(TownAbilityText.Requirement(entry), Is.EqualTo("VIT 7 + INT 7"));
        }

        [Test]
        public void Requirement_WithoutRequirements_ReadsNone()
        {
            Assert.That(TownAbilityText.Requirement(Entry()), Is.EqualTo("None"));
        }

        [Test]
        public void RequirementLabel_NamesTheAttributeAndTheAbility()
        {
            var check = new TownAbilityRequirementCheck(CharacterAttribute.Dexterity, 10, 3);

            Assert.That(TownAbilityText.RequirementLabel(check, "Trap"), Is.EqualTo("DEX (Trap)"));
        }

        [Test]
        public void CurrentOverRequired_ShowsBothValues()
        {
            var check = new TownAbilityRequirementCheck(CharacterAttribute.Dexterity, 10, 3);

            Assert.That(TownAbilityText.CurrentOverRequired(check), Is.EqualTo("3 / 10"));
        }

        [Test]
        public void CardSubtitle_ShowsResourceAndRequirement()
        {
            TownAbilityEntry entry = Entry(
                requirements: new TownAbilityRequirementCheck(CharacterAttribute.Dexterity, 10, 10));

            Assert.That(TownAbilityText.CardSubtitle(entry), Is.EqualTo("Stamina | DEX 10"));
        }

        [Test]
        public void ResourceCost_ShowsResourceAndCost()
        {
            Assert.That(
                TownAbilityText.ResourceCost(Entry(resource: AbilityResourceType.Mana, cost: 30)),
                Is.EqualTo("Mana | 30"));
        }

        [TestCase(6f, "6 seconds")]
        [TestCase(2.5f, "2.5 seconds")]
        [TestCase(1f, "1 second")]
        [TestCase(20.00f, "20 seconds")]
        public void Cooldown_UsesInvariantCultureAndDropsTrailingZeros(float seconds, string expected)
        {
            Assert.That(TownAbilityText.Cooldown(seconds), Is.EqualTo(expected));
        }

        [Test]
        public void SlotDetail_ShowsResourceAndShortCooldown()
        {
            Assert.That(
                TownAbilityText.SlotDetail(Entry(cooldown: 6f)),
                Is.EqualTo("Stamina | CD: 6s"));
        }

        [TestCase(TownAbilityEntryState.Available, "Unlocked")]
        [TestCase(TownAbilityEntryState.Equipped, "Equipped")]
        [TestCase(TownAbilityEntryState.RequirementsNotMet, "Missing Req")]
        public void StateBadge_MapsEveryState(TownAbilityEntryState state, string expected)
        {
            Assert.That(TownAbilityText.StateBadge(state), Is.EqualTo(expected));
        }

        [Test]
        public void EquipLabel_ReflectsWhetherTheAbilityAlreadyOccupiesTheSlot()
        {
            Assert.That(TownAbilityText.EquipLabel(UniversalAbilitySlot.Slot1, false), Is.EqualTo("Equip to Slot 1"));
            Assert.That(TownAbilityText.EquipLabel(UniversalAbilitySlot.Slot2, false), Is.EqualTo("Equip to Slot 2"));
            Assert.That(TownAbilityText.EquipLabel(UniversalAbilitySlot.Slot2, true), Is.EqualTo("Equipped in Slot 2"));
        }

        [Test, Category("TASK206")]
        public void Rejection_Success_IsEmpty()
        {
            Assert.That(
                TownAbilityText.Rejection(TownAbilityMutationResult.From(AbilityPreparationResult.Success)),
                Is.Empty);
        }

        [Test, Category("TASK206")]
        public void Rejection_BlockedByReadyState_ExplainsTheReadyLock()
        {
            Assert.That(
                TownAbilityText.Rejection(TownAbilityMutationResult.Blocked()),
                Is.EqualTo("Abilities can't be changed while Ready."));
        }

        [TestCase(AbilityPreparationResult.AttributeRequirementsNotMet, "Attribute requirements not met.")]
        [TestCase(AbilityPreparationResult.AbilityNotUnlocked, "This ability is not unlocked.")]
        [TestCase(AbilityPreparationResult.DuplicateAbility, "Already equipped in the other slot.")]
        [TestCase(AbilityPreparationResult.ProfileUnavailable, "Profile unavailable. Nothing was changed.")]
        [TestCase(AbilityPreparationResult.PersistenceFailed, "Couldn't save your abilities. Nothing was changed.")]
        [TestCase(AbilityPreparationResult.InvalidSlot, "This ability can't be equipped there.")]
        [TestCase(AbilityPreparationResult.InvalidAbility, "This ability can't be equipped there.")]
        [TestCase(AbilityPreparationResult.UnknownAbility, "This ability can't be equipped there.")]
        [TestCase(AbilityPreparationResult.InvalidPreparedState, "This ability can't be equipped there.")]
        [Category("TASK206")]
        public void Rejection_MapsEveryRejectedReason(AbilityPreparationResult reason, string expected)
        {
            Assert.That(TownAbilityText.Rejection(TownAbilityMutationResult.From(reason)), Is.EqualTo(expected));
        }

        [Test, Category("TASK206")]
        public void Rejection_EveryNonSuccessReasonHasAMessage()
        {
            foreach (AbilityPreparationResult reason in System.Enum.GetValues(typeof(AbilityPreparationResult)))
            {
                string message = TownAbilityText.Rejection(TownAbilityMutationResult.From(reason));
                Assert.That(
                    string.IsNullOrEmpty(message),
                    Is.EqualTo(reason == AbilityPreparationResult.Success),
                    $"{reason} must {(reason == AbilityPreparationResult.Success ? "not " : "")}produce a message.");
            }
        }
    }
}
#endif
