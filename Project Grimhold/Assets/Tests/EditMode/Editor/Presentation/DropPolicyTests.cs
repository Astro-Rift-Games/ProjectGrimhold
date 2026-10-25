#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class DropPolicyTests
{
    [TestCase(EquipmentSlot.WeaponSetAOffHand)]
    [TestCase(EquipmentSlot.WeaponSetBOffHand)]
    public void ReservedOffHand_RejectsOtherwiseCompatibleDrop(EquipmentSlot slot)
    {
        var payload = new DragPayload(new LootId("shield"), DragSlotLocation.Inventory,
            EquipmentSlot.None, null, 1, LootCategory.Shield, WeaponHandedness.OneHanded);
        Assert.That(DropPolicy.CanAttemptEquipmentDrop(in payload, slot, true), Is.False);
        Assert.That(DropPolicy.CanAttemptEquipmentDrop(in payload, slot, false), Is.True);
    }

    [Test]
    public void Reservation_DoesNotBlockMainHand_OrBypassCompatibility()
    {
        var payload = new DragPayload(new LootId("weapon"), DragSlotLocation.Inventory,
            EquipmentSlot.None, null, 1, LootCategory.Weapon, WeaponHandedness.TwoHanded);
        Assert.That(DropPolicy.CanAttemptEquipmentDrop(in payload, EquipmentSlot.WeaponSetAMainHand, true), Is.True);
        Assert.That(DropPolicy.CanAttemptEquipmentDrop(in payload, EquipmentSlot.WeaponSetAOffHand, false), Is.False);
    }
}
#endif
