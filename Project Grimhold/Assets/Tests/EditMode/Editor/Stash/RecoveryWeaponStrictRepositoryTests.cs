#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RecoveryWeaponStrictRepositoryTests
{
    private static readonly ProfileId Profile = new("56565656565656565656565656565656");
    private static readonly LootId Weapon = new("arming_sword");
    private LootDefinitionCatalog _catalog;
    private ProfileId _previousProfile;
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        Assert.That(_catalog, Is.Not.Null);
        _previousProfile = LocalProfileProvider.GetOrCreateLocalProfile();
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        LocalProfileProvider.SetRemoteCharacterId(_previousProfile);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OwnedRecovery_PreservesLowestSlotInstanceAndOtherUnits(bool stashOnly)
    {
        var files = new MemoryFiles();
        LocalProfileStore store = CreateStore(files);
        var low = Item(2, "111111111111111111111111");
        var high = Item(9, "222222222222222222222222");
        var stash = Item(0, "333333333333333333333333");
        InventoryData inventory = Inventory();
        inventory.loadout = stashOnly ? Array.Empty<InventoryItemData>() : new[] { high, low };
        inventory.stash = stashOnly ? new[] { high, low, stash } : new[] { stash };
        Assert.That(store.ReconcileRemoteState(inventory, null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
        int writes = files.Writes, events = 0;
        store.ProfileCommitted += _ => events++;
        store.InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation preparation);
        // No log is expected after the correction; suppress only the known original failure for RED.
        LogAssert.ignoreFailingMessages = true;
        try
        {
            Assert.That(store.TryPrepareExpeditionEquipment(preparation), Is.EqualTo(ExpeditionPreparationResult.Success));
        }
        finally { LogAssert.ignoreFailingMessages = false; }
        string expected = stashOnly ? stash.instance.instanceId : low.instance.instanceId;
        var beforeInstance = stashOnly ? stash.instance : low.instance;
        Assert.That(beforeInstance.TryToInstance(GetWeapon(), true, out var exactInstance, out _, _catalog), Is.True);
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(exactInstance));
        Assert.That(store.GetStash()[store.GetStash().Count - 1].Instance.InstanceId,
            Is.EqualTo(stashOnly ? high.instance.instanceId : stash.instance.instanceId));
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId, Is.EqualTo(expected));
        Assert.That(store.GetLoadout().Count + store.GetStash().Count, Is.EqualTo(2));
        Assert.That(files.Writes, Is.EqualTo(writes + 1));
        Assert.That(events, Is.EqualTo(1));
        Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(files.Writes, Is.EqualTo(writes + 1));
        var reloaded = new LocalProfileRepository(files, ".");
        Assert.That(reloaded.Initialize(Profile, _catalog), Is.True, reloaded.LastError);
        Assert.That(reloaded.Snapshot.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId, Is.EqualTo(expected));
    }

    [TestCase("admitted-return")]
    [TestCase("pending-town-entry")]
    [TestCase("success")]
    [TestCase("recover")]
    [TestCase("startup-failure")]
    [TestCase("cancel-during-post")]
    [TestCase("ambiguous-rollback")]
    [TestCase("newer-reservation")]
    [TestCase("local-drift")]
    [TestCase("cancel")]
    [TestCase("disable")]
    [TestCase("launch-drift")]
    [TestCase("consent-drift")]
    public async Task ConsentedStrictGrant_CoordinatorDoesNotCommitOrPublishTicketSynchronously(string outcome)
    {
        var files = new MemoryFiles();
        LocalProfileStore store = CreateStore(files);
        Assert.That(store.ReconcileRemoteState(Inventory(), null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
        store.InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation consent);
        LocalProfileProvider.SetRemoteCharacterId(Profile);
        _root = new GameObject("Recovery isolated coordinator");
        var context = _root.AddComponent<ApplicationStashContext>();
        var service = _root.AddComponent<InMemoryPlayerLoadoutService>();
        service.Initialize(store);
        context.Initialize(store, null, service, null, null);
        var remote = _root.AddComponent<RecoveryWeaponFakeAuthority>();
        var coordinator = _root.AddComponent<SessionConnectionCoordinator>();
        var machine = (SessionConnectionStateMachine)Field("_stateMachine").GetValue(coordinator);
        Assert.That(machine.TryTransition(SessionConnectionState.ConnectingTown), Is.True);
        Assert.That(machine.TryTransition(SessionConnectionState.Town), Is.True);
        RaidCode.TryParse("123456", out RaidCode code);
        Field("_recoveryStashContext").SetValue(coordinator, context);
        Field("_recoveryConsentRaidCode").SetValue(coordinator, code);
        Field("_approvedRecoveryPreparation").SetValue(coordinator, consent);
        RaidTeamId.TryCreate(1, out var team);
        RaidLaunchContext.TryCreate(code, Profile, new[] { new RaidLaunchParticipant(Profile, team) }, Profile, 1, out var launch);
        TownPartyContinuationContext.TryCreate(Profile, new[] { Profile }, out var party);
        int writes = files.Writes, events = 0;
        store.ProfileCommitted += _ => events++;
        LogAssert.ignoreFailingMessages = true;
        RaidLaunchPreparationResult result;
        try { result = coordinator.TryStoreRaidLaunchContext(launch, party); }
        finally { LogAssert.ignoreFailingMessages = false; }
        Assert.That(result, Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(coordinator.ActiveTicket, Is.Null);
        Assert.That(store.PendingReservation, Is.Null);
        Assert.That(files.Writes, Is.EqualTo(writes));
        Assert.That(events, Is.Zero);
        Assert.That(coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(remote.Requests, Is.EqualTo(1));
        var controller = _root.AddComponent<TownRaidPreparationNetworkController>();
        var snapshot = new TownRaidPreparationSnapshot(code, Profile, TownRaidPreparationState.Launching,
            new[] { new TownRaidPreparationMember(Profile, true, 1) }, 4, 1, 1);
        int acknowledgements = 0;
        controller.AdvanceLocalLaunch(snapshot, Profile, coordinator, party, _ => acknowledgements++);
        if (outcome == "cancel-during-post") coordinator.CancelPendingRaidLaunch(code.Value, 1);
        remote.Reservation.SetResult((true, Response(remote.ReservationId), default));
        if (outcome != "cancel-during-post") await remote.ReadStarted.Task;
        Assert.That(files.Writes, Is.EqualTo(writes));
        Assert.That(coordinator.ActiveTicket, Is.Null);
        if (outcome == "cancel") coordinator.CancelPendingRaidLaunch(code.Value, 1);
        if (outcome == "disable")
        {
            coordinator.enabled = false;
            // EditMode does not dispatch lifecycle messages; exercise the production lifecycle policy.
            coordinator.SuspendRecoveryWeaponPreparation();
        }
        if (outcome == "consent-drift") coordinator.CancelRecoveryWeaponConsent(code, 0);
        if (outcome == "launch-drift")
        {
            RaidLaunchContext.TryCreate(code, Profile, new[] { new RaidLaunchParticipant(Profile, team) }, Profile, 2, out var newer);
            Assert.That(coordinator.TryStoreRaidLaunchContext(newer, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        }
        var authority = ReservedState(remote.ReservationId);
        var spare = Item(4, "555555555555555555555555");
        if (outcome == "admitted-return")
        {
            var inventoryWithSpare = authority.Inventory;
            inventoryWithSpare.stash = new[] { spare };
            authority = new RecoveryWeaponAuthorityState(inventoryWithSpare, authority.Progression);
        }
        remote.Authority.SetResult(authority);
        if (outcome != "admitted-return" && outcome != "pending-town-entry" && outcome != "success" && outcome != "recover" && outcome != "startup-failure" &&
            outcome != "ambiguous-rollback" && outcome != "newer-reservation" && outcome != "local-drift")
        {
            await (Task)Field("_participationOutcomeTask").GetValue(coordinator);
            Assert.That(coordinator.ActiveTicket, Is.Null);
            Assert.That(store.PendingReservation, Is.Null);
            Assert.That(files.Writes, Is.EqualTo(writes + 1));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(remote.RolledBackId, Is.EqualTo(remote.ReservationId));
            Assert.That(PreparedEquipmentLoadout.HasUsableWeapon(store.GetPreparedEquipment(), _catalog), Is.True);
            Assert.That(remote.Requests, Is.EqualTo(1));
            return;
        }
        for (int i = 0; i < 100 && !coordinator.ActiveTicket.HasValue; i++)
        {
            await Task.Yield();
            if (outcome == "success" || outcome == "admitted-return" || outcome == "pending-town-entry")
                controller.AdvanceLocalLaunch(snapshot, Profile, coordinator, party, _ => acknowledgements++);
            else coordinator.TryStoreRaidLaunchContext(launch, party);
        }
        Assert.That(coordinator.ActiveTicket.HasValue, Is.True);
        if (outcome == "success")
        {
            controller.AdvanceLocalLaunch(snapshot, Profile, coordinator, party, _ => acknowledgements++);
            Assert.That(acknowledgements, Is.EqualTo(1));
        }
        Assert.That(store.PendingReservation.ReservationId, Is.EqualTo(remote.ReservationId));
        Assert.That(store.PendingReservation.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId,
            Is.EqualTo("444444444444444444444444"));
        Assert.That(files.Writes, Is.EqualTo(writes + 1));
        Assert.That(events, Is.EqualTo(1));
        Assert.That(remote.Requests, Is.EqualTo(1));
        Assert.That(coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        if (outcome == "admitted-return")
        {
            // Invoke the actual local admission-consumption and completed Town-entry boundaries.
            // Runner transport is deliberately absent; no recovery-stage fields are cleared here.
            Assert.That(typeof(SessionConnectionCoordinator).GetMethod("TryConfirmActiveReservation",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(coordinator, null), Is.EqualTo(true));
            Assert.That(store.PendingReservation, Is.Null);
            Assert.That(store.GetPreparedEquipment().HasAnyWeapon, Is.False);
            typeof(SessionConnectionCoordinator).GetMethod("CompleteTownEntry",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(coordinator, new object[] { null });
            Assert.That(coordinator.ActiveTicket, Is.Null);
            Assert.That(store.GetStash().Count, Is.EqualTo(1));
            Assert.That(spare.instance.TryToInstance(GetWeapon(), true, out var originalSpare, out _, _catalog), Is.True);
            RaidLaunchContext.TryCreate(code, Profile, new[] { new RaidLaunchParticipant(Profile, team) }, Profile, 2, out var next);
            Assert.That(coordinator.TryStoreRaidLaunchContext(next, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
            Assert.That(coordinator.HasActiveLaunchTicket(code.Value, 2, Profile), Is.True);
            Assert.That(store.PendingReservation.ReservationId, Is.Not.EqualTo(remote.ReservationId));
            Assert.That(store.PendingReservation.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand), Is.EqualTo(originalSpare));
            Assert.That(store.PendingReservation.Items.Count, Is.Zero);
            Assert.That(store.GetStash().Count + store.GetLoadout().Count, Is.Zero);
            Assert.That(remote.Requests, Is.EqualTo(1));
            Assert.That(remote.RollbackRequests, Is.Zero, "Admitted ownership must never be rolled back.");
            Assert.That(acknowledgements, Is.EqualTo(1));
            return;
        }
        if (outcome == "pending-town-entry")
        {
            typeof(SessionConnectionCoordinator).GetMethod("CompleteTownEntry",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(coordinator, new object[] { null });
            Assert.That(coordinator.ActiveTicket, Is.Null);
            Assert.That(store.PendingReservation.ReservationId, Is.EqualTo(remote.ReservationId));
            Assert.That(await coordinator.RecoverPendingRecoveryWeaponAsync(), Is.True,
                "Town entry must not discard an unconsumed stage and its guarded restoration.");
            Assert.That(remote.RolledBackId, Is.EqualTo(remote.ReservationId));
            Assert.That(store.PendingReservation, Is.Null);
            Assert.That(remote.Requests, Is.EqualTo(1));
            return;
        }
        if (outcome == "startup-failure")
        {
            int townReplacements = 0;
            var resultAfterFailure = await coordinator.RecoverFailedRecoveryWeaponStartAsync(() =>
            {
                Assert.That(store.PendingReservation, Is.Null);
                townReplacements++;
                return Task.FromResult(SessionTransitionResult.Succeeded);
            });
            Assert.That(resultAfterFailure, Is.EqualTo(SessionTransitionResult.Succeeded));
            Assert.That(townReplacements, Is.EqualTo(1));
        }
        if (outcome == "local-drift")
            Assert.That(store.TryCreditCurrency(7), Is.EqualTo(StashOperationResult.Success));
        remote.RefuseRollback = outcome == "newer-reservation";
        remote.LoseRollbackResponse = outcome == "ambiguous-rollback";
        coordinator.CancelPendingRaidLaunch(code.Value, 1);
        Assert.That(coordinator.ActiveTicket, Is.Null);
        await (Task)Field("_participationOutcomeTask").GetValue(coordinator);
        if (outcome == "local-drift")
        {
            Assert.That(store.GetCurrency(), Is.EqualTo(17), "Cleanup cannot overwrite a newer local aggregate.");
            Assert.That(store.PendingReservation.ReservationId, Is.EqualTo(remote.ReservationId));
            Assert.That(remote.RolledBackId, Is.Null);
            Assert.That(files.Writes, Is.EqualTo(writes + 2));
            Assert.That(events, Is.EqualTo(2));
            return;
        }
        if (outcome == "newer-reservation")
        {
            Assert.That(store.PendingReservation.ReservationId, Is.EqualTo(remote.ReservationId));
            Assert.That(remote.RolledBackId, Is.Null);
            Assert.That(files.Writes, Is.EqualTo(writes + 1));
            Assert.That(events, Is.EqualTo(1));
            return;
        }
        Assert.That(store.PendingReservation, Is.Null, "Cancellation must restore usable Town ownership.");
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId,
            Is.EqualTo("444444444444444444444444"));
        RaidLaunchContext.TryCreate(code, Profile, new[] { new RaidLaunchParticipant(Profile, team) }, Profile, 2, out var nextLaunch);
        Assert.That(coordinator.TryStoreRaidLaunchContext(nextLaunch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
    }

    [TestCase("cancel")]
    [TestCase("launch-drift")]
    [TestCase("store-drift")]
    [TestCase("wrong-response")]
    [TestCase("malformed-response")]
    [TestCase("wrong-authority")]
    [TestCase("missing-instance")]
    [TestCase("revision-mismatch")]
    [TestCase("lost-unconfirmed")]
    [TestCase("write-failure")]
    public async Task AsyncGrant_FailuresNeverPublishOwnership(string failure)
    {
        var files = new MemoryFiles();
        LocalProfileStore store = CreateStore(files);
        Assert.That(store.ReconcileRemoteState(Inventory(), null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
        store.InspectRecoveryWeaponPreparation(out var consent);
        _root = new GameObject("Recovery fake transport");
        var remote = _root.AddComponent<RecoveryWeaponFakeAuthority>();
        bool launchCurrent = true;
        var stage = new RecoveryWeaponReservationPreparation(store, consent, () => launchCurrent);
        Task<ExpeditionPreparationResult> completion = stage.Start(remote, remote);
        Assert.That(stage.Start(remote, remote), Is.SameAs(completion));
        Assert.That(completion.IsCompleted, Is.False);
        if (failure == "cancel") stage.Cancel();
        if (failure == "launch-drift") launchCurrent = false;
        if (failure == "store-drift") store.SetRemoteRevision(store.RemoteRevision);
        int writes = files.Writes, notifications = 0;
        store.ProfileCommitted += _ => notifications++;
        RecoveryWeaponAuthorityState state = ReservedState(stage.ReservationId);
        InventoryData inv = state.Inventory;
        ProgressionData prog = state.Progression;
        if (failure == "wrong-authority") inv.pendingReservation.reservationId = "another-reservation";
        if (failure == "missing-instance") inv.pendingReservation.preparedEquipment.instances = null;
        if (failure == "revision-mismatch") prog.revision++;
        if (failure == "lost-unconfirmed") inv.pendingReservation = default;
        remote.Authority.SetResult(new RecoveryWeaponAuthorityState(inv, prog));
        if (failure == "write-failure") files.RejectWrites = true;
        LogAssert.ignoreFailingMessages = true;
        try
        {
            var response = Response(failure == "wrong-response" ? "wrong" : stage.ReservationId);
            if (failure == "malformed-response") response.pendingReservation.preparedEquipment.instances = null;
            remote.Reservation.SetResult((failure != "lost-unconfirmed", response, default));
            Assert.That(await completion, Is.Not.EqualTo(ExpeditionPreparationResult.Success));
        }
        finally { LogAssert.ignoreFailingMessages = false; }
        Assert.That(files.Writes, Is.EqualTo(writes));
        Assert.That(notifications, Is.Zero);
        Assert.That(store.PendingReservation, Is.Null);
        Assert.That(store.GetPreparedEquipment().HasAnyWeapon, Is.False);
        Assert.That(store.RemoteRevision, Is.EqualTo(7));
        Assert.That(store.GetCurrency(), Is.EqualTo(10));
        Assert.That(remote.Requests, Is.EqualTo(1));
        Assert.That(stage.Start(remote, remote), Is.SameAs(completion));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AmbiguousResponse_ConfirmsOnlySameIdFullAuthorityWithoutAnotherPost(bool throws)
    {
        var files = new MemoryFiles();
        LocalProfileStore store = CreateStore(files);
        Assert.That(store.ReconcileRemoteState(Inventory(), null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
        store.InspectRecoveryWeaponPreparation(out var consent);
        _root = new GameObject("Recovery lost response");
        var remote = _root.AddComponent<RecoveryWeaponFakeAuthority>();
        var stage = new RecoveryWeaponReservationPreparation(store, consent, () => true);
        var completion = stage.Start(remote, remote);
        remote.Authority.SetResult(ReservedState(stage.ReservationId));
        if (throws) remote.Reservation.SetException(new InvalidOperationException("Lost response"));
        else remote.Reservation.SetResult((false, default, default));
        Assert.That(await completion, Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(remote.Requests, Is.EqualTo(1));
        Assert.That(store.PendingReservation.ReservationId, Is.EqualTo(stage.ReservationId));
    }

    [Test]
    public void ExistingPreparedWeapon_AndOrdinaryFullReconciliationRemainIdempotent()
    {
        var files = new MemoryFiles();
        LocalProfileStore store = CreateStore(files);
        InventoryData inventory = Inventory();
        inventory.preparedEquipment = ReservedState("not-applied").Inventory.pendingReservation.preparedEquipment;
        inventory.stash = new[] { Item(4, "555555555555555555555555") };
        int notifications = 0;
        store.ProfileCommitted += _ => notifications++;
        Assert.That(store.ReconcileRemoteState(inventory, null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
        int writes = files.Writes;
        Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
        store.InspectRecoveryWeaponPreparation(out var inspection);
        Assert.That(inspection.RequiresConsent, Is.False);
        Assert.That(store.TryPrepareExpeditionEquipment(inspection), Is.EqualTo(ExpeditionPreparationResult.Success));
        Assert.That(store.GetStash()[0].Instance.InstanceId, Is.EqualTo("555555555555555555555555"));
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId,
            Is.EqualTo("444444444444444444444444"));
        Assert.That(store.RemoteRevision, Is.EqualTo(7));
        Assert.That(files.Writes, Is.EqualTo(writes));
        Assert.That(notifications, Is.EqualTo(1));
    }

    [Test]
    public async Task ExistingReconciliation_UninitializedServiceStillRejectsWithoutTransport()
    {
        _root = new GameObject("Uninitialized reconciliation");
        var service = _root.AddComponent<ProfileReconciliationService>();
        LogAssert.Expect(LogType.Error, "[ProfileReconciliationService] Not initialized or not authenticated.");
        Assert.That(await service.ReconcileAsync(), Is.False);
    }

    [Test]
    public async Task MissingBackend_FailsClosedWithoutSynchronousV3Grant()
    {
        var files = new MemoryFiles();
        LocalProfileStore store = CreateStore(files);
        Assert.That(store.ReconcileRemoteState(Inventory(), null, _catalog, null), Is.EqualTo(StashOperationResult.Success));
        store.InspectRecoveryWeaponPreparation(out var consent);
        int writes = files.Writes, notifications = 0;
        store.ProfileCommitted += _ => notifications++;
        var stage = new RecoveryWeaponReservationPreparation(store, consent, () => true);
        Assert.That(await stage.Start(null, null), Is.EqualTo(ExpeditionPreparationResult.ProfileUnavailable));
        Assert.That(store.TryPrepareExpeditionEquipment(consent), Is.EqualTo(ExpeditionPreparationResult.ProfileUnavailable));
        Assert.That(files.Writes, Is.EqualTo(writes));
        Assert.That(notifications, Is.Zero);
        Assert.That(store.PendingReservation, Is.Null);
    }

    private static RecoveryWeaponAuthorityState ReservedState(string reservationId)
    {
        InventoryData inventory = Inventory();
        inventory.revision = 8;
        var equipment = new PreparedEquipmentData();
        equipment.Set(EquipmentSlot.WeaponSetAMainHand, Weapon.Value,
            new EquipmentInstanceData { instanceId = "444444444444444444444444" }, true);
        inventory.pendingReservation = new PendingReservationData
        { reservationId = reservationId, items = Array.Empty<InventoryItemData>(), preparedEquipment = equipment };
        return new RecoveryWeaponAuthorityState(inventory, new ProgressionData
        {
            revision = 8, level = 1,
            characterAttributes = new CharacterAttributesData
            { vitality = 10, resistance = 10, strength = 10, dexterity = 10, intelligence = 10, luck = 10 }
        });
    }

    private static SaveReservationResult Response(string id) => new()
    { pendingReservation = ReservedState(id).Inventory.pendingReservation, revision = 8 };

    private LootDefinition GetWeapon()
    { Assert.That(_catalog.TryGet(Weapon.Value, out var definition), Is.True); return definition; }

    private static FieldInfo Field(string name) => typeof(SessionConnectionCoordinator).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    private LocalProfileStore CreateStore(MemoryFiles files)
    {
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(Profile, _catalog), Is.True, repository.LastError);
        return new LocalProfileStore(repository, Profile, _catalog, Weapon);
    }
    private static InventoryData Inventory() => new()
    {
        inventorySchemaVersion = 3, revision = 7, currency = 10,
        stash = Array.Empty<InventoryItemData>(), loadout = Array.Empty<InventoryItemData>(),
        preparedEquipment = new PreparedEquipmentData()
    };
    private static InventoryItemData Item(int slot, string id) => new()
    {
        slot = slot, lootId = Weapon.Value, amount = 1,
        instance = new EquipmentInstanceData { instanceId = id, modifiers = new WeaponInstanceModifiersData
        { gemSocketCount = 1, gems = new string[] { null } } }
    };
    private sealed class MemoryFiles : ILocalProfileFileStore
    {
        private readonly Dictionary<string, string> _files = new();
        public int Writes { get; private set; }
        public bool RejectWrites { get; set; }
        public bool Exists(string path) => _files.ContainsKey(path);
        public bool TryRead(string path, out string contents, out string error)
        { error = _files.TryGetValue(path, out contents) ? null : "Missing"; return error == null; }
        public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
        {
            if (RejectWrites) { error = "Injected write rejection"; return false; }
            if (_files.TryGetValue(mainPath, out var old)) _files[backupPath] = old;
            _files[mainPath] = contents; Writes++; error = null; return true;
        }
        public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
        { error = "Not used"; return false; }
    }
}

public sealed class RecoveryWeaponFakeAuthority : MonoBehaviour, IRecoveryWeaponReservationService, IRecoveryWeaponAuthorityReader
{
    public readonly TaskCompletionSource<(bool success, SaveReservationResult data, BackendError error)> Reservation = new();
    public readonly TaskCompletionSource<RecoveryWeaponAuthorityState> Authority = new();
    public readonly TaskCompletionSource<bool> ReadStarted = new();
    public int Requests { get; private set; }
    public string ReservationId { get; private set; }
    public string RolledBackId { get; private set; }
    public int RollbackRequests { get; private set; }
    public bool RefuseRollback { get; set; }
    public bool LoseRollbackResponse { get; set; }
    public async Task<(bool success, BackendError error)> RollbackRecoveryWeaponAsync(string expectedReservationId, Func<bool> isCurrent)
    {
        RollbackRequests++;
        await Authority.Task;
        if (!isCurrent() || RefuseRollback)
            return (false, new BackendError { error = "RESERVATION_MISMATCH" });
        RolledBackId = expectedReservationId;
        if (LoseRollbackResponse) throw new InvalidOperationException("Lost rollback response");
        return (true, default);
    }
    public Task<(bool success, SaveReservationResult data, BackendError error)> ReserveRecoveryWeaponAsync(string reservationId, Func<bool> isCurrent)
    { Requests++; ReservationId = reservationId; return Reservation.Task; }
    public async Task<RecoveryWeaponAuthorityState> FetchRecoveryWeaponAuthorityAsync()
    {
        ReadStarted.TrySetResult(true);
        var state = await Authority.Task;
        if (RolledBackId == null) return state;
        var inventory = state.Inventory;
        inventory.preparedEquipment = inventory.pendingReservation.preparedEquipment;
        inventory.loadout = inventory.pendingReservation.items;
        inventory.pendingReservation = default;
        inventory.revision++;
        var progression = state.Progression;
        progression.revision = inventory.revision;
        return new RecoveryWeaponAuthorityState(inventory, progression);
    }
}
#endif
