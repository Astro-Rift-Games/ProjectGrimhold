#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using Grimhold.Backend;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class EquipmentInstanceBackendTests
{
    private const string Id = "0123456789abcdef01234567";
    private const string Block = "{\"instanceId\":\"" + Id + "\",\"modifiers\":{\"primary\":{\"attribute\":\"strength\",\"grade\":\"C\"},\"gemSocketCount\":1,\"gems\":[\"RES_RUBY_GEM\"]}}";
    private LootDefinitionCatalog Catalog => AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

    [Test]
    public void V3_ResponseHydratesListsPreparedEquipmentAndReservation_WithIdentityAndModifiers()
    {
        string json = "{\"inventorySchemaVersion\":3,\"revision\":9,\"loadout\":[{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1,\"instance\":" + Block +
            "}],\"preparedEquipment\":{\"weaponSlot1\":{\"lootId\":\"rapier\",\"instance\":" + Block.Replace(Id,"1123456789abcdef01234567") +
            "}},\"pendingReservation\":{\"reservationId\":\"r\",\"items\":[{\"slot\":40,\"lootId\":\"rapier\",\"amount\":1,\"instance\":" + Block.Replace(Id,"2123456789abcdef01234567") + "}]}}";
        var data = JsonConvert.DeserializeObject<InventoryData>(json);
        var profile = new ProfileId("49494949494949494949494949494949");
        var snapshot = new LocalProfileSnapshot { ProfileId = profile, RequiresEquipmentHydration = true };
        Assert.That(ApplicationStashServiceBootstrapper.HydrateSnapshot(profile, snapshot, data, null, Catalog, null), Is.True);
        Assert.That(snapshot.Loadout[0].Slot, Is.EqualTo(7));
        Assert.That(snapshot.Loadout[0].Instance.InstanceId, Is.EqualTo(Id));
        var modifiers = snapshot.Loadout[0].Instance.Modifiers.Value;
        Assert.That(modifiers.Primary.Attribute, Is.EqualTo(CharacterAttribute.Strength));
        Assert.That(modifiers.Primary.Grade, Is.EqualTo(WeaponScalingGrade.C));
        Assert.That(modifiers.TryGetGemSocket(0, out var socket), Is.True);
        Assert.That(socket.GemLootId.Value, Is.EqualTo("RES_RUBY_GEM"));
        Assert.That(snapshot.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId, Is.EqualTo("1123456789abcdef01234567"));
        Assert.That(snapshot.PendingReservation.Items[0].Slot, Is.EqualTo(40));
        Assert.That(snapshot.PendingReservation.Items[0].Instance.InstanceId, Is.EqualTo("2123456789abcdef01234567"));
        Assert.That(snapshot.RequiresEquipmentHydration, Is.False);
        Assert.That(snapshot.InventorySchemaVersion, Is.EqualTo(3));
        Assert.That(snapshot.RemoteRevision, Is.EqualTo(9));
    }

    [TestCase("Dexterity", "C")]
    [TestCase("2", "C")]
    [TestCase("not-an-attribute", "C")]
    [TestCase("dexterity", "None")]
    [TestCase("dexterity", "7")]
    [TestCase("dexterity", "c")]
    public void InvalidWireScalingNamesAreRejectedAtParse(string attribute, string grade)
    {
        string json = "{\"instanceId\":\"" + Id + "\",\"modifiers\":{\"primary\":{\"attribute\":\"" + attribute + "\",\"grade\":\"" + grade + "\"},\"gemSocketCount\":0,\"gems\":[]}}";
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<EquipmentInstanceData>(json));
    }

    [TestCase("vitality")]
    [TestCase("resistance")]
    [TestCase("strength")]
    [TestCase("dexterity")]
    [TestCase("intelligence")]
    [TestCase("luck")]
    public void ValidWireAttributesAndAllGradesMapBothWays(string attribute)
    {
        foreach (string grade in new[] { "E", "D", "C", "B", "A", "S" })
        {
            var data = JsonConvert.DeserializeObject<WeaponScalingModifierData>("{\"attribute\":\"" + attribute + "\",\"grade\":\"" + grade + "\"}");
            Assert.That(data.TryToModifier(out var modifier, out var error), Is.True, error);
            var encoded = WeaponScalingModifierData.FromModifier(modifier);
            Assert.That(encoded.attribute, Is.EqualTo(attribute));
            Assert.That(encoded.grade, Is.EqualTo(grade));
        }
    }

    [TestCase("missing")]
    [TestCase("quantity")]
    [TestCase("resource")]
    [TestCase("duplicate")]
    [TestCase("wrong-natural")]
    [TestCase("not-a-gem")]
    public void V3_InvalidHydrationLeavesEntireSnapshotUnchanged(string invalid)
    {
        string item = "{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1,\"instance\":" + Block + "}";
        if (invalid == "missing") item = "{\"slot\":7,\"lootId\":\"rapier\",\"amount\":1}";
        if (invalid == "quantity") item = item.Replace("\"amount\":1", "\"amount\":2");
        if (invalid == "resource") item = item.Replace("rapier", "bone");
        if (invalid == "duplicate") item += "," + item.Replace("\"slot\":7", "\"slot\":8");
        if (invalid == "wrong-natural") item = item.Replace("strength", "dexterity");
        if (invalid == "not-a-gem") item = item.Replace("RES_RUBY_GEM", "bone");
        var data = JsonConvert.DeserializeObject<InventoryData>("{\"inventorySchemaVersion\":3,\"revision\":99,\"currency\":99,\"loadout\":[" + item + "]}");
        var profile = new ProfileId("49494949494949494949494949494949");
        var snapshot = new LocalProfileSnapshot { ProfileId = profile, Currency = 3, RemoteRevision = 2, RequiresEquipmentHydration = true };
        snapshot.Stash.Add(new StashItem(new LootId("bone"), 2, 1));
        string before = LocalProfileSaveCodec.Encode(snapshot);
        Assert.That(ApplicationStashServiceBootstrapper.HydrateSnapshot(profile, snapshot, data, null, Catalog, null), Is.False);
        Assert.That(LocalProfileSaveCodec.Encode(snapshot), Is.EqualTo(before));
        Assert.That(snapshot.RemoteRevision, Is.EqualTo(2));
    }

    [Test]
    public void EquipV3_SendsIdentityOnlyAndNullUnassignments_WhileV2StillSendsStrings()
    {
        var equipment = new PreparedEquipmentLoadout(new LootId("rapier"), default,
            mainAInstance: new EquipmentInstance(Id, default(WeaponInstanceModifiers)));
        string json = InventoryRequestJson.Encode(new UpdatePreparedEquipmentRequest
        { instanceEquipment = PreparedEquipmentData.FromLoadout(equipment), expectedRevision = 4 });
        var body = Newtonsoft.Json.Linq.JObject.Parse(json);
        Assert.That(body["weaponSlot1"]?["lootId"]?.Value<string>(), Is.EqualTo("rapier"));
        Assert.That(body["weaponSlot1"]?["instanceId"]?.Value<string>(), Is.EqualTo(Id));
        Assert.That(body["weaponSlot1"]?["modifiers"], Is.Null);
        Assert.That(body["weaponSlot2"].Type, Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null));
        Assert.That(body.Value<int>("expectedRevision"), Is.EqualTo(4));
        Assert.That(json, Does.Not.Contain("instanceEquipment"));
        json = InventoryRequestJson.Encode(new UpdatePreparedEquipmentRequest { weaponSlot1 = "rapier", expectedRevision = 4 });
        Assert.That(Newtonsoft.Json.Linq.JObject.Parse(json).Value<string>("weaponSlot1"), Is.EqualTo("rapier"));
        Assert.That(json, Does.Not.Contain("instanceId"));
    }

    [Test]
    public void InstallGem_SerializesExactWireContract()
    {
        string json = InventoryRequestJson.Encode(new InstallGemRequest
        {
            transactionId = "install-1", weaponList = InventoryList.Stash, weaponSlot = 9, instanceId = Id,
            gemList = InventoryList.Loadout, gemSlot = 3, gemLootId = "RES_RUBY_GEM", expectedRevision = 4
        });
        Assert.That(json, Is.EqualTo("{\"transactionId\":\"install-1\",\"weaponList\":\"stash\",\"weaponSlot\":9,\"instanceId\":\"" + Id +
            "\",\"gemList\":\"loadout\",\"gemSlot\":3,\"gemLootId\":\"RES_RUBY_GEM\",\"expectedRevision\":4}"));
    }

    [Test]
    public async System.Threading.Tasks.Task GemInstallWithoutBackendId_IsRejectedBeforeSending()
    {
        var request = new InstallGemRequest { transactionId = "install-1", weaponList = InventoryList.Stash,
            weaponSlot = 9, gemList = InventoryList.Loadout, gemSlot = 3, gemLootId = "RES_RUBY_GEM" };
        var result = await InventoryClient.InstallGemAsync(null, null, request);
        Assert.That(result.success, Is.False);
        Assert.That(result.error.error, Is.EqualTo("INSTANCE_MISMATCH"));
        Assert.That(result.error.message, Is.EqualTo("A backend-issued weapon instance is required."));
        Assert.That(result.data.stash, Is.Null);
        Assert.That(result.data.loadout, Is.Null);
        Assert.That(request.instanceId, Is.Null);
    }

    [Test]
    public void V2_ResponseStillUsesStringAssignments_AndHydrationClearsCacheGuard()
    {
        var data = JsonConvert.DeserializeObject<InventoryData>("{\"inventorySchemaVersion\":2,\"preparedEquipment\":{\"weaponSlot1\":\"rapier\"},\"loadout\":[{\"slot\":7,\"lootId\":\"bone\",\"amount\":2}]}");
        var profile = new ProfileId("49494949494949494949494949494949");
        var snapshot = new LocalProfileSnapshot { ProfileId = profile, RequiresEquipmentHydration = true };
        Assert.That(ApplicationStashServiceBootstrapper.HydrateSnapshot(profile, snapshot, data, null, Catalog, null), Is.True);
        Assert.That(snapshot.PreparedEquipment.WeaponSetAMainHand.Value, Is.EqualTo("rapier"));
        Assert.That(snapshot.Loadout[0], Is.EqualTo(new StashItem(new LootId("bone"), 2, 7)));
        Assert.That(snapshot.RequiresEquipmentHydration, Is.False);
        using var request = InventoryProtocol.Get("http://localhost/inventory");
        Assert.That(request.GetRequestHeader(InventoryProtocol.Header), Is.EqualTo("3"), "v2 behavior uses the minimum-version-compatible header 3");
    }
}
#endif
