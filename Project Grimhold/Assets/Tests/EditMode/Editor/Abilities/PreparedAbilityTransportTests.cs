#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[Category("TASK206")]
public sealed class PreparedAbilityTransportTests
{
    private const string MissionIdValue = "mission_transport_trap";
    private const string ReservationId = "0123456789abcdef0123456789abcdef";
    private const long ChargePrice = 100L;

    private static readonly ProfileId Profile = new("96969696969696969696969696969696");

    private LootDefinitionCatalog _lootCatalog;
    private AbilityDefinition _charge;
    private AbilityDefinition _trap;
    private AbilityDefinitionCatalog _abilityCatalog;
    private MissionDefinition _mission;

    [SetUp]
    public void SetUp()
    {
        _lootCatalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
        _charge = AbilityTestFactory.CreateDefinition(
            "charge",
            requirements: new CharacterAttributeRequirement(CharacterAttribute.Strength, 10));
        _trap = AbilityTestFactory.CreateDefinition("trap");
        _abilityCatalog = AbilityTestFactory.CreateCatalog(_charge, _trap);

        _mission = ScriptableObject.CreateInstance<MissionDefinition>();
        typeof(MissionDefinition)
            .GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_mission, MissionIdValue);
        _mission.Rewards = new List<RewardDefinition>
        {
            new() { Type = RewardDefinition.RewardType.Ability, ReferenceId = "trap", Amount = 1 }
        };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_mission);
        Object.DestroyImmediate(_abilityCatalog);
        Object.DestroyImmediate(_charge);
        Object.DestroyImmediate(_trap);
        Object.DestroyImmediate(_lootCatalog);
    }

    [TestCase(null, null)]
    [TestCase("charge", null)]
    [TestCase(null, "trap")]
    [TestCase("trap", null)]
    [TestCase(null, "charge")]
    [TestCase("charge", "trap")]
    [TestCase("trap", "charge")]
    public void Raid_ReceivesExactlyThePreparedAbilities(string slot1, string slot2)
    {
        LocalProfileStore store = CreateStoreWithAcquiredAbilities();
        PreparedAbilityLoadout expected = Prepare(store, slot1, slot2);

        PreparedAbilityLoadout received = Transport(store, out RaidAdmissionData decoded);

        Assert.That(received, Is.EqualTo(expected));
        Assert.That(received.Slot1.IsValid, Is.EqualTo(!string.IsNullOrEmpty(slot1)));
        Assert.That(received.Slot2.IsValid, Is.EqualTo(!string.IsNullOrEmpty(slot2)));
        Assert.That(decoded.ProfileId, Is.EqualTo(Profile));
        Assert.That(decoded.PreparedAbilities, Is.EqualTo(expected));
    }

    [Test]
    public void Raid_RebuildsOriginalLoadoutAfterTheLocalProfileChanges()
    {
        LocalProfileStore store = CreateStoreWithAcquiredAbilities();
        PreparedAbilityLoadout expected = Prepare(store, "charge", "trap");
        byte[] token = BuildToken(store);

        Assert.That(
            store.TryClearPreparedAbility(UniversalAbilitySlot.Slot1),
            Is.EqualTo(AbilityPreparationResult.Success));
        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, new AbilityId("charge")),
            Is.EqualTo(AbilityPreparationResult.Success));
        Assert.That(store.GetPreparedAbilities().Slot1.IsValid, Is.False);
        Assert.That(store.GetPreparedAbilities().Slot2, Is.EqualTo(new AbilityId("charge")));

        PreparedAbilityLoadout received = RebuildFromToken(token);

        Assert.That(received, Is.EqualTo(expected));
    }

    [Test]
    public void Raid_RebuildsOriginalLoadoutAfterPreparingADifferentAbility()
    {
        LocalProfileStore store = CreateStoreWithAcquiredAbilities();
        PreparedAbilityLoadout expected = Prepare(store, "trap", null);
        byte[] token = BuildToken(store);

        Assert.That(
            store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId("charge")),
            Is.EqualTo(AbilityPreparationResult.Success));
        Assert.That(store.GetPreparedAbilities().Slot1, Is.EqualTo(new AbilityId("charge")));

        Assert.That(RebuildFromToken(token), Is.EqualTo(expected));
    }

    [Test]
    public void Raid_DoesNotReceiveAnAbilityInvalidatedBeforeAdmission()
    {
        LocalProfileStore store = CreateStoreWithAcquiredAbilities();
        Prepare(store, "charge", "trap");

        store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 9));
        PreparedAbilityLoadout received = Transport(store, out _);

        Assert.That(received.Slot1.IsValid, Is.False);
        Assert.That(received.Slot2, Is.EqualTo(new AbilityId("trap")));
        Assert.That(store.IsAbilityUnlocked(new AbilityId("charge")), Is.True);
    }

    private LocalProfileStore CreateStoreWithAcquiredAbilities()
    {
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _lootCatalog), Is.True);
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.ActiveMissions.Add(
            new MissionInstanceState(new MissionId(MissionIdValue), MissionState.PendienteDeReclamar));
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        var store = new LocalProfileStore(
            repository,
            Profile,
            lootCatalog: _lootCatalog,
            abilityCatalog: _abilityCatalog);

        // "trap" arrives as a mission reward, "charge" through the shop purchase path.
        Assert.That(store.TryClaimMission(_mission), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryCreditCurrency(ChargePrice), Is.EqualTo(StashOperationResult.Success));
        Assert.That(
            store.TryPurchaseAbility(new AbilityId("charge"), ChargePrice),
            Is.EqualTo(AbilityPurchaseResult.Success));
        store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 10));
        Assert.That(store.IsAbilityUnlocked(new AbilityId("trap")), Is.True);
        Assert.That(store.IsAbilityUnlocked(new AbilityId("charge")), Is.True);
        return store;
    }

    private static PreparedAbilityLoadout Prepare(LocalProfileStore store, string slot1, string slot2)
    {
        if (!string.IsNullOrEmpty(slot1))
        {
            Assert.That(
                store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, new AbilityId(slot1)),
                Is.EqualTo(AbilityPreparationResult.Success));
        }

        if (!string.IsNullOrEmpty(slot2))
        {
            Assert.That(
                store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, new AbilityId(slot2)),
                Is.EqualTo(AbilityPreparationResult.Success));
        }

        PreparedAbilityLoadout prepared = store.GetPreparedAbilities();
        Assert.That(prepared.Slot1.IsValid, Is.EqualTo(!string.IsNullOrEmpty(slot1)));
        Assert.That(prepared.Slot2.IsValid, Is.EqualTo(!string.IsNullOrEmpty(slot2)));
        return prepared;
    }

    private static PreparedAbilityLoadout Transport(LocalProfileStore store, out RaidAdmissionData decoded)
    {
        byte[] token = BuildToken(store);
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out decoded), Is.True);
        return RebuildFromToken(token);
    }

    // Mirrors FusionSessionLauncher: store snapshot -> RaidAdmissionData.TryCreate -> codec encode.
    private static byte[] BuildToken(LocalProfileStore store)
    {
        Assert.That(RaidCode.TryParse("038271", out RaidCode code), Is.True);
        LootId sword = new("arming_sword");
        var reservation = new PendingLoadoutReservation(
            ReservationId,
            new[] { new StashItem(new LootId("coins"), 4) },
            new PreparedEquipmentLoadout(sword, sword));

        Assert.That(
            store.TryGetRaidAdmissionAbilitySnapshot(
                out CharacterAttributeState attributes,
                out PreparedAbilityLoadout prepared,
                out string error),
            Is.True,
            error);
        Assert.That(
            RaidAdmissionData.TryCreate(
                code,
                Profile,
                reservation,
                attributes,
                prepared,
                store.GetLevel(),
                store.GetCurrentExperience(),
                store.GetLastAppliedProgressionResultSequence(),
                out RaidAdmissionData admissionData),
            Is.True);
        Assert.That(RaidAdmissionDataCodec.TryEncode(admissionData, out byte[] token), Is.True);
        return token;
    }

    // Mirrors the participant spawn path: decoded admission slots -> NetworkRaidParticipant entitlement.
    private static PreparedAbilityLoadout RebuildFromToken(byte[] token)
    {
        Assert.That(RaidAdmissionDataCodec.TryDecode(token, out RaidAdmissionData decoded), Is.True);
        Assert.That(
            NetworkRaidParticipant.TryBuildPreparedAbilityLoadout(
                true,
                decoded.PreparedAbilities.Slot1.ToString(),
                decoded.PreparedAbilities.Slot2.ToString(),
                out PreparedAbilityLoadout rebuilt),
            Is.True);
        return rebuilt;
    }
}
#endif
