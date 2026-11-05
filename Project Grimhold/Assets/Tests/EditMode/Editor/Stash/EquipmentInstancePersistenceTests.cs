#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class EquipmentInstancePersistenceTests
{
    private static readonly ProfileId Profile = new("49494949494949494949494949494949");
    private LootDefinitionCatalog Catalog => AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
    private const string Id = "0123456789abcdef01234567";
    private const string Instance = "{\"instanceId\":\"" + Id + "\",\"modifiers\":{\"gemSocketCount\":2,\"gems\":[\"RES_RUBY_GEM\",null]}}";

    private static string Save(int version, string fields) =>
        "{\"schemaVersion\":" + version + ",\"inventorySchemaVersion\":2,\"profileId\":\"" + Profile.Value +
        "\",\"level\":1," + fields + "}";

    [Test]
    public void V11_RetainsInstancesInListsEquipmentAndReservation_WithoutOwnershipRepair()
    {
        string entry = "{\"lootId\":\"rapier\",\"instance\":" + Instance + "}";
        string item = "{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1,\"instance\":" + Instance + "}";
        // Distinct IDs across ownership locations; same template must not trigger legacy repair.
        string json = Save(11, "\"loadout\":[" + item + "],\"preparedEquipment\":{\"weaponSlot1\":" +
            entry.Replace(Id, "1123456789abcdef01234567") + "},\"pendingReservation\":{\"reservationId\":\"r\",\"items\":[" +
            item.Replace(Id, "2123456789abcdef01234567") + "],\"preparedEquipment\":{\"weaponSlot1\":" +
            entry.Replace(Id, "3123456789abcdef01234567") + "}}");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, Profile, Catalog, out var snapshot, out _, out var error), Is.True, error);
        Assert.That(snapshot.Loadout.Single().Amount, Is.EqualTo(1));
        string encoded = LocalProfileSaveCodec.Encode(snapshot);
        foreach (string id in new[] { Id, "1123456789abcdef01234567", "2123456789abcdef01234567", "3123456789abcdef01234567" })
            Assert.That(encoded, Does.Contain(id));
        Assert.That(encoded, Does.Contain("RES_RUBY_GEM"));
        Assert.That(LocalProfileSaveCodec.TryDecode(encoded, Profile, Catalog, out var again, out _, out error), Is.True, error);
        Assert.That(again.Loadout, Is.EqualTo(snapshot.Loadout));
        Assert.That(again.PreparedEquipment, Is.EqualTo(snapshot.PreparedEquipment));
        Assert.That(again.PendingReservation.Items, Is.EqualTo(snapshot.PendingReservation.Items));
        Assert.That(again.PendingReservation.PreparedEquipment, Is.EqualTo(snapshot.PendingReservation.PreparedEquipment));
    }

    [Test]
    public void V10_EquipmentStackSplitsLosslessly_AndCacheCannotEquipOrUnequip()
    {
        string json = Save(10, "\"loadout\":[{\"slot\":7,\"lootId\":\"rapier\",\"amount\":2}],\"preparedWeaponSetAMainHand\":\"rapier\"");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, Profile, Catalog, out var snapshot, out _, out var error), Is.True, error);
        Assert.That(snapshot.Loadout.Count, Is.EqualTo(2));
        Assert.That(snapshot.Loadout.All(x => x.Amount == 1), Is.True);
        Assert.That(snapshot.Loadout.Any(x => x.Slot == 7), Is.True);
        var repository = new InMemoryLocalProfileRepository();
        repository.Initialize(Profile, Catalog);
        repository.TrySave(snapshot, out _);
        var store = new LocalProfileStore(repository, Profile, Catalog);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        string before = LocalProfileSaveCodec.Encode(repository.Snapshot);
        Assert.That(store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetBMainHand, new LootId("rapier")), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.TryClearPreparedEquipment(EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(LocalProfileSaveCodec.Encode(repository.Snapshot), Is.EqualTo(before));
        Assert.That(commits, Is.Zero);
    }

    [TestCase("missing")]
    [TestCase("quantity")]
    [TestCase("resource")]
    [TestCase("duplicate")]
    [TestCase("prepared-missing")]
    public void V11_V3RejectsInvalidInstanceShapes(string invalid)
    {
        string item = "{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1,\"instance\":" + Instance + "}";
        if (invalid == "missing") item = "{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1}";
        if (invalid == "quantity") item = item.Replace("\"amount\":1", "\"amount\":2");
        if (invalid == "resource") item = item.Replace("rapier", "bone");
        if (invalid == "duplicate") item += "," + item.Replace("\"slot\":7", "\"slot\":8");
        string fields = "\"loadout\":[" + item + "]";
        if (invalid == "prepared-missing") fields += ",\"preparedWeaponSetAMainHand\":\"rapier\"";
        string json = Save(11, fields).Replace("\"inventorySchemaVersion\":2", "\"inventorySchemaVersion\":3");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, Profile, Catalog, out var snapshot, out _, out _), Is.False);
        Assert.That(snapshot, Is.Null);
    }

    [Test]
    public void EquipmentUnitsNeverMerge_AndStoreEquipUnequipRetainsIdentity()
    {
        var first = new EquipmentInstance(Id, default(WeaponInstanceModifiers));
        var second = new EquipmentInstance("1123456789abcdef01234567", default(WeaponInstanceModifiers));
        var sword = new LootId("rapier");
        var items = new System.Collections.Generic.List<StashItem> { new(sword, 1, 7, first) };
        Assert.That(ProfileInventoryRules.TryMerge(items, new[] { new StashItem(sword, 1, -1, second) }, 30), Is.True);
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[0].Instance, Is.EqualTo(second));
        Assert.That(items[1].Instance, Is.EqualTo(first));
        Assert.That(ProfileInventoryRules.TryMove(items, 7, items, 0, sword, 30, 30), Is.True);
        Assert.That(items[0].Instance, Is.EqualTo(first));
        Assert.That(items[1].Instance, Is.EqualTo(second));
        var repository = new InMemoryLocalProfileRepository(); repository.Initialize(Profile, Catalog);
        var snapshot = repository.Snapshot.Clone(); snapshot.InventorySchemaVersion = 3;
        snapshot.Loadout.Add(new StashItem(sword, 1, 7, first)); repository.TrySave(snapshot, out _);
        var store = new LocalProfileStore(repository, Profile, Catalog);
        Assert.That(store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, sword), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(first));
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.TryClearPreparedEquipment(EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout().Single().Instance, Is.EqualTo(first));
        Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
    }

    [Test]
    public void V10_ExpansionPreservesOtherAddressesAndOverflow_WithoutIssuingIds()
    {
        string json = Save(10, "\"loadout\":[{\"slot\":40,\"lootId\":\"rapier\",\"amount\":3},{\"slot\":0,\"lootId\":\"bone\",\"amount\":2}]");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, Profile, Catalog, out var snapshot, out _, out var error), Is.True, error);
        Assert.That(snapshot.Loadout.Select(x => x.Slot), Is.EqualTo(new[] { 0, 1, 2, 40 }));
        foreach (var item in snapshot.Loadout.Where(x => x.Instance != null))
        {
            Assert.That(item.Amount, Is.EqualTo(1));
            Assert.That(item.Instance.HasIdentity, Is.False);
            Assert.That(item.Instance.Modifiers.Value.HasPrimary, Is.False);
            Assert.That(item.Instance.Modifiers.Value.GemSocketCount, Is.Zero);
        }
        Assert.That(snapshot.RequiresEquipmentHydration, Is.True);
        Assert.That(LocalProfileSaveCodec.TryDecode(LocalProfileSaveCodec.Encode(snapshot), Profile, Catalog,
            out var again, out _, out error), Is.True, error);
        Assert.That(again.RequiresEquipmentHydration, Is.True, "cache-only saves must not unlock equipment");
    }

    [Test]
    public void BuybackResponseCacheRetainsExistingV3EquipmentInstances()
    {
        var repository = new InMemoryLocalProfileRepository(); repository.Initialize(Profile, Catalog);
        var snapshot = repository.Snapshot.Clone(); snapshot.InventorySchemaVersion = 3; repository.TrySave(snapshot, out _);
        var store = new LocalProfileStore(repository, Profile, Catalog);
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Grimhold.Backend.ShopTransactionResult>(
            "{\"revision\":5,\"currency\":12,\"loadout\":[{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1,\"instance\":" + Instance + "}]}");
        var transaction = new ShopTransactionId(10, System.Guid.NewGuid());
        Assert.That(store.ApplyBuybackAcknowledgement(Profile, transaction, data), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout().Single().Slot, Is.EqualTo(7));
        Assert.That(store.GetLoadout().Single().Instance?.InstanceId, Is.EqualTo(Id));
        Assert.That(store.RemoteRevision, Is.EqualTo(5));
        Assert.That(store.GetCurrency(), Is.EqualTo(12));
    }

    [Test]
    public void V2_BuybackAcknowledgementStillNeedsNoStoreCatalog()
    {
        var repository = new InMemoryLocalProfileRepository(); repository.Initialize(Profile, Catalog);
        var snapshot = repository.Snapshot.Clone(); snapshot.InventorySchemaVersion = 2; repository.TrySave(snapshot, out _);
        var store = new LocalProfileStore(repository, Profile);
        var response = new Grimhold.Backend.ShopTransactionResult { revision = 5, currency = 12,
            loadout = new[] { new Grimhold.Backend.InventoryItemData { slot = 7, lootId = "bone", amount = 2 } } };
        int commits = 0; store.ProfileCommitted += _ => commits++;
        var transaction = new ShopTransactionId(10, System.Guid.NewGuid());
        Assert.That(store.ApplyBuybackAcknowledgement(Profile, transaction, response), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.GetLoadout().Single(), Is.EqualTo(new StashItem(new LootId("bone"), 2, 7)));
        Assert.That(store.GetCurrency(), Is.EqualTo(12));
        Assert.That(store.RemoteRevision, Is.EqualTo(5));
        Assert.That(commits, Is.EqualTo(1));
    }

    [Test]
    public void V11_PendingExtractionKeepsIdFreePerUnitModifiersAcrossRetryCache()
    {
        string instance = Instance.Replace("\"instanceId\":\"" + Id + "\",", "");
        string item = "{\"slot\":0,\"lootId\":\"rapier\",\"amount\":1,\"instance\":" + instance + "}";
        string equipment = "{\"weaponSlot1\":{\"lootId\":\"rapier\",\"instance\":" + instance + "}}";
        string json = Save(11, "\"loadout\":[" + item + "],\"preparedEquipment\":" + equipment +
            ",\"pendingExtractionCommit\":{\"raidId\":\"raid\",\"profileId\":\"" + Profile.Value +
            "\",\"resultSequence\":1,\"resultingLevel\":1,\"items\":[" + item + "],\"preparedEquipment\":" + equipment + "}")
            .Replace("\"inventorySchemaVersion\":2", "\"inventorySchemaVersion\":3");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, Profile, Catalog, out var snapshot, out _, out var error), Is.True, error);
        Assert.That(snapshot.PendingExtractionCommit.Items.Single().Instance.HasIdentity, Is.False);
        Assert.That(snapshot.PendingExtractionCommit.Items.Single().Instance.Modifiers.Value.GemSocketCount, Is.EqualTo(2));
        Assert.That(snapshot.PendingExtractionCommit.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand).HasIdentity, Is.False);
        Assert.That(LocalProfileSaveCodec.TryDecode(LocalProfileSaveCodec.Encode(snapshot), Profile, Catalog, out var again, out _, out error), Is.True, error);
        Assert.That(again.PendingExtractionCommit.Items, Is.EqualTo(snapshot.PendingExtractionCommit.Items));
        Assert.That(again.PendingExtractionCommit.PreparedEquipment, Is.EqualTo(snapshot.PendingExtractionCommit.PreparedEquipment));
    }

    [TestCase(LootCategory.Weapon)]
    [TestCase(LootCategory.Shield)]
    [TestCase(LootCategory.Helmet)]
    [TestCase(LootCategory.Armor)]
    [TestCase(LootCategory.Gloves)]
    [TestCase(LootCategory.Boots)]
    public void EveryEquipmentCategoryDecodesAsIndividualPlainCacheUnits(LootCategory category)
    {
        LootDefinition definition = null;
        for (int i = 0; i < Catalog.DefinitionCount; i++)
            if (Catalog.TryGetByIndex(i, out var candidate) && candidate.Category == category) { definition = candidate; break; }
        Assert.That(definition, Is.Not.Null);
        string json = Save(10, "\"stash\":[{\"slot\":7,\"lootId\":\"" + definition.Id + "\",\"amount\":2}]");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, Profile, Catalog, out var snapshot, out _, out var error), Is.True, error);
        Assert.That(snapshot.Stash.Count, Is.EqualTo(2));
        foreach (var item in snapshot.Stash)
        {
            Assert.That(item.LootId, Is.EqualTo(definition.LootId));
            Assert.That(item.Amount, Is.EqualTo(1));
            Assert.That(item.Instance, Is.Not.Null);
            Assert.That(item.Instance.HasIdentity, Is.False);
            Assert.That(item.Instance.Modifiers.HasValue, Is.EqualTo(category == LootCategory.Weapon));
        }
    }

    [Test]
    public void FutureCacheIsUnsupportedEvenWhenItsNewInstanceFormatIsUnknown()
    {
        string instance = Instance.Replace("\"gemSocketCount\":2", "\"primary\":{\"attribute\":\"future-attribute\",\"grade\":\"future-grade\"},\"gemSocketCount\":2");
        string json = Save(LocalProfileSnapshot.CurrentSchemaVersion + 1,
            "\"loadout\":[{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1,\"instance\":" + instance + "}]");
        Assert.That(LocalProfileSaveCodec.TryDecode(json, Profile, Catalog, out var snapshot, out var status, out var error), Is.False);
        Assert.That(status, Is.EqualTo(LocalProfilePersistenceStatus.UnsupportedVersion));
        Assert.That(error, Is.EqualTo("Unsupported profile schema version " + (LocalProfileSnapshot.CurrentSchemaVersion + 1) + "."));
        Assert.That(snapshot, Is.Null);
    }

    [Test]
    public void V10_NonEquipmentPreservesSlotsAmountsAndBackendVersion()
    {
        Assert.That(LocalProfileSaveCodec.TryDecode(Save(10, "\"stash\":[{\"slot\":9,\"lootId\":\"bone\",\"amount\":3}]"),
            Profile, Catalog, out var snapshot, out _, out var error), Is.True, error);
        Assert.That(snapshot.Stash.Single(), Is.EqualTo(new StashItem(new LootId("bone"), 3, 9)));
        Assert.That(snapshot.InventorySchemaVersion, Is.EqualTo(2));
    }
}
#endif
