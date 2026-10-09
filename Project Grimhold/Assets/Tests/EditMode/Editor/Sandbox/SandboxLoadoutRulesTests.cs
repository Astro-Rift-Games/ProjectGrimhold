using NUnit.Framework;

namespace Tests.EditMode.Sandbox
{
    public sealed class SandboxLoadoutRulesTests
    {
        private static readonly AbilityId First = new AbilityId("sandbox_first");
        private static readonly AbilityId Second = new AbilityId("sandbox_second");

        [Test]
        public void AbilityPair_AllowsTwoDistinctEquippableAbilities()
        {
            bool ok = SandboxLoadoutRules.TryValidateAbilityPair(First, Second, _ => true, out string error);

            Assert.That(ok, Is.True, error);
        }

        [Test]
        public void AbilityPair_AllowsEmptySlots()
        {
            Assert.That(SandboxLoadoutRules.TryValidateAbilityPair(default, default, _ => false, out _), Is.True);
            Assert.That(SandboxLoadoutRules.TryValidateAbilityPair(First, default, _ => true, out _), Is.True);
            Assert.That(SandboxLoadoutRules.TryValidateAbilityPair(default, Second, _ => true, out _), Is.True);
        }

        [Test]
        public void AbilityPair_RejectsSameAbilityInBothSlots()
        {
            bool ok = SandboxLoadoutRules.TryValidateAbilityPair(First, First, _ => true, out string error);

            Assert.That(ok, Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void AbilityPair_RejectsAbilityWithoutBehaviour(bool firstEquippable, bool secondEquippable)
        {
            bool ok = SandboxLoadoutRules.TryValidateAbilityPair(
                First,
                Second,
                id => id == First ? firstEquippable : secondEquippable,
                out string error);

            Assert.That(ok, Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void AbilityPair_RejectsMissingPredicate()
        {
            Assert.That(SandboxLoadoutRules.TryValidateAbilityPair(First, Second, null, out _), Is.False);
        }

        [TestCase(LootCategory.Helmet, WeaponSetSlot.None, false, EquipmentSlot.Helmet)]
        [TestCase(LootCategory.Armor, WeaponSetSlot.SetA, true, EquipmentSlot.Armor)]
        [TestCase(LootCategory.Gloves, WeaponSetSlot.None, false, EquipmentSlot.Gloves)]
        [TestCase(LootCategory.Boots, WeaponSetSlot.None, false, EquipmentSlot.Boots)]
        [TestCase(LootCategory.Weapon, WeaponSetSlot.SetA, false, EquipmentSlot.WeaponSetAMainHand)]
        [TestCase(LootCategory.Weapon, WeaponSetSlot.SetB, false, EquipmentSlot.WeaponSetBMainHand)]
        [TestCase(LootCategory.Weapon, WeaponSetSlot.SetA, true, EquipmentSlot.WeaponSetAOffHand)]
        [TestCase(LootCategory.Weapon, WeaponSetSlot.SetB, true, EquipmentSlot.WeaponSetBOffHand)]
        [TestCase(LootCategory.Shield, WeaponSetSlot.SetA, false, EquipmentSlot.WeaponSetAOffHand)]
        [TestCase(LootCategory.Shield, WeaponSetSlot.SetB, true, EquipmentSlot.WeaponSetBOffHand)]
        public void EquipmentSlot_ResolvesCompatibleSlot(
            LootCategory category, WeaponSetSlot set, bool offHand, EquipmentSlot expected)
        {
            bool ok = SandboxLoadoutRules.TryResolveEquipmentSlot(category, set, offHand, out EquipmentSlot slot);

            Assert.That(ok, Is.True);
            Assert.That(slot, Is.EqualTo(expected));
            Assert.That(EquipmentSlotRules.IsCompatible(category, slot), Is.True);
        }

        [TestCase(LootCategory.Weapon)]
        [TestCase(LootCategory.Shield)]
        public void EquipmentSlot_HandItemsRequireAWeaponSet(LootCategory category)
        {
            Assert.That(
                SandboxLoadoutRules.TryResolveEquipmentSlot(category, WeaponSetSlot.None, false, out EquipmentSlot slot),
                Is.False);
            Assert.That(slot, Is.EqualTo(EquipmentSlot.None));
        }

        [TestCase(LootCategory.None)]
        [TestCase(LootCategory.Valuable)]
        [TestCase(LootCategory.Material)]
        [TestCase(LootCategory.Quest)]
        [TestCase(LootCategory.Miscellaneous)]
        public void EquipmentSlot_RejectsNonEquippableCategories(LootCategory category)
        {
            Assert.That(
                SandboxLoadoutRules.TryResolveEquipmentSlot(category, WeaponSetSlot.SetA, false, out EquipmentSlot slot),
                Is.False);
            Assert.That(slot, Is.EqualTo(EquipmentSlot.None));
        }

        [Test]
        public void WeaponSetPair_RejectsTwoHandedMainWithOffHand()
        {
            Assert.That(SandboxLoadoutRules.TryValidateWeaponSetPair(true, true, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void WeaponSetPair_AllowsEveryOtherCombination(bool twoHandedMain, bool offHandPresent)
        {
            Assert.That(SandboxLoadoutRules.TryValidateWeaponSetPair(twoHandedMain, offHandPresent, out _), Is.True);
        }

        [TestCase(50f, 100f, 0f, 50f)]
        [TestCase(500f, 100f, 0f, 100f)]
        [TestCase(-5f, 100f, 0f, 0f)]
        [TestCase(0f, 100f, 1f, 1f)]
        [TestCase(10f, 0f, 0f, 0f)]
        public void ClampValue_KeepsValueInsideRange(float requested, float maximum, float minimum, float expected)
        {
            Assert.That(SandboxLoadoutRules.TryClampValue(requested, minimum, maximum, out float clamped), Is.True);
            Assert.That(clamped, Is.EqualTo(expected));
        }

        [TestCase(float.NaN, 100f, 0f)]
        [TestCase(float.PositiveInfinity, 100f, 0f)]
        [TestCase(10f, float.NaN, 0f)]
        [TestCase(10f, -1f, 0f)]
        [TestCase(10f, 5f, 8f)]
        public void ClampValue_RejectsNonFiniteOrInvertedRange(float requested, float maximum, float minimum)
        {
            Assert.That(SandboxLoadoutRules.TryClampValue(requested, minimum, maximum, out float clamped), Is.False);
            Assert.That(clamped, Is.EqualTo(0f));
        }
    }
}
