using NUnit.Framework;
using Grimhold.Backend;

[TestFixture]
public class RaidResultCanonicalizerTests
{
    [Test]
    public void BuildCanonicalResultString_NormalizesAndSortsItemsAndSlotsAlphabetically()
    {
        var items = new[]
        {
            new InventoryItemData { lootId = "potion_heal", amount = 2 },
            new InventoryItemData { lootId = "iron_ore", amount = 5 }
        };

        var eq = new PreparedEquipmentData
        {
            helmet = "helm_iron",
            armor = "armor_leather",
            weaponSlot1 = "sword_basic"
        };

        string canonical = RaidResultCanonicalizer.BuildCanonicalResultString(
            "raid_01",
            "raid_01:char_01:1",
            "char_01",
            items,
            eq,
            50,
            "2026-10-19T12:00:00.000Z");

        string expected = "raid_01|raid_01:char_01:1|char_01|iron_ore:5,potion_heal:2|armor=armor_leather;boots=;gloves=;helmet=helm_iron;offHand1=;offHand2=;weaponSlot1=sword_basic;weaponSlot2=|50|2026-10-19T12:00:00.000Z";
        Assert.AreEqual(expected, canonical);
    }
}
