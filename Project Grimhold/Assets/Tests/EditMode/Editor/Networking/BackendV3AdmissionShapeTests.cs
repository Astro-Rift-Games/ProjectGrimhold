#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Text;
using Grimhold.Backend;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>Characterizes the v3 session-ticket preparedEquipment payload emitted by backend SessionTicketService.</summary>
public sealed class BackendV3AdmissionShapeTests
{
    private const string Id = "arming_sword";
    private static readonly string[] Slots =
    { "weaponSlot1", "weaponSlot2", "offHand1", "helmet", "armor", "gloves", "boots", "offHand2" };

    private static string CreateTicket(JObject preparedEquipment)
    {
        string raw = TrainingWeaponAdmissionTicketTests.CreateTicket(Id);
        string body = raw.Split('.')[0].Replace('-', '+').Replace('_', '/');
        body = body.PadRight((body.Length + 3) / 4 * 4, '=');
        var json = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(body)));
        json["snapshot"]["preparedEquipment"] = preparedEquipment;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToString(Formatting.None)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." + new string('0', 64);
    }

    private static JObject BackendEquipment()
    {
        var equipment = new JObject();
        foreach (string slot in Slots) equipment[slot] = JValue.CreateNull();
        equipment["weaponSlot1"] = new JObject
        {
            ["lootId"] = Id,
            ["modifiers"] = new JObject
            {
                ["primary"] = new JObject { ["attribute"] = "strength", ["grade"] = "E" },
                ["gemSocketCount"] = 2,
                ["gems"] = new JArray(JValue.CreateNull(), JValue.CreateNull())
            }
        };
        return equipment;
    }

    [Test]
    public void BackendPreparedEquipment_AllEightSlots_DecodesAndAdmits()
    {
        string raw = CreateTicket(BackendEquipment());

        Assert.That(AuthoritativeRaidAdmissionTicketCodec.TryDecode(raw, out var ticket), Is.True);
        Assert.That(ticket.PreparedEquipment.WeaponSetAMainHand.Value, Is.EqualTo(Id));
        var instance = ticket.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand);
        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.HasIdentity, Is.False);
        Assert.That(instance.Modifiers.HasValue, Is.True);
        var modifiers = instance.Modifiers.Value;
        Assert.That(modifiers.Primary.Attribute, Is.EqualTo(CharacterAttribute.Strength));
        Assert.That(modifiers.Primary.Grade.ToString(), Is.EqualTo("E"));
        Assert.That(modifiers.GemSocketCount, Is.EqualTo(2));
        for (int i = 0; i < 2; i++)
        {
            Assert.That(modifiers.TryGetGemSocket(i, out var socket), Is.True);
            Assert.That(socket.IsOccupied, Is.False);
        }

        Assert.That(ticket.TryToRaidAdmissionData(out var admission, catalog: TrainingWeaponAdmissionTicketTests.Catalog), Is.True);
        Assert.That(admission.WeaponSetAMainHandEntryIndexPlusOne, Is.GreaterThan(0));
        Assert.That(admission.ReservedLoadout[admission.WeaponSetAMainHandEntryIndexPlusOne - 1].LootId.Value, Is.EqualTo(Id));
    }

    [Test]
    public void MongooseInternalsPreparedEquipment_IsRejected()
    {
        var equipment = new JObject
        {
            ["$__"] = new JObject { ["activePaths"] = new JObject() },
            ["_doc"] = new JObject
            {
                ["weaponSlot1"] = new JObject
                {
                    ["lootId"] = Id,
                    ["instance"] = new JObject { ["instanceId"] = "x" }
                }
            }
        };
        string raw = CreateTicket(equipment);

        bool decoded = AuthoritativeRaidAdmissionTicketCodec.TryDecode(raw, out var ticket);
        bool admitted = decoded &&
            ticket.TryToRaidAdmissionData(out _, catalog: TrainingWeaponAdmissionTicketTests.Catalog);
        Assert.That(decoded && admitted, Is.False, $"decoded={decoded}, admitted={admitted}");
    }
}
#endif
