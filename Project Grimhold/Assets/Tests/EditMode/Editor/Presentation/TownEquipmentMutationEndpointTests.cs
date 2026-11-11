#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Presentation
{
    public sealed class TownEquipmentMutationEndpointTests
    {
        private static readonly ProfileId Profile = new("30303030303030303030303030303030");
        private LootDefinitionCatalog _catalog;
        private FakeLoadoutService _service;
        private bool _canMutate;
        private TownEquipmentMutationEndpoint _endpoint;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(_catalog, Is.Not.Null);
            _service = new FakeLoadoutService();
            _canMutate = true;
            _endpoint = new TownEquipmentMutationEndpoint(
                _service,
                Profile,
                _catalog,
                () => _canMutate);
        }

        [Test]
        public void Equip_UsesDeterministicCurrentSlotsAndSupportsReplacement()
        {
            LootId sword = new("rapier");
            LootId recoverySword = new("arming_sword");
            _service.Loadout = new[] { new StashItem(sword, 1), new StashItem(recoverySword, 1) };

            Assert.That(_endpoint.TryEquip(sword, EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_service.LastAssignedSlot, Is.EqualTo(EquipmentSlot.WeaponSetAMainHand));
            Assert.That(_endpoint.TryEquip(recoverySword, EquipmentSlot.WeaponSetBMainHand), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_service.LastAssignedSlot, Is.EqualTo(EquipmentSlot.WeaponSetBMainHand));

            Assert.That(_endpoint.TryEquip(sword, EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(StashOperationResult.Success));
            Assert.That(
                _service.LastAssignedSlot,
                Is.EqualTo(EquipmentSlot.WeaponSetAMainHand),
                "A third weapon deterministically replaces the first current weapon slot.");
        }

        [Test]
        public void ReadyGate_BlocksEquipAndExactUnequipWithoutCallingPersistence()
        {
            LootId sword = new("rapier");
            _service.Loadout = new[] { new StashItem(sword, 1) };
            _canMutate = false;

            Assert.That(_endpoint.CanEquip(sword, EquipmentSlot.WeaponSetAMainHand), Is.False);
            Assert.That(_endpoint.TryEquip(sword, EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(
                _endpoint.TryUnequip(EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_service.AssignmentCalls, Is.Zero);
            Assert.That(_service.ClearCalls, Is.Zero);

            _canMutate = true;
            Assert.That(_endpoint.TryUnequip(EquipmentSlot.WeaponSetBMainHand), Is.EqualTo(StashOperationResult.Success));
            Assert.That(_service.LastClearedSlot, Is.EqualTo(EquipmentSlot.WeaponSetBMainHand));
        }

        [Test]
        public async System.Threading.Tasks.Task RequestTrainingWeapon_WithoutBackend_DoesNotGrantLocally()
        {
            LootId trainingSword = new("training_short_sword");
            _canMutate = true;

            Assert.That(await _endpoint.RequestTrainingWeaponAsync(trainingSword, EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(StashOperationResult.PersistenceFailed));
            Assert.That(_service.AssignmentCalls, Is.Zero);
        }

        [Test]
        public async System.Threading.Tasks.Task ReadyGate_BlocksRequestTrainingWeapon_WhenCanMutateIsFalse()
        {
            LootId trainingSword = new("training_short_sword");
            _canMutate = false;

            Assert.That(await _endpoint.RequestTrainingWeaponAsync(trainingSword),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(_service.AssignmentCalls, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async System.Threading.Tasks.Task RequestTrainingWeapon_NotifiesOnlyAfterSuccessfulCommit(bool success)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(Profile, _catalog), Is.True);
            var snapshot = repository.Snapshot.Clone();
            snapshot.InventorySchemaVersion = 3;
            Assert.That(repository.TrySave(snapshot, out _), Is.True);
            var store = new LocalProfileStore(repository, Profile, _catalog);
            int notifications = 0;
            var endpoint = new TownEquipmentMutationEndpoint(_service, Profile, _catalog, () => true, () => notifications++);
            endpoint.BindTrainingBackend(store, request =>
            {
                Assert.That(request.expectedRevision, Is.Zero);
                Assert.That(request.weaponId, Is.EqualTo("training_dagger"));
                Assert.That(request.destinationSlot, Is.EqualTo("weaponSlot1"));
                var equipment = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
                    new LootId("training_dagger"), new EquipmentInstance("111111111111111111111111",
                        EquipmentInstance.PlainCache(GetDefinition("training_dagger")).Modifiers));
                return System.Threading.Tasks.Task.FromResult((success, new Grimhold.Backend.TrainingWeaponResult
                {
                    stash = Array.Empty<Grimhold.Backend.InventoryItemData>(),
                    loadout = Array.Empty<Grimhold.Backend.InventoryItemData>(), revision = 1,
                    preparedEquipment = Grimhold.Backend.PreparedEquipmentData.FromLoadout(equipment)
                }, new Grimhold.Backend.BackendError { error = "REVISION_CONFLICT" }));
            });
            var result = await endpoint.RequestTrainingWeaponAsync(new LootId("training_dagger"), EquipmentSlot.WeaponSetAMainHand);
            Assert.That(result, Is.EqualTo(success ? StashOperationResult.Success : StashOperationResult.InvalidInventory));
            Assert.That(notifications, Is.EqualTo(success ? 1 : 0));
            Assert.That(store.RemoteRevision, Is.EqualTo(success ? 1 : 0));
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand.IsValid, Is.EqualTo(success));
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetStash(), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async System.Threading.Tasks.Task ReservationPending_RejectsWithoutChangingStateOrRevision(bool localReservation)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(Profile, _catalog), Is.True);
            var snapshot = repository.Snapshot.Clone();
            snapshot.InventorySchemaVersion = 3;
            snapshot.RemoteRevision = 7;
            snapshot.PreparedEquipment = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
                new LootId("arming_sword"), new EquipmentInstance("333333333333333333333333",
                    EquipmentInstance.PlainCache(GetDefinition("arming_sword")).Modifiers));
            if (localReservation) snapshot.PendingReservation = new PendingLoadoutReservation("reserved", Array.Empty<StashItem>());
            Assert.That(repository.TrySave(snapshot, out _), Is.True);
            var store = new LocalProfileStore(repository, Profile, _catalog);
            int calls = 0;
            int commits = 0;
            int notifications = 0;
            store.ProfileCommitted += _ => commits++;
            var endpoint = new TownEquipmentMutationEndpoint(_service, Profile, _catalog, () => true, () => notifications++);
            endpoint.BindTrainingBackend(store, request =>
            {
                calls++;
                Assert.That(request.expectedRevision, Is.EqualTo(7));
                return System.Threading.Tasks.Task.FromResult((false, default(Grimhold.Backend.TrainingWeaponResult),
                    new Grimhold.Backend.BackendError { error = "RESERVATION_PENDING", message = "Reservation pending." }));
            });
            Assert.That(await endpoint.RequestTrainingWeaponAsync(new LootId("training_dagger"), EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(StashOperationResult.ReservationPending));
            Assert.That(calls, Is.EqualTo(localReservation ? 0 : 1));
            Assert.That(commits, Is.Zero);
            Assert.That(notifications, Is.Zero);
            Assert.That(endpoint.IsTrainingWeaponRequestInFlight, Is.False);
            Assert.That(store.RemoteRevision, Is.EqualTo(7));
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.GetPreparedEquipment(), Is.EqualTo(snapshot.PreparedEquipment));
            Assert.That(store.PendingReservation?.ReservationId, Is.EqualTo(localReservation ? "reserved" : null));
        }

        [TestCase(false, EquipmentSlot.Helmet)]
        [TestCase(true, EquipmentSlot.WeaponSetAMainHand)]
        public async System.Threading.Tasks.Task TrainingRequest_RejectsHydrationAndIncompatibleSlotWithoutCallingBackend(bool requiresHydration, EquipmentSlot slot)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(Profile, _catalog), Is.True);
            var snapshot = repository.Snapshot.Clone();
            snapshot.InventorySchemaVersion = 3;
            snapshot.RequiresEquipmentHydration = requiresHydration;
            Assert.That(repository.TrySave(snapshot, out _), Is.True);
            var store = new LocalProfileStore(repository, Profile, _catalog);
            int calls = 0;
            int commits = 0;
            store.ProfileCommitted += _ => commits++;
            _endpoint.BindTrainingBackend(store, request =>
            {
                calls++;
                return System.Threading.Tasks.Task.FromResult((false, default(Grimhold.Backend.TrainingWeaponResult), default(Grimhold.Backend.BackendError)));
            });
            Assert.That(await _endpoint.RequestTrainingWeaponAsync(new LootId("training_dagger"), slot), Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(calls, Is.Zero);
            Assert.That(commits, Is.Zero);
            Assert.That(store.RemoteRevision, Is.Zero);
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand.IsValid, Is.False);
        }

        [TestCase("INVALID_INVENTORY")]
        [TestCase("")]
        [TestCase("REVISION_CONFLICT")]
        [TestCase("TIMEOUT")]
        [TestCase("NETWORK_ERROR")]
        [TestCase("exception")]
        public async System.Threading.Tasks.Task LostAcknowledgement_LeavesCacheUntouchedAndRehydratesWithoutRetry(string failure)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(Profile, _catalog), Is.True);
            var snapshot = repository.Snapshot.Clone();
            snapshot.InventorySchemaVersion = 3;
            Assert.That(repository.TrySave(snapshot, out _), Is.True);
            var store = new LocalProfileStore(repository, Profile, _catalog);
            int posts = 0, refreshes = 0, notifications = 0, commits = 0;
            var pending = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var endpoint = new TownEquipmentMutationEndpoint(_service, Profile, _catalog, () => true, () => notifications++);
            store.ProfileCommitted += _ => commits++;
            endpoint.BindTrainingBackend(store, request =>
            {
                posts++;
                if (failure == "exception") throw new TimeoutException("Lost response.");
                return System.Threading.Tasks.Task.FromResult((false, default(Grimhold.Backend.TrainingWeaponResult),
                    new Grimhold.Backend.BackendError { error = failure }));
            }, () => { refreshes++; return pending.Task; });
            var requestTask = endpoint.RequestTrainingWeaponAsync(new LootId("training_dagger"), EquipmentSlot.WeaponSetAMainHand);
            Assert.That(refreshes, Is.EqualTo(1), "An unknown backend outcome must request authoritative reconciliation.");
            Assert.That(store.RemoteRevision, Is.Zero);
            Assert.That(store.GetPreparedEquipment().WeaponSetAMainHand.IsValid, Is.False);
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(commits, Is.Zero);
            Assert.That(notifications, Is.Zero);
            Assert.That(endpoint.IsTrainingWeaponRequestInFlight, Is.True);
            Assert.That(requestTask.IsCompleted, Is.False);
            Assert.That(await endpoint.RequestTrainingWeaponAsync(new LootId("training_dagger")),
                Is.EqualTo(StashOperationResult.InvalidInventory));
            Assert.That(posts, Is.EqualTo(1), "The busy gate must reject a second POST during reconciliation.");
            var prepared = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
                new LootId("training_dagger"), new EquipmentInstance("111111111111111111111111",
                    EquipmentInstance.PlainCache(GetDefinition("training_dagger")).Modifiers));
            Assert.That(store.ReconcileRemoteState(new Grimhold.Backend.InventoryData
            {
                inventorySchemaVersion = 3, revision = 1,
                preparedEquipment = Grimhold.Backend.PreparedEquipmentData.FromLoadout(prepared),
                stash = Array.Empty<Grimhold.Backend.InventoryItemData>(), loadout = Array.Empty<Grimhold.Backend.InventoryItemData>()
            }, null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
            pending.SetResult(true);
            Assert.That(await requestTask, Is.EqualTo(failure == "REVISION_CONFLICT"
                ? StashOperationResult.InvalidInventory : StashOperationResult.PersistenceFailed));
            Assert.That(posts, Is.EqualTo(1));
            Assert.That(refreshes, Is.EqualTo(1));
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(notifications, Is.Zero);
            Assert.That(store.RemoteRevision, Is.EqualTo(1));
            Assert.That(store.GetPreparedEquipment(), Is.EqualTo(prepared));
            Assert.That(endpoint.IsTrainingWeaponRequestInFlight, Is.False);
            if (failure == "REVISION_CONFLICT")
            {
                endpoint.BindTrainingBackend(store, request =>
                {
                    posts++;
                    Assert.That(request.expectedRevision, Is.EqualTo(1), "A new request must use the reconciled revision.");
                    return System.Threading.Tasks.Task.FromResult((true, new Grimhold.Backend.TrainingWeaponResult
                    {
                        revision = 2,
                        preparedEquipment = Grimhold.Backend.PreparedEquipmentData.FromLoadout(prepared),
                        stash = Array.Empty<Grimhold.Backend.InventoryItemData>(),
                        loadout = Array.Empty<Grimhold.Backend.InventoryItemData>()
                    }, default(Grimhold.Backend.BackendError)));
                }, () => { refreshes++; return System.Threading.Tasks.Task.FromResult(true); });
                Assert.That(await endpoint.RequestTrainingWeaponAsync(new LootId("training_dagger"), EquipmentSlot.WeaponSetAMainHand),
                    Is.EqualTo(StashOperationResult.Success));
                Assert.That(posts, Is.EqualTo(2));
                Assert.That(refreshes, Is.EqualTo(1));
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(store.RemoteRevision, Is.EqualTo(2));
            }
        }

        [TestCase("RESERVATION_PENDING", StashOperationResult.ReservationPending)]
        [TestCase("INVENTORY_FULL", StashOperationResult.InventoryFull)]
        [TestCase("VALIDATION_FAILED", StashOperationResult.InvalidInventory)]
        [TestCase("INVENTORY_MIGRATION_REQUIRED", StashOperationResult.InvalidInventory)]
        [TestCase("TOO_MANY_REQUESTS", StashOperationResult.PersistenceFailed)]
        public async System.Threading.Tasks.Task RejectedCodes_DoNotReconcile(string code, StashOperationResult expected)
        {
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(Profile, _catalog), Is.True);
            var snapshot = repository.Snapshot.Clone();
            snapshot.InventorySchemaVersion = 3;
            snapshot.RemoteRevision = 7;
            snapshot.PreparedEquipment = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
                new LootId("arming_sword"), new EquipmentInstance("333333333333333333333333",
                    EquipmentInstance.PlainCache(GetDefinition("arming_sword")).Modifiers));
            Assert.That(repository.TrySave(snapshot, out _), Is.True);
            var store = new LocalProfileStore(repository, Profile, _catalog);
            int posts = 0, refreshes = 0, notifications = 0, commits = 0;
            store.ProfileCommitted += _ => commits++;
            var endpoint = new TownEquipmentMutationEndpoint(_service, Profile, _catalog, () => true, () => notifications++);
            endpoint.BindTrainingBackend(store, request =>
            {
                posts++;
                Assert.That(request.expectedRevision, Is.EqualTo(7));
                return System.Threading.Tasks.Task.FromResult((false, default(Grimhold.Backend.TrainingWeaponResult),
                    new Grimhold.Backend.BackendError { error = code }));
            }, () => { refreshes++; return System.Threading.Tasks.Task.FromResult(true); });

            Assert.That(await endpoint.RequestTrainingWeaponAsync(new LootId("training_dagger"), EquipmentSlot.WeaponSetAMainHand),
                Is.EqualTo(expected));
            Assert.That(refreshes, Is.Zero);
            Assert.That(posts, Is.EqualTo(1));
            Assert.That(notifications, Is.Zero);
            Assert.That(commits, Is.Zero);
            Assert.That(store.RemoteRevision, Is.EqualTo(7));
            Assert.That(store.GetPreparedEquipment(), Is.EqualTo(snapshot.PreparedEquipment));
            Assert.That(store.GetLoadout(), Is.Empty);
            Assert.That(store.GetStash(), Is.Empty);
            Assert.That(store.PendingReservation, Is.Null);
            Assert.That(endpoint.IsTrainingWeaponRequestInFlight, Is.False);
        }

        private LootDefinition GetDefinition(string id)
        {
            Assert.That(_catalog.TryGet(id, out var definition), Is.True);
            return definition;
        }

        private sealed class FakeLoadoutService : IPlayerLoadoutService
        {
            public StashOperationResult TryOrder(ProfileId profileId, InventoryList list, IReadOnlyList<LootId> lootIds) => StashOperationResult.InvalidInventory;
            public StashOperationResult EvaluateOrder(ProfileId profileId, InventoryList list, IReadOnlyList<LootId> lootIds) => StashOperationResult.InvalidInventory;
            public IReadOnlyList<StashItem> Loadout { get; set; } = Array.Empty<StashItem>();
            public PreparedEquipmentLoadout PreparedEquipment { get; private set; }
            public EquipmentSlot LastAssignedSlot { get; private set; }
            public EquipmentSlot LastClearedSlot { get; private set; }
            public int AssignmentCalls { get; private set; }
            public int ClearCalls { get; private set; }

            public event Action<ProfileId> LoadoutChanged
            {
                add { }
                remove { }
            }

            public IReadOnlyList<StashItem> GetLoadout(ProfileId profileId) => Loadout;
            public PreparedEquipmentLoadout GetPreparedEquipment(ProfileId profileId) => PreparedEquipment;

            public StashOperationResult TryAssignPreparedEquipment(
                ProfileId profileId,
                EquipmentSlot slot,
                LootId lootId)
            {
                AssignmentCalls++;
                LastAssignedSlot = slot;
                PreparedEquipment = PreparedEquipment.With(slot, lootId);
                return StashOperationResult.Success;
            }

            public StashOperationResult TryClearPreparedEquipment(ProfileId profileId, EquipmentSlot slot)
            {
                ClearCalls++;
                LastClearedSlot = slot;
                PreparedEquipment = PreparedEquipment.Without(slot);
                return StashOperationResult.Success;
            }

            public StashOperationResult TryEquipFromStash(ProfileId profileId, LootId lootId, EquipmentSlot slot) => StashOperationResult.InvalidInventory;
            public StashOperationResult EvaluateEquipFromStash(ProfileId profileId, LootId lootId, EquipmentSlot slot, out PreparedEquipmentLoadout equipment)
            {
                equipment = default;
                return StashOperationResult.InvalidInventory;
            }

            public ExpeditionPreparationResult TryPrepareExpeditionLoadout(ProfileId profileId) =>
                ExpeditionPreparationResult.ProfileUnavailable;
            public StashOperationResult TryTransferToLoadout(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryTransferToStash(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryTransferAllToLoadout(ProfileId profileId) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryTransferAllToStash(ProfileId profileId) => StashOperationResult.InvalidInventory;
            public StashOperationResult EvaluateTransferToLoadout(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
            public StashOperationResult EvaluateTransferToStash(ProfileId profileId, LootId lootId, int amount) => StashOperationResult.InvalidInventory;
            public StashOperationResult EvaluateTransferAllToLoadout(ProfileId profileId) => StashOperationResult.InvalidInventory;
            public StashOperationResult EvaluateTransferAllToStash(ProfileId profileId) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryImportItems(ProfileId profileId, IReadOnlyList<StashItem> items) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryCreateLoadoutReservation(ProfileId profileId, string reservationId, out PendingLoadoutReservation reservation)
            {
                reservation = null;
                return StashOperationResult.InvalidInventory;
            }
            public StashOperationResult TryConfirmLoadoutReservation(ProfileId profileId, string reservationId) => StashOperationResult.InvalidInventory;
            public StashOperationResult TryRollbackLoadoutReservation(ProfileId profileId, string reservationId) => StashOperationResult.InvalidInventory;
        }
    }
}
#endif
