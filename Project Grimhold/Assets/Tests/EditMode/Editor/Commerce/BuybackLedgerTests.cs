using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BuybackLedgerTests
{
    [Test]
    public void Replacement_IsWholesaleImmutableAndPublishesChangedIncludingEmptyHydration()
    {
        var ledger = new BuybackLedger();
        int changed = 0;
        ledger.Changed += () => changed++;
        var inventory = JsonUtility.FromJson<InventoryData>("{\"buyback\":{\"entries\":[{\"entryId\":\"sale\",\"lootId\":\"bone\",\"amount\":2,\"unitPrice\":9}]}}");
        ledger.Replace(inventory.buyback);
        Assert.That(ledger.Entries, Has.Count.EqualTo(1));
        Assert.That(ledger.Entries[0].EntryId, Is.EqualTo("sale"));
        Assert.That(ledger.Entries[0].LootId, Is.EqualTo(new LootId("bone")));
        Assert.That(ledger.Entries[0].Amount, Is.EqualTo(2));
        Assert.That(ledger.Entries[0].UnitPrice, Is.EqualTo(9));
        inventory.buyback.entries[0].amount = 99;
        Assert.That(ledger.Entries[0].Amount, Is.EqualTo(2));
        ledger.Replace(default);
        Assert.That(ledger.Entries, Is.Empty);
        Assert.That(changed, Is.EqualTo(2));
    }

    [Test]
    public void ContextHydration_ReplacesRowsAndReinitializationClearsAccountState()
    {
        var owner = new GameObject("Buyback hydration");
        try
        {
            var context = owner.AddComponent<ApplicationStashContext>();
            var inventory = new InventoryData { buyback = new BuybackData { entries = new[]
            {
                new BuybackEntryData { entryId = "first", lootId = "bone", amount = 1, unitPrice = 9 }
            } } };
            context.HydrateBuyback(inventory);
            Assert.That(context.BuybackLedger.Entries[0].EntryId, Is.EqualTo("first"));
            context.HydrateBuyback(default);
            Assert.That(context.BuybackLedger.Entries.Count, Is.Zero);
            context.HydrateBuyback(inventory);
            context.Initialize(null, null, null, null, null);
            Assert.That(context.BuybackLedger.Entries.Count, Is.Zero);
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [TestCase(LootCategory.None, false)]
    [TestCase(LootCategory.Valuable, false)]
    [TestCase(LootCategory.Material, false)]
    [TestCase(LootCategory.Quest, false)]
    [TestCase(LootCategory.Miscellaneous, false)]
    [TestCase(LootCategory.Weapon, true)]
    [TestCase(LootCategory.Helmet, true)]
    [TestCase(LootCategory.Armor, true)]
    [TestCase(LootCategory.Gloves, true)]
    [TestCase(LootCategory.Boots, true)]
    [TestCase(LootCategory.Shield, true)]
    public void Eligibility_IsPresentationOnlyEquipmentPolicy(LootCategory category, bool expected)
    {
        var definition = ScriptableObject.CreateInstance<LootDefinition>();
        try
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_category").intValue = (int)category;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(new EquipmentBuybackEligibilityPolicy().IsEligible(definition), Is.EqualTo(expected));
        }
        finally { Object.DestroyImmediate(definition); }
    }

    [Test]
    public void MissingDefinition_IsNotEligible() =>
        Assert.That(new EquipmentBuybackEligibilityPolicy().IsEligible(null), Is.False);
}
