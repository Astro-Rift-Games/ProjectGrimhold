#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEditor;

/// <summary>Doc 09 §15.2: the training weapon returns to the Trainer on Town return only when a normal weapon is owned.</summary>
public sealed class TrainingWeaponExtractionReturnTests
{
    private const string TrainingWeapon = "training_dagger";
    private const string NormalWeapon = "arming_sword";
    private static readonly ProfileId Profile = new("48484848484848484848484848484848");
    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp() => _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

    [Test]
    public void ExtractionWithNormalWeaponInLoot_RemovesEquippedTrainingWeapon()
    {
        var store = CreateStore(out _);
        var prepared = EquippedTraining();

        Assert.That(store.TryCommitExtraction(Receipt(), new[] { Item(NormalWeapon, "222222222222222222222222", 0) },
            prepared, 0, 1, 0), Is.EqualTo(StashOperationResult.Success));

        Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand.IsValid, Is.False);
        Assert.That(store.GetLoadout(), Has.Count.EqualTo(1));
        Assert.That(store.GetLoadout()[0].LootId.Value, Is.EqualTo(NormalWeapon));
        AssertNoTrainingWeapon(store);
    }

    [Test]
    public void ExtractionWithoutNormalWeapon_KeepsTrainingWeapon()
    {
        var store = CreateStore(out _);
        var prepared = EquippedTraining();

        Assert.That(store.TryCommitExtraction(Receipt(), Array.Empty<StashItem>(), prepared, 0, 1, 0),
            Is.EqualTo(StashOperationResult.Success));

        Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand.Value, Is.EqualTo(TrainingWeapon));
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand),
            Is.EqualTo(prepared.GetInstance(EquipmentSlot.WeaponSetAMainHand)));
        Assert.That(store.GetLoadout(), Is.Empty);
    }

    [Test]
    public void ExtractionWithNormalWeaponOnlyInStash_RemovesTrainingWeaponFromExtractedLoot()
    {
        var store = CreateStore(out var repository);
        var snapshot = repository.Snapshot.Clone();
        snapshot.Stash.Add(Item(NormalWeapon, "333333333333333333333333", 0));
        Assert.That(repository.TrySave(snapshot, out _), Is.True);

        Assert.That(store.TryCommitExtraction(Receipt(), new[] { Item(TrainingWeapon, "111111111111111111111111", 0) },
            default, 0, 1, 0), Is.EqualTo(StashOperationResult.Success));

        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.GetStash(), Has.Count.EqualTo(1));
        Assert.That(store.GetStash()[0].LootId.Value, Is.EqualTo(NormalWeapon));
        AssertNoTrainingWeapon(store);
    }

    [Test]
    public void ExtractionWithNormalWeapon_LeavesRetryPayloadAsTheAuthoritativeRaidResult()
    {
        var store = CreateStore(out var repository);
        var prepared = EquippedTraining();

        Assert.That(store.TryCommitExtraction(Receipt(), new[] { Item(NormalWeapon, "222222222222222222222222", 0) },
            prepared, 0, 1, 0), Is.EqualTo(StashOperationResult.Success));

        // The backend settles the same rule authoritatively; the retry payload must stay the exact published result.
        var pending = repository.Snapshot.PendingExtractionCommit;
        Assert.That(pending, Is.Not.Null);
        Assert.That(pending.PreparedEquipment.WeaponSetAMainHand.Value, Is.EqualTo(TrainingWeapon));
    }

    private void AssertNoTrainingWeapon(LocalProfileStore store)
    {
        foreach (var item in store.GetLoadout()) Assert.That(IsTraining(item.LootId), Is.False);
        foreach (var item in store.GetStash()) Assert.That(IsTraining(item.LootId), Is.False);
        var equipment = store.GetPreparedEquipment();
        foreach (var slot in EquipmentSlotRules.AllSlots) Assert.That(IsTraining(equipment.Get(slot)), Is.False);
    }

    private bool IsTraining(LootId id) =>
        id.IsValid && _catalog.TryGet(id.Value, out var definition) && definition.IsTrainingWeapon;

    private PreparedEquipmentLoadout EquippedTraining() => default(PreparedEquipmentLoadout).With(
        EquipmentSlot.WeaponSetAMainHand, new LootId(TrainingWeapon), Instance(TrainingWeapon, "111111111111111111111111"));

    private StashItem Item(string id, string instanceId, int slot) => new(new LootId(id), 1, slot, Instance(id, instanceId));

    private EquipmentInstance Instance(string id, string instanceId)
    {
        Assert.That(_catalog.TryGet(id, out var definition), Is.True);
        return new EquipmentInstance(instanceId, EquipmentInstance.PlainCache(definition).Modifiers);
    }

    private static ExtractionReceipt Receipt() => new("training-return-raid", Profile, 1);

    private LocalProfileStore CreateStore(out InMemoryLocalProfileRepository repository)
    {
        repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _catalog), Is.True);
        var snapshot = repository.Snapshot.Clone();
        snapshot.InventorySchemaVersion = 3;
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        return new LocalProfileStore(repository, Profile, _catalog);
    }
}
#endif
