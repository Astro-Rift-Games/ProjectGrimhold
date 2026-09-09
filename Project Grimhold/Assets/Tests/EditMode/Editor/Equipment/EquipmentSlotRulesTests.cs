using NUnit.Framework;

namespace Tests.EditMode.Equipment
{
    /// <summary>
    /// Equipment owns slot compatibility. Loot only classifies the unit, so the full category x slot
    /// matrix is asserted here rather than through any Loot type.
    /// </summary>
    public sealed class EquipmentSlotRulesTests
    {
        private static readonly EquipmentSlot[] EverySlot =
        {
            EquipmentSlot.WeaponSetAMainHand, EquipmentSlot.WeaponSetBMainHand, EquipmentSlot.Helmet,
            EquipmentSlot.Armor, EquipmentSlot.Gloves, EquipmentSlot.Boots,
            EquipmentSlot.WeaponSetAOffHand, EquipmentSlot.WeaponSetBOffHand
        };

        [TestCase(EquipmentSlot.WeaponSetAMainHand, true)]
        [TestCase(EquipmentSlot.WeaponSetBMainHand, true)]
        [TestCase(EquipmentSlot.WeaponSetAOffHand, true)]
        [TestCase(EquipmentSlot.WeaponSetBOffHand, true)]
        [TestCase(EquipmentSlot.Helmet, false)]
        [TestCase(EquipmentSlot.Armor, false)]
        [TestCase(EquipmentSlot.Gloves, false)]
        [TestCase(EquipmentSlot.Boots, false)]
        [TestCase(EquipmentSlot.None, false)]
        public void WeaponCategory_IsCompatibleOnlyWithHandSlots(EquipmentSlot slot, bool expected)
        {
            Assert.That(EquipmentSlotRules.IsCompatible(LootCategory.Weapon, slot), Is.EqualTo(expected));
        }

        [TestCase(LootCategory.Helmet, EquipmentSlot.Helmet)]
        [TestCase(LootCategory.Armor, EquipmentSlot.Armor)]
        [TestCase(LootCategory.Gloves, EquipmentSlot.Gloves)]
        [TestCase(LootCategory.Boots, EquipmentSlot.Boots)]
        public void ArmorCategory_IsCompatibleOnlyWithItsOwnSlot(
            LootCategory category,
            EquipmentSlot expectedSlot)
        {
            for (int index = 0; index < EverySlot.Length; index++)
            {
                EquipmentSlot slot = EverySlot[index];
                Assert.That(
                    EquipmentSlotRules.IsCompatible(category, slot),
                    Is.EqualTo(slot == expectedSlot),
                    $"{category} against {slot}");
            }

            Assert.That(EquipmentSlotRules.IsCompatible(category, EquipmentSlot.None), Is.False);
        }

        [TestCase(LootCategory.None)]
        [TestCase(LootCategory.Valuable)]
        [TestCase(LootCategory.Material)]
        [TestCase(LootCategory.Quest)]
        [TestCase(LootCategory.Miscellaneous)]
        public void NonEquippableCategory_IsRejectedByEightSlots(LootCategory category)
        {
            Assert.That(EquipmentSlotRules.IsEquippableCategory(category), Is.False);
            for (int index = 0; index < EverySlot.Length; index++)
            {
                Assert.That(
                    EquipmentSlotRules.IsCompatible(category, EverySlot[index]),
                    Is.False,
                    $"{category} against {EverySlot[index]}");
            }
        }

        [TestCase(LootCategory.Weapon)]
        [TestCase(LootCategory.Helmet)]
        [TestCase(LootCategory.Armor)]
        [TestCase(LootCategory.Gloves)]
        [TestCase(LootCategory.Boots)]
        public void EquippableCategories_AreExactlyTheFiveSupportedOnes(LootCategory category)
        {
            Assert.That(EquipmentSlotRules.IsEquippableCategory(category), Is.True);
        }

        [TestCase(LootCategory.Helmet, EquipmentSlot.Helmet)]
        [TestCase(LootCategory.Armor, EquipmentSlot.Armor)]
        [TestCase(LootCategory.Gloves, EquipmentSlot.Gloves)]
        [TestCase(LootCategory.Boots, EquipmentSlot.Boots)]
        [TestCase(LootCategory.Weapon, EquipmentSlot.None)]
        [TestCase(LootCategory.Valuable, EquipmentSlot.None)]
        public void ResolveFixedSlot_OnlyResolvesArmorDestinations(
            LootCategory category,
            EquipmentSlot expected)
        {
            Assert.That(EquipmentSlotRules.ResolveFixedSlot(category), Is.EqualTo(expected));
        }

