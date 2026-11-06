#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class RaidAdmissionInstanceTests
{
    private LootDefinitionCatalog Catalog => AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

    [Test]
    public void LocalReservation_ProjectsDistinctUnitsAndPreparedReferencesWithoutDurableIds()
    {
        WeaponInstanceModifiers.TryCreate(default, default, CharacterAttribute.Strength, 2,
            default, default, out var sockets, out _);
        var sword = new LootId("arming_sword");
        var first = new EquipmentInstance("000000000000000000000001", sockets);
        var second = new EquipmentInstance("000000000000000000000002", default(WeaponInstanceModifiers));
        var prepared = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand, sword, first)
            .With(EquipmentSlot.WeaponSetBMainHand, sword, second);
        var reservation = new PendingLoadoutReservation("0123456789abcdef0123456789abcdef", new[] {
            new StashItem(sword, 1, 9, first), new StashItem(sword, 1, 2, second),
            new StashItem(new LootId("bone"), 3, 4), new StashItem(new LootId("bone"), 2, 7) }, prepared);
        RaidCode.TryParse("123456", out var code);
        Assert.That(RaidAdmissionData.TryCreate(code, new ProfileId("profile"), reservation,
            ProgressionBalanceDefaults.InitialCharacterAttributeState, 1, 0, 0, out var admission), Is.True);
        Assert.That(admission.ReservedLoadout.Count, Is.EqualTo(5));
        Assert.That(admission.ReservedLoadout.Count(e => e.LootId == sword && e.Amount == 1), Is.EqualTo(4));
        Assert.That(admission.ReservedLoadout.Single(e => e.LootId == new LootId("bone")).Amount, Is.EqualTo(5));
        Assert.That(admission.WeaponSetAMainHandEntryIndexPlusOne, Is.Not.EqualTo(admission.WeaponSetBMainHandEntryIndexPlusOne));
        Assert.That(RaidAdmissionDataCodec.TryEncode(admission, out var bytes), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(bytes, out var decoded), Is.True);
        Assert.That(decoded.ReservedLoadout, Is.EqualTo(admission.ReservedLoadout));
        Assert.That(Encoding.UTF8.GetString(bytes), Does.Not.Contain(first.InstanceId).And.Not.Contain(second.InstanceId));
        Assert.That(reservation.Items.Count, Is.EqualTo(4));
        Assert.That(reservation.Items[0].Instance, Is.SameAs(first));
        Assert.That(reservation.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetBMainHand), Is.SameAs(second));
    }

    [Test]
    public void SignedMaximumCapacitySnapshot_DecodesThirtyInventoryAndFourPreparedWeapons()
    {
        var payload = RaidAdmissionPayloadChunkingTests.CreateMaximumCapacityTicket();
        Assert.That(AuthoritativeRaidAdmissionTicketCodec.TryDecode(payload, out var ticket), Is.True);
        Assert.That(ticket.TryToRaidAdmissionData(out var admission), Is.True);
        Assert.That(admission.ReservedLoadout.Count, Is.EqualTo(34));
        Assert.That(admission.ReservedLoadout.All(e => e.Amount == 1), Is.True);
        Assert.That(admission.EntryIndicesPlusOne.Where(i => i > 0).Distinct().Count(), Is.EqualTo(4));
        Assert.That(RaidAdmissionDataCodec.TryEncode(admission, out var bytes), Is.True);
        Assert.That(RaidAdmissionDataCodec.TryDecode(bytes, out var decoded), Is.True);
        Assert.That(decoded.ReservedLoadout, Is.EqualTo(admission.ReservedLoadout));
    }

    [TestCase("bad-grade")]
    [TestCase("bad-sockets")]
    [TestCase("instance-id")]
    public void SignedSnapshot_RejectsMalformedModifierOrDurableIdentity(string mutation)
    {
        string raw = Encoding.UTF8.GetString(RaidAdmissionPayloadChunkingTests.CreateMaximumCapacityTicket());
        string body = raw.Split('.')[0].Replace('-', '+').Replace('_', '/');
        body = body.PadRight((body.Length + 3) / 4 * 4, '=');
        var json = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(body)));
        var item = json["snapshot"]["items"][0];
        if (mutation == "bad-grade") item["modifiers"]["primary"]["grade"] = "Z";
        if (mutation == "bad-sockets") item["modifiers"]["gemSocketCount"] = 3;
        if (mutation == "instance-id") item["instanceId"] = "000000000000000000000001";
        string changed = Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." + new string('0', 64);
        Assert.That(AuthoritativeRaidAdmissionTicketCodec.TryDecode(changed, out _), Is.False);
    }
}
#endif
