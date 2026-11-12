#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExpeditionWeaponRequirementTests
{
    private const string RequiredMessage = "Necesitás un arma equipada para salir de expedición. Pedile una al Entrenador.";
    private static readonly ProfileId Profile = new("56565656565656565656565656565656");
    private GameObject _root;
    private ProfileId _previousProfile;
    private LocalProfileStore _store;
    private SessionConnectionCoordinator _coordinator;
    private SessionConnectionCoordinator _previousCoordinator;
    private TownRaidPreparationView _view;

    [SetUp]
    public void SetUp()
    {
        _previousCoordinator = SessionConnectionCoordinator.Instance;
        _previousProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        LocalProfileProvider.SetRemoteCharacterId(Profile);
        var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        var repository = new InMemoryLocalProfileRepository();
        Assert.That(repository.Initialize(Profile, catalog), Is.True);
        _store = new LocalProfileStore(repository, Profile, catalog);
        _root = new GameObject(nameof(ExpeditionWeaponRequirementTests));
        _root.AddComponent<HubSessionLauncher>();
        _root.AddComponent<FusionSessionLauncher>();
        var service = _root.AddComponent<InMemoryPlayerLoadoutService>();
        service.Initialize(_store);
        var context = _root.AddComponent<ApplicationStashContext>();
        context.Initialize(_store, null, service, null, null);
        _coordinator = _root.AddComponent<SessionConnectionCoordinator>();
        // EditMode does not run the scene lifecycle; bind the existing application singleton seam.
        typeof(SessionConnectionCoordinator).GetField("<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _coordinator);
        var state = (SessionConnectionStateMachine)typeof(SessionConnectionCoordinator)
            .GetField("_stateMachine", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_coordinator);
        Assert.That(state.TryTransition(SessionConnectionState.ConnectingTown), Is.True);
        Assert.That(state.TryTransition(SessionConnectionState.Town), Is.True);
        _view = TownRaidPreparationView.CreateEditorSource(_root.transform);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        LocalProfileProvider.SetRemoteCharacterId(_previousProfile);
        typeof(SessionConnectionCoordinator).GetField("<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousCoordinator);
    }

    [Test]
    public void NoWeapon_ReadyAndLaunchAreBlockedWithoutReservationOrMutation()
    {
        int readyCalls = 0, commits = 0;
        _view.ReadyRequested += _ => readyCalls++;
        _store.ProfileCommitted += _ => commits++;
        _view.transform.Find("RaidCodePanel/Ready").GetComponent<Button>().onClick.Invoke();
        Assert.That(readyCalls, Is.Zero, "No Ready intention may reach the presenter/reservation path.");
        Assert.That(_view.transform.Find("RaidCodePanel/Status").GetComponent<TMP_Text>().text, Is.EqualTo(RequiredMessage));
        var (launch, party) = Launch();
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Rejected));
        Assert.That(_coordinator.TryConsumeLastLaunchRejection(out var reason), Is.True);
        Assert.That(reason, Is.EqualTo(ExpeditionPreparationResult.PreparedWeaponRequired));
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(commits, Is.Zero);
        Assert.That(_store.GetStash(), Is.Empty);
        Assert.That(_store.GetLoadout(), Is.Empty);
        Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
    }

    [TestCase("training_short_sword")]
    [TestCase("training_dagger")]
    [TestCase("training_wand")]
    [TestCase("arming_sword")]
    public void EquippedWeapon_ReadyAndReservationRetainTheExactSelection(string weapon)
    {
        var id = new LootId(weapon);
        Assert.That(_store.TryImportItems(new[] { new StashItem(id, 1) }), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, id), Is.EqualTo(StashOperationResult.Success));
        var selected = _store.GetPreparedEquipment();
        int readyCalls = 0;
        _view.ReadyRequested += ready => { Assert.That(ready, Is.True); readyCalls++; };
        _view.transform.Find("RaidCodePanel/Ready").GetComponent<Button>().onClick.Invoke();
        Assert.That(readyCalls, Is.EqualTo(1));
        var (launch, party) = Launch();
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_store.PendingReservation.PreparedEquipment, Is.EqualTo(selected));
        Assert.That(_coordinator.ActiveTicket.Value.LoadoutReservation.PreparedEquipment, Is.EqualTo(selected));
        Assert.That(_store.PendingReservation.Items, Is.Empty);
    }

    [Test]
    public async Task BackendWeaponRequired_IsNotRetriedAndUsesTheTrainerNotice()
    {
        int calls = 0;
        var error = new BackendError { error = "PREPARED_WEAPON_REQUIRED", message = "Equip a weapon in a Weapon Set or ask the Trainer before preparing an expedition." };
        var result = await RemoteIdempotentRetryPolicy.ExecuteWithRetryAsync(() =>
        {
            calls++;
            return Task.FromResult((false, error));
        });
        Assert.That(result.success, Is.False);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(result.error.error, Is.EqualTo("PREPARED_WEAPON_REQUIRED"));
        var reason = SessionConnectionCoordinator.ReservationFailureReason(result.error);
        Assert.That(reason, Is.EqualTo(ExpeditionPreparationResult.PreparedWeaponRequired));
        _view.ShowPreparationRejected(reason);
        Assert.That(_view.transform.Find("RaidCodePanel/Status").GetComponent<TMP_Text>().text, Is.EqualTo(RequiredMessage));
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(SessionConnectionCoordinator.ReservationFailureReason(new BackendError { error = "OTHER" }),
            Is.EqualTo(ExpeditionPreparationResult.ReservationFailed));
    }

    [Test]
    public async Task UninitializedReconciliation_StillRejectsWithoutTransport()
    {
        var service = _root.AddComponent<ProfileReconciliationService>();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
            "[ProfileReconciliationService] Not initialized or not authenticated.");
        Assert.That(await service.ReconcileAsync(), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReservationSave_PendingCannotAck_ThenSuccessPreservesHostOrClientRole(bool isHost)
    {
        var (launch, party, save) = PendingSave(isHost);
        var reservation = _store.PendingReservation;
        int commits = 0;
        _store.ProfileCommitted += _ => commits++;
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(_coordinator.ActiveTicket.Value.Request.Role,
            Is.EqualTo(isHost ? RaidConnectionRole.Host : RaidConnectionRole.Client));
        Assert.That(commits, Is.Zero);
        save.SetResult((true, default));
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo(reservation.ReservationId));
        Assert.That(_store.PendingReservation.PreparedEquipment, Is.EqualTo(reservation.PreparedEquipment));
        Assert.That(commits, Is.Zero, "Polling must not create a second reservation.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReservationSave_BackendRejectionRestoresEquipmentAndNeverAcks(bool isHost)
    {
        var (launch, party, save) = PendingSave(isHost);
        var selected = _store.PendingReservation.PreparedEquipment;
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        save.SetResult((false, new BackendError { error = "PREPARED_WEAPON_REQUIRED" }));
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Rejected));
        Assert.That(_coordinator.TryConsumeLastLaunchRejection(out var reason), Is.True);
        Assert.That(reason, Is.EqualTo(ExpeditionPreparationResult.PreparedWeaponRequired));
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(_store.GetPreparedEquipment(), Is.EqualTo(selected));
        Assert.That(_coordinator.TryGetPendingPartyContinuation(out _), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CancelWhileReservationSaveIsPending_LateCompletionCannotRestoreTicket(bool isHost)
    {
        var (launch, party, save) = PendingSave(isHost);
        var selected = _store.PendingReservation.PreparedEquipment;
        // A stale revision must not cancel the live launch.
        _coordinator.CancelPendingRaidLaunch(launch.RaidCode.Value, launch.LaunchRevision + 1);
        Assert.That(_coordinator.ActiveTicket, Is.Not.Null);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        _coordinator.CancelPendingRaidLaunch(launch.RaidCode.Value, launch.LaunchRevision);
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(_store.GetPreparedEquipment(), Is.EqualTo(selected));
        Assert.That(_coordinator.TryGetPendingPartyContinuation(out _), Is.False);
        int commits = 0;
        _store.ProfileCommitted += _ => commits++;
        save.SetResult((true, default));
        _coordinator.CancelPendingRaidLaunch(launch.RaidCode.Value, launch.LaunchRevision);
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(_store.GetPreparedEquipment(), Is.EqualTo(selected));
        Assert.That(commits, Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ControllerObservation_AcksExactlyOnceAndOnlyAfterReservationSave(bool isHost)
    {
        var (launch, party, save) = PendingSave(isHost);
        var controller = _root.AddComponent<TownRaidPreparationNetworkController>();
        var members = launch.Participants.Select(
            participant => new TownRaidPreparationMember(
                participant.ProfileId, true, launch.LaunchRevision, false, participant.TeamId.Value)).ToArray();
        var snapshot = new TownRaidPreparationSnapshot(launch.RaidCode, launch.HostProfileId,
            TownRaidPreparationState.Launching, members, 1, launch.LaunchRevision, members.Length);
        int acks = 0;
        void Ack(int revision) { Assert.That(revision, Is.EqualTo(launch.LaunchRevision)); acks++; }
        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, Ack, 0f),
            Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(acks, Is.Zero);
        save.SetResult((true, default));
        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, Ack, RetryInterval),
            Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(acks, Is.EqualTo(1));
        controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, Ack, RetryInterval * 2f);
        Assert.That(acks, Is.EqualTo(1), "The same snapshot/revision must never ACK twice.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ControllerObservation_CancelledSnapshotCannotAckAfterLateSave(bool isHost)
    {
        var (launch, party, save) = PendingSave(isHost);
        var controller = _root.AddComponent<TownRaidPreparationNetworkController>();
        var members = launch.Participants.Select(
            participant => new TownRaidPreparationMember(
                participant.ProfileId, true, launch.LaunchRevision, false, participant.TeamId.Value)).ToArray();
        var launching = new TownRaidPreparationSnapshot(launch.RaidCode, launch.HostProfileId,
            TownRaidPreparationState.Launching, members, 1, launch.LaunchRevision, members.Length);
        int acks = 0;
        Assert.That(controller.AdvanceLocalLaunch(launching, Profile, _coordinator, party, _ => acks++, 0f),
            Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        _coordinator.CancelPendingRaidLaunch(launch.RaidCode.Value, launch.LaunchRevision);
        var waiting = new TownRaidPreparationSnapshot(launch.RaidCode, launch.HostProfileId,
            TownRaidPreparationState.Waiting, members, 2);
        controller.AdvanceLocalLaunch(waiting, Profile, _coordinator, party, _ => acks++, RetryInterval);
        save.SetResult((true, default));
        controller.AdvanceLocalLaunch(waiting, Profile, _coordinator, party, _ => acks++, RetryInterval * 2f);
        Assert.That(acks, Is.Zero);
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
    }

    // FIX-01 (HacknPlan #668): transient NotReady is classified and retried with spacing inside the
    // local ACK window; a permanent rejection is still reported once per launch revision.

    [TestCase(true)]
    [TestCase(false)]
    public void NotReadyReason_PendingReservationSave_ClearsOnSuccess(bool isHost)
    {
        var (launch, party, save) = PendingSave(isHost);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(_coordinator.LastLaunchNotReadyReason, Is.EqualTo(RaidLaunchNotReadyReason.ReservationSavePending));
        save.SetResult((true, default));
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_coordinator.LastLaunchNotReadyReason, Is.EqualTo(RaidLaunchNotReadyReason.None));
    }

    [Test]
    public void NotReadyReason_TicketOfAnotherRevision_IsTicketMismatch()
    {
        EquipWeapon();
        var (launch, party) = Launch();
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        var (nextRevision, _) = Launch(revision: 2);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(nextRevision, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(_coordinator.LastLaunchNotReadyReason, Is.EqualTo(RaidLaunchNotReadyReason.TicketMismatch));
        Assert.That(_coordinator.ActiveTicket.Value.LaunchContext.LaunchRevision, Is.EqualTo(1),
            "Classifying the mismatch must not touch the stored ticket (stale ticket release is FIX-02).");
    }

    [Test]
    public void NotReadyReason_MissingPartyContinuation_IsClassifiedWithoutReservation()
    {
        EquipWeapon();
        var (launch, _) = Launch();
        var otherHost = new ProfileId("90909090909090909090909090909090");
        Assert.That(TownPartyContinuationContext.TryCreate(otherHost,
            new[] { otherHost, new ProfileId("91919191919191919191919191919191") }, out var otherParty), Is.True);
        foreach (var party in new[] { null, otherParty })
        {
            Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
            Assert.That(_coordinator.LastLaunchNotReadyReason, Is.EqualTo(RaidLaunchNotReadyReason.PartyContinuationMissing));
        }

        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void LocalLaunchRetry_IsSpacedUntilTheConditionResolves_ThenAcksOnce(bool isHost)
    {
        var (launch, party, save) = PendingSave(isHost);
        var controller = _root.AddComponent<TownRaidPreparationNetworkController>();
        var snapshot = LaunchingSnapshot(launch);
        int acks = 0;
        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, 0f),
            Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(controller.LocalLaunchWaitReason, Is.EqualTo(RaidLaunchNotReadyReason.ReservationSavePending));
        save.SetResult((true, default));
        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, RetryInterval * 0.5f),
            Is.EqualTo(RaidLaunchPreparationResult.NotReady), "A retry before the spacing interval must not run.");
        Assert.That(acks, Is.Zero);
        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, RetryInterval),
            Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(acks, Is.EqualTo(1));
        Assert.That(controller.LocalLaunchWaitReason, Is.EqualTo(RaidLaunchNotReadyReason.None));
        for (int i = 2; i < 6; i++)
            controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, RetryInterval * i);
        Assert.That(acks, Is.EqualTo(1), "An acknowledged revision never sends a second ACK.");
    }

    [Test]
    public void LocalLaunchRetry_UnresolvedConditionStopsAtTheAckWindowAndKeepsItsReason()
    {
        var (launch, party, save) = PendingSave(isHost: false);
        var controller = _root.AddComponent<TownRaidPreparationNetworkController>();
        var snapshot = LaunchingSnapshot(launch);
        int acks = 0;
        for (float now = 0f; now < AckWindow; now += RetryInterval)
        {
            Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, now),
                Is.EqualTo(RaidLaunchPreparationResult.NotReady));
            Assert.That(controller.IsLocalLaunchWaitExpired, Is.False);
        }

        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, AckWindow),
            Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(controller.IsLocalLaunchWaitExpired, Is.True);
        Assert.That(controller.LocalLaunchWaitReason, Is.EqualTo(RaidLaunchNotReadyReason.ReservationSavePending),
            "The expired wait keeps its transient cause for the cancellation feedback.");
        save.SetResult((true, default));
        controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, AckWindow + RetryInterval);
        Assert.That(acks, Is.Zero, "No ACK may leave after the local ACK window expired.");
    }

    [Test]
    public void LocalLaunchRetry_MissingPartyContinuationIsRetriedWhenItArrives()
    {
        EquipWeapon();
        var (launch, party) = Launch(isHost: false);
        var controller = _root.AddComponent<TownRaidPreparationNetworkController>();
        var snapshot = LaunchingSnapshot(launch);
        int acks = 0;
        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, null, _ => acks++, 0f),
            Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(controller.LocalLaunchWaitReason, Is.EqualTo(RaidLaunchNotReadyReason.PartyContinuationMissing));
        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, RetryInterval),
            Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(acks, Is.EqualTo(1));
    }

    [Test]
    public void LocalLaunchRetry_PermanentRejectionIsReportedExactlyOnce()
    {
        var (launch, party, save) = PendingSave(isHost: false);
        var controller = _root.AddComponent<TownRaidPreparationNetworkController>();
        var snapshot = LaunchingSnapshot(launch);
        int acks = 0, rejections = 0;
        Assert.That(controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, 0f),
            Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        save.SetResult((false, new BackendError { error = "PREPARED_WEAPON_REQUIRED" }));
        for (int i = 1; i < 6; i++)
        {
            if (controller.AdvanceLocalLaunch(snapshot, Profile, _coordinator, party, _ => acks++, RetryInterval * i) ==
                RaidLaunchPreparationResult.Rejected)
                rejections++;
        }

        Assert.That(rejections, Is.EqualTo(1));
        Assert.That(acks, Is.Zero);
        Assert.That(_coordinator.TryConsumeLastLaunchRejection(out var reason), Is.True);
        Assert.That(reason, Is.EqualTo(ExpeditionPreparationResult.PreparedWeaponRequired));
    }

    private const float RetryInterval = 0.25f;
    private const float AckWindow = 15f;

    private static TownRaidPreparationSnapshot LaunchingSnapshot(RaidLaunchContext launch)
    {
        var members = launch.Participants.Select(
            participant => new TownRaidPreparationMember(
                participant.ProfileId, true, launch.LaunchRevision, false, participant.TeamId.Value)).ToArray();
        return new TownRaidPreparationSnapshot(launch.RaidCode, launch.HostProfileId,
            TownRaidPreparationState.Launching, members, 1, launch.LaunchRevision, members.Length);
    }

    private void EquipWeapon()
    {
        var id = new LootId("training_short_sword");
        Assert.That(_store.TryImportItems(new[] { new StashItem(id, 1) }), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, id), Is.EqualTo(StashOperationResult.Success));
    }

    private (RaidLaunchContext, TownPartyContinuationContext,
        TaskCompletionSource<(bool success, BackendError error)>) PendingSave(bool isHost)
    {
        EquipWeapon();
        var (launch, party) = Launch(isHost);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        // Substitute only the transport completion at the coordinator's existing task boundary.
        // All observations remain through public launch/profile APIs; no real backend is contacted.
        var save = new TaskCompletionSource<(bool success, BackendError error)>();
        typeof(SessionConnectionCoordinator).GetField("_launchReservationSave", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_coordinator, save.Task);
        return (launch, party, save);
    }

    private static (RaidLaunchContext launch, TownPartyContinuationContext party) Launch(bool isHost = true, int revision = 1)
    {
        Assert.That(RaidCode.TryParse("123456", out var code), Is.True);
        Assert.That(RaidTeamId.TryCreate(1, out var team), Is.True);
        var host = isHost ? Profile : new ProfileId("78787878787878787878787878787878");
        var profiles = isHost ? new[] { Profile } : new[] { host, Profile };
        var participants = Array.ConvertAll(profiles, profile => new RaidLaunchParticipant(profile, team));
        Assert.That(RaidLaunchContext.TryCreate(code, host, participants, Profile, revision, out var launch), Is.True);
        Assert.That(TownPartyContinuationContext.TryCreate(host, profiles, out var party), Is.True);
        return (launch, party);
    }
}
#endif
