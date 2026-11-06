#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Grimhold.Backend;
using NUnit.Framework;
using UnityEngine;

public sealed class InventoryProtocolSlotTests
{
    [Test]
    public void NewCharacterInventory_AcceptsExactServerSnapshot()
    {
        const string json = "{\"inventorySchemaVersion\":1,\"currency\":0,\"stash\":[],\"loadout\":[],\"preparedEquipment\":{\"weaponSlot1\":\"\",\"weaponSlot2\":\"\",\"offHand1\":\"\",\"helmet\":\"\",\"armor\":\"\",\"gloves\":\"\",\"boots\":\"\",\"offHand2\":\"\"},\"pendingReservation\":null,\"buyback\":{\"entries\":[]},\"lastAppliedExtractionReceipt\":null,\"revision\":0}";
        // Identical serializer and DTO boundary to InventoryClient.ProcessResponse<InventoryData>.
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<InventoryData>(json);
        Assert.That(data.inventorySchemaVersion, Is.EqualTo(1));
        Assert.That(data.currency, Is.Zero);
        Assert.That(data.stash, Is.Empty);
        Assert.That(data.loadout, Is.Empty);
        foreach (var slot in EquipmentSlotRules.AllSlots)
            Assert.That(data.preparedEquipment.Get(slot), Is.Empty);
        Assert.That(data.pendingReservation.reservationId, Is.Null);
        Assert.That(data.lastAppliedExtractionReceipt.raidId, Is.Null);
        Assert.That(data.buyback.entries, Is.Empty);
        Assert.That(data.revision, Is.Zero);
    }

    [Test]
    public void EveryBackendRequestFactory_AddsProtocolThreeHeader_BeforeHydration()
    {
        using var get = InventoryProtocol.Get("http://localhost/inventory");
        using var post = InventoryProtocol.Create("http://localhost/inventory", "POST");
        using var delete = InventoryProtocol.Delete("http://localhost/inventory");
        foreach (var request in new[] { get, post, delete })
            Assert.That(request.GetRequestHeader("X-Grimhold-Inventory-Protocol"), Is.EqualTo("3"));
    }

    [Test]
    public void SlotMutationDtos_SerializeExactAddresses()
    {
        string json = InventoryRequestJson.Encode(new MoveItemRequest
        { sourceSlot = 7, lootId = "bone", amount = 2, expectedRevision = 3 });
        Assert.That(json, Does.Contain("\"sourceSlot\":7"));
        json = InventoryRequestJson.Encode(new ShopSellRequest
        { sourceSlot = 7, lootId = "bone", amount = 2, expectedRevision = 3 });
        Assert.That(json, Does.Contain("\"sourceSlot\":7"));
        json = JsonUtility.ToJson(new InventorySlotOrderRequest
        { list = "stash", slotOrder = new[] { 7, 1 }, expectedRevision = 3 });
        Assert.That(json, Does.Contain("\"slotOrder\":[7,1]"));
        Assert.That(json, Does.Not.Contain("lootIds"));
        json = InventoryRequestJson.Encode(new InventorySlotMoveRequest
        { fromList = "stash", fromSlot = 7, toList = "loadout", toSlot = 1, lootId = "bone", expectedRevision = 3 });
        Assert.That(json, Does.Contain("\"fromSlot\":7"));
        Assert.That(json, Does.Contain("\"toSlot\":1"));
    }

    [Test]
    public void ExtractionWirePayloads_StayAggregateWithoutSlotFields()
    {
        var items = new[] { new InventoryItemData { slot = 7, lootId = "bone", amount = 3 } };
        foreach (string json in new[]
        {
            InventoryRequestJson.Encode(new CommitExtractionRequest { items = items }),
            InventoryRequestJson.Encode(new CommitExtractionUnifiedRequest { items = items }),
            InventoryRequestJson.Encode(new PublishExtractionResultRequest { items = items })
        })
        {
            Assert.That(json, Does.Contain("\"items\":[{\"lootId\":\"bone\",\"amount\":3}]"));
            Assert.That(json, Does.Not.Contain("\"slot\""));
        }
    }
}
#endif
