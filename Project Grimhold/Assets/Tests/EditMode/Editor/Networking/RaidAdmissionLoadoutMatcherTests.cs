#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public sealed class RaidAdmissionLoadoutMatcherTests
{
    private static LootDefinitionCatalog Catalog => AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
    private static readonly LootId Sword = new LootId("arming_sword");
    private static readonly LootId Bone = new LootId("bone");

    private sealed class Confirmation
    {
        public RaidAdmissionData Expected;
        public LootEntry[] Combined;
        public List<ExpeditionWeaponUnit> Units = new();
        public LootEntry?[] Slots = new LootEntry?[EquipmentSlotRules.AllSlots.Length];
        public WeaponInstanceModifiersNetworkData[] Modifiers = new WeaponInstanceModifiersNetworkData[EquipmentSlotRules.AllSlots.Length];
        public WeaponSetSlot ActiveSet = WeaponSetSlot.SetA;
        public bool Matches() => RaidAdmissionLoadoutMatcher.Matches(Expected, Combined, Units, Slots, Modifiers, ActiveSet, Catalog);
    }

    private static Confirmation Create(bool ticket)
    {
        Assert.That(WeaponInstanceModifiers.TryCreate(default, default, CharacterAttribute.Strength,
            1, default, default, out var first, out _), Is.True);
        Assert.That(WeaponInstanceModifiers.TryCreate(default, default, CharacterAttribute.Strength,
            2, default, default, out var second, out _), Is.True);
        var prepared = default(PreparedEquipmentLoadout)
            .With(EquipmentSlot.WeaponSetAMainHand, Sword, new EquipmentInstance(null, first))
            .With(EquipmentSlot.WeaponSetBMainHand, Sword, new EquipmentInstance(null, second));
        var inventory = new[]
        {
            new StashItem(Sword, 1, 0, new EquipmentInstance(null, first)),
            new StashItem(Sword, 1, 1, new EquipmentInstance(null, default(WeaponInstanceModifiers))),
            new StashItem(Bone, 3, 2), new StashItem(Bone, 2, 3)
        };
        var reservation = new PendingLoadoutReservation("reservation", inventory, prepared);
        RaidCode.TryParse("123456", out var code);
        var profile = new ProfileId("isolated-confirmation");
        Assert.That(RaidAdmissionData.TryCreate(code, profile, reservation,
            ProgressionBalanceDefaults.InitialCharacterAttributeState, 1, 0, 0, out var expected), Is.True);
        if (ticket)
        {
            Assert.That(ProfileInventoryRules.TryAggregate(inventory, out var entries), Is.True);
            var authoritative = new AuthoritativeRaidAdmissionTicket("isolated-ticket", profile, code,
                reservation.ReservationId, 1, 0, ProgressionBalanceDefaults.InitialCharacterAttributeState,
                default, entries.ToArray(), prepared, 0, long.MaxValue);
            Assert.That(authoritative.TryToRaidAdmissionData(out expected, catalog: Catalog), Is.True);
        }
        var result = new Confirmation { Expected = expected,
            Combined = new[] { new LootEntry(Sword, 4), new LootEntry(Bone, 5) } };
        Assert.That(Catalog.TryGet(Sword.Value, out var definition), Is.True);
        Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(first,
            definition.WeaponDefinition.NaturalScalingAttribute, Catalog, out var firstPacked, out _), Is.True);
        Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(second,
            definition.WeaponDefinition.NaturalScalingAttribute, Catalog, out var secondPacked, out _), Is.True);
        int a = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetAMainHand);
        int b = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetBMainHand);
        result.Slots[a] = result.Slots[b] = new LootEntry(Sword, 1);
        result.Modifiers[a] = firstPacked;
        result.Modifiers[b] = secondPacked;
        result.Units.Add(new ExpeditionWeaponUnit(Sword, RaidLootOrigin.Dungeon, firstPacked));
        result.Units.Add(new ExpeditionWeaponUnit(Sword, RaidLootOrigin.Dungeon, secondPacked));
        result.Units.Add(new ExpeditionWeaponUnit(Sword, RaidLootOrigin.Dungeon, firstPacked));
        return result;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Confirmation_CountsEquippedUnitsOnceAndPreservesRepeatedTemplateModifiers(bool ticket)
    {
        var confirmation = Create(ticket);
        Assert.That(confirmation.Matches(), Is.True);
        confirmation.Units.Reverse();
        Assert.That(confirmation.Matches(), Is.True, "Unit order is not ownership identity.");
    }

    private static IEnumerable<TestCaseData> Rejections()
    {
        foreach (bool ticket in new[] { false, true })
            foreach (string mutation in new[] { "missing", "extra", "double-counted", "modifier", "missing-modifier",
                "swapped-slots", "wrong-equipment", "empty-slot", "unexpected-slot", "active-set", "invalid-ownership" })
                yield return new TestCaseData(ticket, mutation);
    }

    [TestCaseSource(nameof(Rejections))]
    public void Confirmation_RejectsChangedOwnershipOrPreparationWithoutMutatingInputs(bool ticket, string mutation)
    {
        var c = Create(ticket);
        int a = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetAMainHand);
        int b = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetBMainHand);
        switch (mutation)
        {
            case "missing": c.Combined[1] = new LootEntry(Bone, 4); break;
            case "extra": c.Combined[1] = new LootEntry(Bone, 6); break;
            case "double-counted": c.Combined[0] = new LootEntry(Sword, 6); break;
            case "modifier": c.Units[0] = c.Units[1]; break;
            case "missing-modifier": c.Units.RemoveAt(0); break;
            case "swapped-slots": (c.Modifiers[a], c.Modifiers[b]) = (c.Modifiers[b], c.Modifiers[a]); break;
            case "wrong-equipment": c.Slots[a] = new LootEntry(new LootId("rapier"), 1); break;
            case "empty-slot": c.Slots[a] = null; break;
            case "unexpected-slot": c.Slots[Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.Helmet)] = new LootEntry(Sword, 1); break;
            case "active-set": c.ActiveSet = WeaponSetSlot.SetB; break;
            case "invalid-ownership": c.Combined[0] = default; break;
        }
        var totals = (LootEntry[])c.Combined.Clone();
        var units = c.Units.ToArray();
        var slots = (LootEntry?[])c.Slots.Clone();
        var modifiers = (WeaponInstanceModifiersNetworkData[])c.Modifiers.Clone();
        Assert.That(c.Matches(), Is.False);
        Assert.That(c.Combined, Is.EqualTo(totals));
        Assert.That(c.Units, Is.EqualTo(units));
        Assert.That(c.Slots, Is.EqualTo(slots));
        Assert.That(c.Modifiers, Is.EqualTo(modifiers));
    }

    [TestCase("training_short_sword")]
    [TestCase("training_dagger")]
    [TestCase("training_wand")]
    public void TrainingAcknowledgement_MatchesIdentityFreeBackendTicket(string id)
    {
        var profile = new ProfileId("training-admission");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(profile, Catalog), Is.True);
        var initial = repository.Snapshot.Clone();
        initial.InventorySchemaVersion = 3;
        Assert.That(repository.TrySave(initial, out _), Is.True);
        var store = new LocalProfileStore(repository, profile, Catalog);
        Assert.That(Catalog.TryGet(id, out var definition), Is.True);
        var localEquipment = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
            new LootId(id), new EquipmentInstance("111111111111111111111111", EquipmentInstance.PlainCache(definition).Modifiers));
        Assert.That(store.ApplyConfirmedTrainingWeapon(new Grimhold.Backend.TrainingWeaponResult
        {
            preparedEquipment = Grimhold.Backend.PreparedEquipmentData.FromLoadout(localEquipment), revision = 1,
            stash = Array.Empty<Grimhold.Backend.InventoryItemData>(), loadout = Array.Empty<Grimhold.Backend.InventoryItemData>()
        }, 0), Is.EqualTo(StashOperationResult.Success));
        Assert.That(AuthoritativeRaidAdmissionTicketCodec.TryDecode(TrainingWeaponAdmissionTicketTests.CreateTicket(id, true), out var ticket), Is.True);
        Assert.That(ticket.TryToRaidAdmissionData(out var expected, catalog: Catalog), Is.True);
        var prepared = store.GetPreparedEquipment();
        Assert.That(prepared.GetInstance(EquipmentSlot.WeaponSetAMainHand).HasIdentity, Is.True);
        Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(prepared.GetInstance(EquipmentSlot.WeaponSetAMainHand).Modifiers.Value,
            definition.WeaponDefinition.NaturalScalingAttribute, Catalog, out var packed, out _), Is.True);
        var slots = new LootEntry?[EquipmentSlotRules.AllSlots.Length];
        var modifiers = new WeaponInstanceModifiersNetworkData[slots.Length];
        int a = Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetAMainHand);
        slots[a] = new LootEntry(new LootId(id), 1);
        modifiers[a] = packed;
        var units = new List<ExpeditionWeaponUnit>();
        if (!packed.IsEmpty) units.Add(new ExpeditionWeaponUnit(new LootId(id), RaidLootOrigin.Dungeon, packed));
        Assert.That(RaidAdmissionLoadoutMatcher.Matches(expected, new[] { new LootEntry(new LootId(id), 1) },
            units, slots, modifiers, WeaponSetSlot.SetA, Catalog), Is.True);
    }

    public static bool MatchesInventory(IReadOnlyList<StashItem> inventory, IReadOnlyList<LootEntry> actual)
    {
        var prepared = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
            Sword, new EquipmentInstance(null, default(WeaponInstanceModifiers)));
        RaidCode.TryParse("123456", out var code);
        Assert.That(RaidAdmissionData.TryCreate(code, new ProfileId("isolated-resource"),
            new PendingLoadoutReservation("reservation", inventory, prepared),
            ProgressionBalanceDefaults.InitialCharacterAttributeState, 1, 0, 0, out var expected), Is.True);
        var combined = new List<StashItem>();
        foreach (var item in actual) combined.Add(new StashItem(item.LootId, item.Amount, combined.Count));
        combined.Add(new StashItem(Sword, 1, combined.Count));
        Assert.That(ProfileInventoryRules.TryAggregate(combined, out var totals), Is.True);
        var slots = new LootEntry?[EquipmentSlotRules.AllSlots.Length];
        slots[Array.IndexOf(EquipmentSlotRules.AllSlots, EquipmentSlot.WeaponSetAMainHand)] = new LootEntry(Sword, 1);
        return RaidAdmissionLoadoutMatcher.Matches(expected, totals, Array.Empty<ExpeditionWeaponUnit>(), slots,
            new WeaponInstanceModifiersNetworkData[slots.Length], WeaponSetSlot.SetA, Catalog);
    }
}
#endif
