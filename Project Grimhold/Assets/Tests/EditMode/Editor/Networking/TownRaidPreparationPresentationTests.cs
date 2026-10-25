using System.Collections.Generic;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public sealed class TownRaidPreparationPresentationTests
{
    [Test]
    public void ProfileWithoutPreparation_HasNoPreparationPresentation()
    {
        TownRaidPreparationSnapshot snapshot = CreateSnapshot("host", "client", true);

        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshot, new ProfileId("absent"), out _), Is.False);
    }

    [Test]
    public void Member_SeesOnlyResolvedPreparationAndRaidCapacitySource()
    {
        TownRaidPreparationSnapshot snapshotA = CreateSnapshot("host-a", "client-a", true);
        TownRaidPreparationSnapshot snapshotB = CreateSnapshot("host-b", "client-b", false);

        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshotA, new ProfileId("client-a"), out TownRaidPreparationPresentation presentation), Is.True);
        Assert.That(presentation.Snapshot.RaidCode, Is.EqualTo(snapshotA.RaidCode));
        Assert.That(presentation.LocalReady, Is.True);
        Assert.That(presentation.IsHost, Is.False);
        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshotB, new ProfileId("client-a"), out _), Is.False);
        Assert.That(TownRaidPreparationRules.MaxMembers, Is.EqualTo(16));
        Assert.That(RaidSessionRules.MaxParticipants, Is.EqualTo(16));
    }

    [Test]
    public void HostStart_IsDerivedFromHostAndAllReady()
    {
        TownRaidPreparationSnapshot ready = CreateSnapshot("host", "client", true);
        TownRaidPreparationSnapshot notReady = CreateSnapshot("host", "client", false);

        Assert.That(TownRaidPreparationPresentation.TryCreate(
            ready, ready.HostProfileId, out TownRaidPreparationPresentation canStart), Is.True);
        Assert.That(canStart.CanStart, Is.True);
        Assert.That(TownRaidPreparationPresentation.TryCreate(
            notReady, notReady.HostProfileId, out TownRaidPreparationPresentation blocked), Is.True);
        Assert.That(blocked.CanStart, Is.False);
    }

    [Test]
    public void Members_PreserveAuthoritativeOrderAndRefreshResolvedNamesWithoutSnapshotChange()
    {
        TownRaidPreparationSnapshot snapshot = CreateSnapshot("host", "client", false);
        var names = new Dictionary<ProfileId, string>
        {
            [snapshot.HostProfileId] = "Astra",
            [snapshot.Members[1].ProfileId] = "Bram"
        };

        bool Resolve(ProfileId profileId, out string displayName) =>
            names.TryGetValue(profileId, out displayName);

        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshot,
            snapshot.Members[1].ProfileId,
            Resolve,
            out TownRaidPreparationPresentation first), Is.True);
        Assert.That(first.Members[0].DisplayName, Is.EqualTo("Astra"));
        Assert.That(first.Members[0].IsHost, Is.True);
        Assert.That(first.Members[0].IsReady, Is.False);
        Assert.That(first.Members[1].DisplayName, Is.EqualTo("Bram"));
        Assert.That(first.Members[1].IsHost, Is.False);
        Assert.That(first.Members[1].IsReady, Is.False);

        names[snapshot.Members[1].ProfileId] = "Bramble";
        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshot,
            snapshot.Members[1].ProfileId,
            Resolve,
            out TownRaidPreparationPresentation refreshed), Is.True);
        Assert.That(refreshed.Members[1].DisplayName, Is.EqualTo("Bramble"));
    }

    private readonly List<LootDefinition> _definitions = new();

    [TearDown]
    public void TearDown()
    {
        foreach (LootDefinition definition in _definitions)
            UnityEngine.Object.DestroyImmediate(definition);
        _definitions.Clear();
    }

    [Test]
    public void Members_ExposeProfileAndLocalIdentity()
    {
        var snapshot = CreateSnapshot("host", "client", true);
        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshot, snapshot.Members[1].ProfileId, out var presentation), Is.True);
        Assert.That(presentation.Members[0].ProfileId, Is.EqualTo(snapshot.HostProfileId));
        Assert.That(presentation.Members[0].IsLocal, Is.False);
        Assert.That(presentation.Members[1].ProfileId, Is.EqualTo(snapshot.Members[1].ProfileId));
        Assert.That(presentation.Members[1].IsLocal, Is.True);
    }

    [Test]
    public void Equipment_CapturesEverySlotAndRevisionWithoutRetainingReaderState()
    {
        var snapshot = CreateSnapshot("host", "client", true);
        var reader = new FakePreparedEquipmentReader { ObservedEquipmentRevision = 7 };
        foreach (EquipmentSlot slot in EquipmentSlotRules.AllSlots)
        {
            var definition = UnityEngine.ScriptableObject.CreateInstance<LootDefinition>();
            _definitions.Add(definition);
            reader.Definitions[slot] = definition;
        }
        bool Resolve(ProfileId profileId, out IPreparedEquipmentReader resolved)
        {
            resolved = profileId == snapshot.HostProfileId ? reader : null;
            return resolved != null;
        }
        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshot, snapshot.HostProfileId, null, Resolve, out var presentation), Is.True);
        var member = presentation.Members[0];
        Assert.That(member.HasEquipment, Is.True);
        Assert.That(member.EquipmentRevision, Is.EqualTo(7));
        foreach (EquipmentSlot slot in EquipmentSlotRules.AllSlots)
        {
            Assert.That(member.TryGetEquipment(slot, out var definition), Is.True);
            Assert.That(definition, Is.SameAs(reader.Definitions[slot]));
        }
        reader.Definitions.Clear();
        reader.ObservedEquipmentRevision = 8;
        Assert.That(member.EquipmentRevision, Is.EqualTo(7));
        Assert.That(member.TryGetEquipment(EquipmentSlot.WeaponSetAMainHand, out _), Is.True);
        Assert.That(member.TryGetEquipment(EquipmentSlot.None, out _), Is.False);
        Assert.That(member.TryGetEquipment((EquipmentSlot)255, out _), Is.False);
        AssertEmptyEquipment(presentation.Members[1]);
    }

    [Test]
    public void NullEquipmentResolver_PreservesProjectionWithEmptyEquipment()
    {
        var snapshot = CreateSnapshot("host", "client", true);
        Assert.That(TownRaidPreparationPresentation.TryCreate(
            snapshot, snapshot.HostProfileId, null, null, out var presentation), Is.True);
        foreach (var member in presentation.Members)
            AssertEmptyEquipment(member);
    }

    private static void AssertEmptyEquipment(TownRaidPreparationPresentation.Member member)
    {
        Assert.That(member.HasEquipment, Is.False);
        Assert.That(member.EquipmentRevision, Is.Zero);
        foreach (EquipmentSlot slot in EquipmentSlotRules.AllSlots)
        {
            Assert.That(member.TryGetEquipment(slot, out var definition), Is.False);
            Assert.That(definition, Is.Null);
        }
    }

    private sealed class FakePreparedEquipmentReader : IPreparedEquipmentReader
    {
        public int ObservedEquipmentRevision { get; set; }
        public Dictionary<EquipmentSlot, LootDefinition> Definitions { get; } = new();
        public bool TryGetPreparedSlotDefinition(EquipmentSlot slot, out LootDefinition definition) =>
            Definitions.TryGetValue(slot, out definition);
    }

    private static TownRaidPreparationSnapshot CreateSnapshot(string hostValue, string clientValue, bool ready)
    {
        RaidCode.TryParse(hostValue == "host-a" ? "100001" : "100002", out RaidCode code);
        var host = new ProfileId(hostValue);
        return new TownRaidPreparationSnapshot(
            code,
            host,
            TownRaidPreparationState.Waiting,
            new[]
            {
                new TownRaidPreparationMember(host, ready),
                new TownRaidPreparationMember(new ProfileId(clientValue), ready)
            },
            1);
    }
}