        [Test]
        public void SlotClassification_SeparatesWeaponFromArmor()
        {
            Assert.That(EquipmentSlotRules.IsHandSlot(EquipmentSlot.WeaponSetAMainHand), Is.True);
            Assert.That(EquipmentSlotRules.IsHandSlot(EquipmentSlot.WeaponSetBMainHand), Is.True);
            Assert.That(EquipmentSlotRules.IsHandSlot(EquipmentSlot.WeaponSetAOffHand), Is.True);
            Assert.That(EquipmentSlotRules.IsHandSlot(EquipmentSlot.Helmet), Is.False);
            Assert.That(EquipmentSlotRules.IsArmorSlot(EquipmentSlot.WeaponSetAMainHand), Is.False);
            Assert.That(EquipmentSlotRules.IsArmorSlot(EquipmentSlot.Boots), Is.True);
            Assert.That(EquipmentSlotRules.IsEquipmentSlot(EquipmentSlot.None), Is.False);
        }

        [TestCase(WeaponSetSlot.SetA, EquipmentSlot.WeaponSetAMainHand)]
        [TestCase(WeaponSetSlot.SetB, EquipmentSlot.WeaponSetBMainHand)]
        [TestCase(WeaponSetSlot.None, EquipmentSlot.None)]
        public void WeaponSetBridge_RoundTripsWithoutLosingIdentity(
            WeaponSetSlot weaponSlot,
            EquipmentSlot equipmentSlot)
        {
            Assert.That(EquipmentSlotRules.GetMainHandSlot(weaponSlot), Is.EqualTo(equipmentSlot));
            Assert.That(EquipmentSlotRules.GetWeaponSet(equipmentSlot), Is.EqualTo(weaponSlot));
        }

        [TestCase(EquipmentSlot.Helmet)]
        [TestCase(EquipmentSlot.Armor)]
        [TestCase(EquipmentSlot.Gloves)]
        [TestCase(EquipmentSlot.Boots)]
        public void ArmorSlots_NeverMapOntoTheWeaponSetSelectionContract(EquipmentSlot slot)
        {
            Assert.That(EquipmentSlotRules.GetWeaponSet(slot), Is.EqualTo(WeaponSetSlot.None));
        }

        [TestCase(-1, false)]
        [TestCase(0, true)]
        [TestCase(6, true)]
        [TestCase(7, true)]
        [TestCase(8, true)]
        [TestCase(9, false)]
        public void SlotValueRange_CoversExactlyTheEightSlotsPlusNone(int value, bool expected)
        {
            Assert.That(EquipmentSlotRules.IsValidSlotValue(value), Is.EqualTo(expected));
        }

        [Test]
        public void ResultContract_PreservesTheNumericValuesTransportedByTheRpc()
        {
            // The reserved value 5 belonged to the removed WeaponAlreadyEquipped result and must
            // not be reused while an older peer could still send it.
            Assert.That((int)EquipmentOperationResult.None, Is.EqualTo(0));
            Assert.That((int)EquipmentOperationResult.Succeeded, Is.EqualTo(1));
            Assert.That((int)EquipmentOperationResult.InvalidRequest, Is.EqualTo(2));
            Assert.That((int)EquipmentOperationResult.PlayerUnavailable, Is.EqualTo(3));
            Assert.That((int)EquipmentOperationResult.InvalidEquipment, Is.EqualTo(4));
            Assert.That((int)EquipmentOperationResult.ItemNotOwned, Is.EqualTo(6));
            Assert.That((int)EquipmentOperationResult.DependenciesUnavailable, Is.EqualTo(7));
            Assert.That((int)EquipmentOperationResult.ReservedLegacyNoFreeWeaponSlot, Is.EqualTo(8));
            Assert.That((int)EquipmentOperationResult.EmptySlot, Is.EqualTo(9));
            Assert.That((int)EquipmentOperationResult.InventoryFull, Is.EqualTo(10));
            Assert.That((int)EquipmentOperationResult.SlotOccupied, Is.EqualTo(11));
            Assert.That((int)EquipmentOperationResult.AttributeRequirementsNotMet, Is.EqualTo(12));
            Assert.That((int)EquipmentOperationResult.IncompatibleHandConfiguration, Is.EqualTo(13));
        }
    }
}
