#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Text;
using Grimhold.Backend;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class TrainingWeaponAdmissionTicketTests
{
    public static LootDefinitionCatalog Catalog => AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

    public static string CreateTicket(string id, bool minimalBackendModifiers = false)
    {
        Assert.That(Catalog.TryGet(id, out var definition), Is.True);
        var equipment = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
            new LootId(id), EquipmentInstance.PlainCache(definition));
        Assert.That(RaidCode.TryParse("123456", out var raid), Is.True);
        string raw = AuthoritativeRaidAdmissionTicketCodec.CreateTestTicket(new ProfileId("training-admission"), raid,
            "0123456789abcdef0123456789abcdef", 1, 0, ProgressionBalanceDefaults.InitialCharacterAttributeState,
            default, Array.Empty<LootEntry>(), equipment);
        if (!minimalBackendModifiers) return raw;
        string body = raw.Split('.')[0].Replace('-', '+').Replace('_', '/');
        body = body.PadRight((body.Length + 3) / 4 * 4, '=');
        var json = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(body)));
        json["snapshot"]["preparedEquipment"]["weaponSlot1"] = new JObject
        {
            ["lootId"] = id,
            ["modifiers"] = new JObject { ["gemSocketCount"] = 0, ["gems"] = new JArray() }
        };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToString(Formatting.None)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." + new string('0', 64);
    }

    [TestCase("training_short_sword")]
    [TestCase("training_dagger")]
    [TestCase("training_wand")]
    public void TicketDecode_PreservesTrainingWeaponAndPlainCacheUnits(string id)
    {
        Assert.That(AuthoritativeRaidAdmissionTicketCodec.TryDecode(CreateTicket(id), out var ticket), Is.True);
        Assert.That(ticket.PreparedEquipment.WeaponSetAMainHand.Value, Is.EqualTo(id));
        var instance = ticket.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand);
        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.HasIdentity, Is.False);
        Assert.That(Catalog.TryGet(id, out var definition), Is.True);
        Assert.That(instance.Modifiers, Is.EqualTo(EquipmentInstance.PlainCache(definition).Modifiers));
        Assert.That(ticket.TryToRaidAdmissionData(out var admission, catalog: Catalog), Is.True);
        Assert.That(admission.ReservedLoadout.Count, Is.EqualTo(1));
        Assert.That(admission.ReservedLoadout[admission.WeaponSetAMainHandEntryIndexPlusOne - 1].LootId.Value, Is.EqualTo(id));
        Assert.That(ticket.ReservedItems, Is.Empty, "No recovery arming_sword is granted.");
    }
}
#endif
