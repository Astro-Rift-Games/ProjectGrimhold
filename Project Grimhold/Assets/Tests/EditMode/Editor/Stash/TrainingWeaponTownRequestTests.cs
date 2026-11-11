#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;

public sealed class TrainingWeaponTownRequestTests
{
    private LootDefinitionCatalog _catalog;
    private static readonly ProfileId Profile = new("47474747474747474747474747474747");

    [SetUp]
    public void SetUp() => _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

    [Test]
    public void AuthoritativeSuccessiveGrants_ReplaceAtomically_WithoutDuplicateOrInvalidInstances()
    {
        var store = CreateStore(out _);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        var issuedIds = new HashSet<string>();
        int revision = 0;
        foreach (string weapon in new[] { "training_short_sword", "training_dagger", "training_wand", "training_dagger" })
        {
            string instanceId = (++revision).ToString("x24");
            Assert.That(store.ApplyConfirmedTrainingWeapon(Response(weapon, instanceId, revision), revision - 1),
                Is.EqualTo(StashOperationResult.Success));
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.GetLoadout(), Is.Empty);
            var equipment = store.GetPreparedEquipment();
            Assert.That(equipment.WeaponSetAMainHand.Value, Is.EqualTo(weapon));
            var identities = new HashSet<string>();
            int count = 0;
            foreach (var slot in EquipmentSlotRules.AllSlots)
            {
                if (!equipment.Get(slot).IsValid) continue;
                count++;
                var instance = equipment.GetInstance(slot);
                Assert.That(instance.HasIdentity, Is.True);
                Assert.That(identities.Add(instance.InstanceId), Is.True, "Every held instance must be unique.");
                Assert.That(issuedIds.Add(instance.InstanceId), Is.True, "The new grant must use the newly issued backend instance.");
            }
            Assert.That(count, Is.EqualTo(1));
            Assert.That(store.RemoteRevision, Is.EqualTo(revision));
            Assert.That(commits, Is.EqualTo(revision));
            Assert.That(PreparedEquipmentLoadout.HasUsableWeapon(equipment, _catalog), Is.True);
            Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
        }
    }

    [Test]
    public void RejectedSnapshot_LeavesStateRevisionAndEventsUntouched()
    {
        var store = CreateStore(out _);
        Assert.That(store.ApplyConfirmedTrainingWeapon(Response("training_dagger", "111111111111111111111111", 1), 0),
            Is.EqualTo(StashOperationResult.Success));
        var previous = store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand);
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        Assert.That(store.ApplyConfirmedTrainingWeapon(Response("training_wand", "222222222222222222222222", 2), 0),
            Is.EqualTo(StashOperationResult.InvalidInventory));
        Assert.That(store.RemoteRevision, Is.EqualTo(1));
        Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand.Value, Is.EqualTo("training_dagger"));
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(previous));
        Assert.That(store.GetLoadout(), Is.Empty);
        Assert.That(store.GetStash(), Is.Empty);
        Assert.That(commits, Is.Zero);
    }

    [Test]
    public void Acknowledgement_PreservesBackendDisplacedNormalWeaponAndInstance()
    {
        var store = CreateStore(out var repository);
        Assert.That(_catalog.TryGet("arming_sword", out var normalDefinition), Is.True);
        var normalInstance = new EquipmentInstance("333333333333333333333333", EquipmentInstance.PlainCache(normalDefinition).Modifiers);
        var snapshot = repository.Snapshot.Clone();
        snapshot.PreparedEquipment = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
            new LootId("arming_sword"), normalInstance);
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        var response = Response("training_dagger", "111111111111111111111111", 1);
        response.loadout = new[] { new InventoryItemData
        {
            slot = 5, lootId = "arming_sword", amount = 1, instance = EquipmentInstanceData.FromInstance(normalInstance)
        } };
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        Assert.That(store.ApplyConfirmedTrainingWeapon(response, 0), Is.EqualTo(StashOperationResult.Success));
        Assert.That(commits, Is.EqualTo(1));
        Assert.That(store.RemoteRevision, Is.EqualTo(1));
        Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand.Value, Is.EqualTo("training_dagger"));
        Assert.That(store.GetLoadout().Count, Is.EqualTo(1));
        var displaced = store.GetLoadout()[0];
        Assert.That(displaced.LootId.Value, Is.EqualTo("arming_sword"));
        Assert.That(displaced.Amount, Is.EqualTo(1));
        Assert.That(displaced.Slot, Is.EqualTo(5));
        Assert.That(displaced.Instance, Is.EqualTo(normalInstance));
        Assert.That(store.GetStash(), Is.Empty);
        Assert.That(displaced.Instance.InstanceId, Is.Not.EqualTo(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId));
    }

    [TestCase("training_short_sword")]
    [TestCase("training_dagger")]
    [TestCase("training_wand")]
    public void SelectedBackendInstance_IsReservedWithoutSubstitution(string weapon)
    {
        var store = CreateStore(out _);
        const string instanceId = "111111111111111111111111";
        Assert.That(store.ApplyConfirmedTrainingWeapon(Response(weapon, instanceId, 1), 0),
            Is.EqualTo(StashOperationResult.Success));
        var selected = store.GetPreparedEquipment();
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(commits, Is.Zero);
        Assert.That(store.TryCreateLoadoutReservation("selected-training", out var reservation),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(reservation.PreparedEquipment, Is.EqualTo(selected));
        Assert.That(reservation.PreparedEquipment.WeaponSetAMainHand.Value, Is.EqualTo(weapon));
        Assert.That(reservation.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId,
            Is.EqualTo(instanceId));
        Assert.That(reservation.Items, Is.Empty);
        Assert.That(store.GetPreparedEquipment().HasAnyEquipment, Is.False);
        Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(commits, Is.EqualTo(1), "Validation of reserved equipment must not commit.");
    }

    [Test]
    public void OrdinaryReconciliation_PreservesNormalInstancesAndPreparationDoesNotCommit()
    {
        var store = CreateStore(out _);
        var equipment = new PreparedEquipmentData();
        equipment.Set(EquipmentSlot.WeaponSetAMainHand, "arming_sword",
            new EquipmentInstanceData { instanceId = "444444444444444444444444" }, true);
        var inventory = new InventoryData
        {
            inventorySchemaVersion = 3, revision = 7, currency = 10,
            preparedEquipment = equipment,
            loadout = Array.Empty<InventoryItemData>(),
            stash = new[] { new InventoryItemData
            {
                slot = 4, lootId = "arming_sword", amount = 1,
                instance = new EquipmentInstanceData
                {
                    instanceId = "555555555555555555555555",
                    modifiers = new WeaponInstanceModifiersData { gemSocketCount = 1, gems = new string[] { null } }
                }
            } }
        };
        int commits = 0;
        store.ProfileCommitted += _ => commits++;
        Assert.That(store.ReconcileRemoteState(inventory, null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(store.GetStash()[0].Instance.InstanceId, Is.EqualTo("555555555555555555555555"));
        Assert.That(store.GetStash()[0].Slot, Is.EqualTo(4));
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId,
            Is.EqualTo("444444444444444444444444"));
        Assert.That(store.RemoteRevision, Is.EqualTo(7));
        Assert.That(commits, Is.EqualTo(1));
    }

    private LocalProfileStore CreateStore(out InMemoryLocalProfileRepository repository)
    {
        repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, _catalog), Is.True);
        var snapshot = repository.Snapshot.Clone();
        snapshot.InventorySchemaVersion = 3;
        Assert.That(repository.TrySave(snapshot, out _), Is.True);
        return new LocalProfileStore(repository, Profile, _catalog);
    }

    private TrainingWeaponResult Response(string id, string instanceId, int revision)
    {
        Assert.That(_catalog.TryGet(id, out var definition), Is.True);
        var equipment = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
            new LootId(id), new EquipmentInstance(instanceId, EquipmentInstance.PlainCache(definition).Modifiers));
        return new TrainingWeaponResult
        {
            preparedEquipment = PreparedEquipmentData.FromLoadout(equipment),
            stash = Array.Empty<InventoryItemData>(), loadout = Array.Empty<InventoryItemData>(), revision = revision
        };
    }
}
#endif
