#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Grimhold.Backend;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>TASK-283 T7f: extraction carries weapon instance modifiers end to end (ID-free).</summary>
public sealed class ExtractionPayloadModifiersTests
{
    private LootDefinitionCatalog _catalog;
    private int _sword;
    private int _bone;

    private WeaponInstanceModifiers _modifiers;
    private int _packed;

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_catalog.TryGetIndex(new LootId("arming_sword"), out _sword), Is.True);
        Assert.That(_catalog.TryGetIndex(new LootId("bone"), out _bone), Is.True);
        _catalog.TryGetByIndex(_sword, out LootDefinition sword);
        CharacterAttribute natural = sword.WeaponDefinition.NaturalScalingAttribute;
        Assert.That(WeaponGemSocket.TryCreateOccupied(new LootId("RES_RUBY_GEM"), out WeaponGemSocket ruby, out string error),
            Is.True, error);
        Assert.That(WeaponInstanceModifiers.TryCreate(default, default, natural, 1, ruby, default, out _modifiers, out error),
            Is.True, error);
        Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(_modifiers, natural, _catalog,
            out WeaponInstanceModifiersNetworkData data, out error), Is.True, error);
        _packed = data.PackedValue;
        Assert.That(_packed, Is.Not.Zero);

    }

    [Test]
    public void Items_ExpandEquipmentIntoUnitsAndKeepModifiersOnTheModifiedOne()
    {
        Assert.That(ExtractionPayloadBuilder.TryBuildItems(_catalog,
            new[] { _sword, _bone }, new[] { 3, 2 }, new[] { _sword }, new[] { _packed }, out List<StashItem> items), Is.True);

        Assert.That(items, Has.Count.EqualTo(4));
        Assert.That(items[0].Amount, Is.EqualTo(1));
        Assert.That(items[0].Instance, Is.Not.Null);
        Assert.That(items[0].Instance.HasIdentity, Is.False);
        Assert.That(items[0].Instance.Modifiers, Is.EqualTo(_modifiers.Normalize()));
        Assert.That(items[1].Amount, Is.EqualTo(1));
        Assert.That(items[1].Instance, Is.Null);
        Assert.That(items[2].Amount, Is.EqualTo(1));
        Assert.That(items[2].Instance, Is.Null);
        Assert.That(items[3].LootId.Value, Is.EqualTo("bone"));
        Assert.That(items[3].Amount, Is.EqualTo(2));
    }

    [Test]
    public void Items_RejectMoreModifiedUnitsThanTheAmount()
    {
        Assert.That(ExtractionPayloadBuilder.TryBuildItems(_catalog,
            new[] { _sword }, new[] { 1 }, new[] { _sword, _sword }, new[] { _packed, _packed }, out _), Is.False);
    }

    [Test]
    public void Items_RejectModifiersOnNonEquipmentAndOrphanUnits()
    {
        Assert.That(ExtractionPayloadBuilder.TryBuildItems(_catalog,
            new[] { _bone }, new[] { 1 }, new[] { _bone }, new[] { _packed }, out _), Is.False);
        Assert.That(ExtractionPayloadBuilder.TryBuildItems(_catalog,
            new[] { _bone }, new[] { 1 }, new[] { _sword }, new[] { _packed }, out _), Is.False);
    }

    [Test]
    public void Prepared_CarriesHandModifiersAndOffHands()
    {
        int[] slots = { _sword, -1, -1, -1, -1, -1, _sword, -1 };
        Assert.That(ExtractionPayloadBuilder.TryBuildPrepared(_catalog, slots, new[] { _packed, 0, 0, 0 },
            out PreparedEquipmentLoadout prepared), Is.True);

        Assert.That(prepared.Get(EquipmentSlot.WeaponSetAMainHand).Value, Is.EqualTo("arming_sword"));
        Assert.That(prepared.GetInstance(EquipmentSlot.WeaponSetAMainHand).Modifiers, Is.EqualTo(_modifiers.Normalize()));
        Assert.That(prepared.Get(EquipmentSlot.WeaponSetAOffHand).Value, Is.EqualTo("arming_sword"));
        Assert.That(prepared.GetInstance(EquipmentSlot.WeaponSetAOffHand), Is.Null);
    }

    [Test]
    public void Prepared_RejectsModifiersOnAnEmptySlot()
    {
        int[] slots = { -1, -1, -1, -1, -1, -1, -1, -1 };
        Assert.That(ExtractionPayloadBuilder.TryBuildPrepared(_catalog, slots, new[] { _packed, 0, 0, 0 }, out _), Is.False);
    }

    [Test]
    public void PublishBody_IsIdFreeWithModifiersOnlyOnModifiedUnits()
    {
        Assert.That(ExtractionPayloadBuilder.TryBuildItems(_catalog,
            new[] { _sword }, new[] { 2 }, new[] { _sword }, new[] { _packed }, out List<StashItem> items), Is.True);
        Assert.That(ExtractionPayloadBuilder.TryBuildPrepared(_catalog,
            new[] { _sword, -1, -1, -1, -1, -1, -1, -1 }, new[] { _packed, 0, 0, 0 }, out PreparedEquipmentLoadout prepared), Is.True);
        var request = new PublishExtractionResultRequest
        {
            raidId = "raid", resultId = "raid:profile:1", resultSequence = 1, targetCharacterId = "profile",
            items = ToDto(items), preparedEquipment = PreparedEquipmentData.FromLoadout(prepared),
            experienceGranted = 0, issuedAt = "now", hostSignature = ""
        };

        JObject body = JObject.Parse(InventoryRequestJson.Encode(request));

        var jsonItems = (JArray)body["items"];
        Assert.That(jsonItems, Has.Count.EqualTo(2));
        Assert.That((int)jsonItems[0]["amount"], Is.EqualTo(1));
        Assert.That(jsonItems[0]["modifiers"]["gemSocketCount"].Value<int>(), Is.EqualTo(1));
        Assert.That(jsonItems[0]["modifiers"]["gems"][0].Value<string>(), Is.EqualTo("RES_RUBY_GEM"));
        Assert.That(jsonItems[0]["instanceId"], Is.Null);
        Assert.That((int)jsonItems[1]["amount"], Is.EqualTo(1));
        Assert.That(jsonItems[1]["modifiers"], Is.Null);
        JToken slot = body["preparedEquipment"]["weaponSlot1"];
        Assert.That(slot["lootId"].Value<string>(), Is.EqualTo("arming_sword"));
        Assert.That(slot["modifiers"]["gems"][0].Value<string>(), Is.EqualTo("RES_RUBY_GEM"));
        Assert.That(slot["instance"], Is.Null);
        Assert.That(body["preparedEquipment"]["weaponSlot2"].Type, Is.EqualTo(JTokenType.Null));
        Assert.That(body["preparedEquipment"]["offHand1"].Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void CommitBodies_StayQuantityOnlyAndMergeUnitsOfOneTemplate()
    {
        Assert.That(ExtractionPayloadBuilder.TryBuildItems(_catalog,
            new[] { _sword, _bone }, new[] { 3, 2 }, new[] { _sword }, new[] { _packed }, out List<StashItem> items), Is.True);

        JObject unified = JObject.Parse(InventoryRequestJson.Encode(
            new CommitExtractionUnifiedRequest { items = ToDto(items) }));

        var jsonItems = (JArray)unified["items"];
        Assert.That(jsonItems, Has.Count.EqualTo(2));
        Assert.That(jsonItems[0]["lootId"].Value<string>(), Is.EqualTo("arming_sword"));
        Assert.That((int)jsonItems[0]["amount"], Is.EqualTo(3));
        Assert.That(jsonItems[0]["modifiers"], Is.Null);
        Assert.That((int)jsonItems[1]["amount"], Is.EqualTo(2));
    }

    private static InventoryItemData[] ToDto(IReadOnlyList<StashItem> items)
    {
        var result = new InventoryItemData[items.Count];
        for (int index = 0; index < items.Count; index++)
        {
            result[index] = new InventoryItemData
            {
                lootId = items[index].LootId.Value,
                amount = items[index].Amount,
                instance = EquipmentInstanceData.FromInstance(items[index].Instance)
            };
        }

        return result;
    }
}
#endif
