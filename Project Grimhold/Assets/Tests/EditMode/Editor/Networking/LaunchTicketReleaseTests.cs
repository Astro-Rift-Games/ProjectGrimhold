#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;

/// <summary>
/// FIX-02 (HacknPlan #669): stale launch tickets are released, a failed reservation rollback is
/// retried within a bound and then recovered through the existing Town routes, and the stored
/// reservation stays consistent (items neither lost nor duplicated) throughout.
/// </summary>
public sealed class LaunchTicketReleaseTests
{
    private const int RollbackAttempts = 3;
    private const string UnbuiltTownSceneName = "FIX02-Unbuilt-Town-Scene";
    private static readonly ProfileId Profile = new("57575757575757575757575757575757");
    private static readonly Regex CommitFailed = new("Commit failed");
    private static readonly Regex CancelRollbackFailed = new("Could not roll back the cancelled raid reservation");
    private static readonly string[] ReconnectPrefsKeys =
    {
        "Grimhold.RaidReconnect.RaidCode", "Grimhold.RaidReconnect.ProfileId",
        "Grimhold.RaidReconnect.GenerationId", "Grimhold.RaidReconnect.SessionName",
        "Grimhold.RaidReconnect.ReservationId", "Grimhold.RaidReconnect.CreatedAtUtcTicks"
    };

    private GameObject _root;
    private ProfileId _previousProfile;
    private FlakyRepository _repository;
    private LocalProfileStore _store;
    private SessionConnectionCoordinator _coordinator;
    private SessionConnectionCoordinator _previousCoordinator;
    private SessionConnectionStateMachine _state;
    private PreparedEquipmentLoadout _selected;
    private string _baselineTotals;
    private Dictionary<string, string> _savedPrefs;

    [SetUp]
    public void SetUp()
    {
        _previousCoordinator = SessionConnectionCoordinator.Instance;
        _previousProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        LocalProfileProvider.SetRemoteCharacterId(Profile);
        var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
        _repository = new FlakyRepository();
        Assert.That(_repository.Initialize(Profile, catalog), Is.True);
        _store = new LocalProfileStore(_repository, Profile, catalog);
        _root = new GameObject(nameof(LaunchTicketReleaseTests));
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
        _state = (SessionConnectionStateMachine)typeof(SessionConnectionCoordinator)
            .GetField("_stateMachine", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_coordinator);
        Assert.That(_state.TryTransition(SessionConnectionState.ConnectingTown), Is.True);
        Assert.That(_state.TryTransition(SessionConnectionState.Town), Is.True);

        var weapon = new LootId("training_short_sword");
        Assert.That(_store.TryImportItems(new[] { new StashItem(new LootId("bone"), 3) }),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.TryTransferAllToLoadout(), Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.TryImportItems(new[] { new StashItem(weapon, 1), new StashItem(new LootId("healthpotion"), 2) }),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.TryAssignPreparedEquipment(EquipmentSlot.WeaponSetAMainHand, weapon),
            Is.EqualTo(StashOperationResult.Success));
        _selected = _store.GetPreparedEquipment();
        _baselineTotals = Totals();
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        LocalProfileProvider.SetRemoteCharacterId(_previousProfile);
        typeof(SessionConnectionCoordinator).GetField("<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousCoordinator);
        RestoreReconnectPrefs();
    }

    // S3 (PRESERVE): a cancel whose rollback succeeds leaves the next revision free to reserve and ACK.
    [Test]
    public void CancelledRevision_RollbackSucceeds_NewRevisionReservesAndAcks()
    {
        var (first, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(first, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        string firstReservation = _store.PendingReservation.ReservationId;
        _coordinator.CancelPendingRaidLaunch(first.RaidCode.Value, 1);
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(_store.GetPreparedEquipment(), Is.EqualTo(_selected));
        Assert.That(Totals(), Is.EqualTo(_baselineTotals));

        var (second, _) = Launch(revision: 2);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(second, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_coordinator.HasActiveLaunchTicket(second.RaidCode.Value, 2, Profile), Is.True);
        Assert.That(_store.PendingReservation.ReservationId, Is.Not.EqualTo(firstReservation));
        AssertReservedExactlyOnce();
    }

    // S3: a ticket left by an older revision is released instead of blocking the new revision.
    [Test]
    public void TicketOfOlderRevision_IsReleasedAndNewRevisionReservesAndAcks()
    {
        var (first, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(first, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        string firstReservation = _store.PendingReservation.ReservationId;

        var (second, _) = Launch(revision: 2);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(second, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_coordinator.HasActiveLaunchTicket(second.RaidCode.Value, 2, Profile), Is.True);
        Assert.That(_coordinator.HasActiveLaunchTicket(first.RaidCode.Value, 1, Profile), Is.False);
        Assert.That(_store.PendingReservation.ReservationId, Is.Not.EqualTo(firstReservation));
        AssertReservedExactlyOnce();
    }

    // S1: a transient rollback failure is retried inside the bound, so the cancel still completes.
    [Test]
    public void Cancel_TransientRollbackFailure_IsRetriedWithinTheBound()
    {
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        _repository.FailingSaves = RollbackAttempts - 1;
        ExpectCommitFailures(RollbackAttempts - 1);
        _coordinator.CancelPendingRaidLaunch(launch.RaidCode.Value, 1);
        Assert.That(_repository.SaveFailures, Is.EqualTo(RollbackAttempts - 1));
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(_store.GetPreparedEquipment(), Is.EqualTo(_selected));
        Assert.That(Totals(), Is.EqualTo(_baselineTotals));
    }

    // S1: the retry is bounded; an unrecoverable failure does not loop.
    [Test]
    public void Cancel_PersistentRollbackFailure_StopsAtTheBoundAndKeepsTheReservation()
    {
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        PendingLoadoutReservation reserved = _store.PendingReservation;
        _repository.FailingSaves = int.MaxValue;
        ExpectCommitFailures(RollbackAttempts);
        LogAssert.Expect(LogType.Error, CancelRollbackFailed);
        _coordinator.CancelPendingRaidLaunch(launch.RaidCode.Value, 1);
        Assert.That(_repository.SaveFailures, Is.EqualTo(RollbackAttempts));
        Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo(reserved.ReservationId));
        AssertReservedExactlyOnce();
    }

    // S4/S5: after a failed rollback the retained ticket can never report success for a launch that
    // was cancelled, and the next launch after the store recovers reserves again without loss or
    // duplication, for the same and for a later revision.
    [TestCase(1)]
    [TestCase(2)]
    public void Cancel_RollbackFails_ThenRecovery_AllowsRelaunchWithoutLossOrDuplication(int relaunchRevision)
    {
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        string failedReservation = _store.PendingReservation.ReservationId;
        _repository.FailingSaves = int.MaxValue;
        ExpectCommitFailures(RollbackAttempts);
        LogAssert.Expect(LogType.Error, CancelRollbackFailed);
        _coordinator.CancelPendingRaidLaunch(launch.RaidCode.Value, 1);
        Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo(failedReservation));

        var (relaunch, _) = Launch(revision: relaunchRevision);
        ExpectCommitFailures(RollbackAttempts);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(relaunch, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady),
            "A cancelled launch must not ACK while its reservation is still stale.");
        Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo(failedReservation));
        AssertReservedExactlyOnce();

        _repository.FailingSaves = 0;
        Assert.That(_coordinator.TryStoreRaidLaunchContext(relaunch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_coordinator.HasActiveLaunchTicket(relaunch.RaidCode.Value, relaunchRevision, Profile), Is.True);
        Assert.That(_store.PendingReservation.ReservationId, Is.Not.EqualTo(failedReservation));
        AssertReservedExactlyOnce();
    }

    // S4: a stale ticket whose rollback failed cannot be released into a launch through the ACK path.
    [Test]
    public void StaleTicketAfterFailedRollback_CannotBeginTheAcknowledgedLaunch()
    {
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        _repository.FailingSaves = int.MaxValue;
        ExpectCommitFailures(RollbackAttempts);
        LogAssert.Expect(LogType.Error, CancelRollbackFailed);
        _coordinator.CancelPendingRaidLaunch(launch.RaidCode.Value, 1);

        _coordinator.BeginAcknowledgedRaidLaunch(launch.RaidCode.Value, 1);
        Assert.That(ReadField<bool>("_launchDispatchActive"), Is.False);
        Assert.That(ReadField<int>("_acknowledgedLaunchSequence"), Is.Zero);
    }

    // S1/S2: a rollback failure during raid-failure recovery no longer stops in Failed before the
    // Town recovery: it continues through ReturningTown, keeping the reservation intact.
    [Test]
    public async Task RaidFailureRecovery_RollbackFails_StillAttemptsTownRecoveryAndKeepsReservation()
    {
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        string reservation = _store.PendingReservation.ReservationId;
        typeof(SessionConnectionCoordinator).GetField("_hubLauncher", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_coordinator, _root.GetComponent<HubSessionLauncher>());
        typeof(SessionConnectionCoordinator).GetField("_raidLauncher", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_coordinator, _root.GetComponent<FusionSessionLauncher>());
        typeof(SessionConnectionCoordinator).GetField("_townSceneName", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_coordinator, UnbuiltTownSceneName);
        Assert.That(_state.TryTransition(SessionConnectionState.PreparingRaid), Is.True);
        Assert.That(_state.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);
        _repository.FailingSaves = int.MaxValue;
        ExpectCommitFailures(RollbackAttempts);
        // The unbuilt Town scene makes the Town startup throw; reaching it proves recovery went on.
        LogAssert.Expect(LogType.Exception, new Regex("ArgumentException"));

        var recovery = (Task<SessionTransitionResult>)typeof(SessionConnectionCoordinator)
            .GetMethod("RecoverTownAfterRaidFailureAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_coordinator, new object[] { SessionTransitionResult.ConnectionFailed });
        Assert.That(await recovery, Is.EqualTo(SessionTransitionResult.RecoveryFailed));
        Assert.That(_coordinator.TryConsumeLastTransitionFailure(out var failure), Is.True);
        Assert.That(failure, Is.EqualTo(SessionTransitionResult.RecoveryFailed));
        Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo(reservation));
        Assert.That(_coordinator.ActiveTicket.Value.LoadoutReservation.ReservationId, Is.EqualTo(reservation),
            "The ticket keeps the reservation handle so the next recovery can roll it back.");
        AssertReservedExactlyOnce();
    }

    // FIX-05 interplay: a ticket and reservation left behind by a Failed recovery are rolled back
    // by the defined exit once the store recovers, so nothing stays reserved.
    [Test]
    public async Task FailedRecovery_LeftoverReservation_IsRolledBackWhenLeavingForTheMainMenu()
    {
        SaveReconnectPrefs();
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        typeof(SessionConnectionCoordinator).GetField("_hubLauncher", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_coordinator, _root.GetComponent<HubSessionLauncher>());
        typeof(SessionConnectionCoordinator).GetField("_raidLauncher", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_coordinator, _root.GetComponent<FusionSessionLauncher>());
        typeof(SessionConnectionCoordinator).GetField("_townSceneName", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_coordinator, UnbuiltTownSceneName);
        Assert.That(_state.TryTransition(SessionConnectionState.PreparingRaid), Is.True);
        Assert.That(_state.TryTransition(SessionConnectionState.ConnectingRaid), Is.True);
        _repository.FailingSaves = int.MaxValue;
        ExpectCommitFailures(RollbackAttempts);
        LogAssert.Expect(LogType.Exception, new Regex("ArgumentException"));
        var recovery = (Task<SessionTransitionResult>)typeof(SessionConnectionCoordinator)
            .GetMethod("RecoverTownAfterRaidFailureAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_coordinator, new object[] { SessionTransitionResult.ConnectionFailed });
        Assert.That(await recovery, Is.EqualTo(SessionTransitionResult.RecoveryFailed));
        Assert.That(_coordinator.State, Is.EqualTo(SessionConnectionState.Failed));
        Assert.That(_store.PendingReservation, Is.Not.Null);

        _repository.FailingSaves = 0;
        Assert.That(await _coordinator.ReturnToMainMenuAsync(), Is.EqualTo(SessionTransitionResult.Succeeded));
        Assert.That(_coordinator.State, Is.EqualTo(SessionConnectionState.MainMenu));
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(Totals(), Is.EqualTo(_baselineTotals));
    }

    // L6: leaving for the main menu rolls the non-admitted reservation back before the ticket is dropped.
    [Test]
    public async Task ReturnToMainMenu_RollsBackTheReservationBeforeDroppingTheTicket()
    {
        SaveReconnectPrefs();
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_store.PendingReservation, Is.Not.Null);

        Assert.That(await _coordinator.ReturnToMainMenuAsync(), Is.EqualTo(SessionTransitionResult.Succeeded));
        Assert.That(_coordinator.State, Is.EqualTo(SessionConnectionState.MainMenu));
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(_store.GetPreparedEquipment(), Is.EqualTo(_selected));
        Assert.That(Totals(), Is.EqualTo(_baselineTotals));
    }

    // L12: when the rollback still fails after the bounded retry the player is never stuck: the
    // application leaves for the main menu as before, the ticket is dropped (it can no longer ACK
    // or dispatch) and the reservation stays in the store for the login orphan resolution.
    [Test]
    public async Task ReturnToMainMenu_RollbackFails_StillLeavesAndKeepsThePendingReservation()
    {
        SaveReconnectPrefs();
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        string reservation = _store.PendingReservation.ReservationId;
        _repository.FailingSaves = int.MaxValue;
        ExpectCommitFailures(RollbackAttempts);

        Assert.That(await _coordinator.ReturnToMainMenuAsync(), Is.EqualTo(SessionTransitionResult.Succeeded));
        Assert.That(_coordinator.State, Is.EqualTo(SessionConnectionState.MainMenu));
        Assert.That(_coordinator.ActiveTicket, Is.Null);
        Assert.That(ReadField<bool>("_ticketReleasePending"), Is.False);
        Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo(reservation));
        AssertReservedExactlyOnce();

        // The login orphan resolution then rolls the same reservation back: nothing lost or duplicated.
        _repository.FailingSaves = 0;
        Assert.That(ApplicationStashServiceBootstrapper.TryResolveOrphanedReservation(_store, admitted: false),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(_store.PendingReservation, Is.Null);
        Assert.That(_store.GetPreparedEquipment(), Is.EqualTo(_selected));
        Assert.That(Totals(), Is.EqualTo(_baselineTotals));
    }

    // A2: a launch of another raid code never releases a ticket already acknowledged for the live
    // launch (the whole party launches or nobody does).
    [Test]
    public void AcknowledgedTicket_IsNotReleasedByALaunchOfAnotherRaidCode()
    {
        var (live, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(live, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        string reservation = _store.PendingReservation.ReservationId;
        _coordinator.BeginAcknowledgedRaidLaunch(live.RaidCode.Value, 1);
        Assert.That(ReadField<int>("_acknowledgedLaunchSequence"), Is.EqualTo(1));

        var (other, _) = Launch(revision: 1, raidCode: "654321");
        Assert.That(_coordinator.TryStoreRaidLaunchContext(other, party), Is.EqualTo(RaidLaunchPreparationResult.NotReady));
        Assert.That(_coordinator.LastLaunchNotReadyReason, Is.EqualTo(RaidLaunchNotReadyReason.TicketMismatch));
        Assert.That(_coordinator.HasActiveLaunchTicket(live.RaidCode.Value, 1, Profile), Is.True);
        Assert.That(ReadField<int>("_acknowledgedLaunchSequence"), Is.EqualTo(1));
        Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo(reservation));
    }

    // A2 (PRESERVE): a never-acknowledged ticket of another raid code is stale and is released.
    [Test]
    public void UnacknowledgedTicket_IsReleasedByALaunchOfAnotherRaidCode()
    {
        var (first, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(first, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        string reservation = _store.PendingReservation.ReservationId;

        var (other, _) = Launch(revision: 1, raidCode: "654321");
        Assert.That(_coordinator.TryStoreRaidLaunchContext(other, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_coordinator.HasActiveLaunchTicket(other.RaidCode.Value, 1, Profile), Is.True);
        Assert.That(_store.PendingReservation.ReservationId, Is.Not.EqualTo(reservation));
        AssertReservedExactlyOnce();
    }

    // S1/S4 (A5): a backend-rejected save whose local rollback also fails disarms the ticket;
    // once the store recovers the next revision reserves without loss or duplication.
    [Test]
    public void ObservedSaveRejected_RollbackFails_TicketDisarmed_ThenNextRevisionReserves()
    {
        var (launch, party) = Launch(revision: 1);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        string failedReservation = _store.PendingReservation.ReservationId;
        typeof(SessionConnectionCoordinator).GetField("_launchReservationSave", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_coordinator, Task.FromResult((false, new Grimhold.Backend.BackendError { error = "PREPARED_WEAPON_REQUIRED" })));
        _repository.FailingSaves = int.MaxValue;
        ExpectCommitFailures(RollbackAttempts);

        Assert.That(_coordinator.TryStoreRaidLaunchContext(launch, party), Is.EqualTo(RaidLaunchPreparationResult.Rejected));
        Assert.That(_coordinator.TryConsumeLastLaunchRejection(out var reason), Is.True);
        Assert.That(reason, Is.EqualTo(ExpeditionPreparationResult.PreparedWeaponRequired));
        Assert.That(_store.PendingReservation.ReservationId, Is.EqualTo(failedReservation));
        Assert.That(ReadField<bool>("_ticketReleasePending"), Is.True);
        _coordinator.BeginAcknowledgedRaidLaunch(launch.RaidCode.Value, 1);
        Assert.That(ReadField<int>("_acknowledgedLaunchSequence"), Is.Zero, "A disarmed ticket must not ACK.");

        _repository.FailingSaves = 0;
        var (next, _) = Launch(revision: 2);
        Assert.That(_coordinator.TryStoreRaidLaunchContext(next, party), Is.EqualTo(RaidLaunchPreparationResult.Success));
        Assert.That(_coordinator.HasActiveLaunchTicket(next.RaidCode.Value, 2, Profile), Is.True);
        Assert.That(_store.PendingReservation.ReservationId, Is.Not.EqualTo(failedReservation));
        AssertReservedExactlyOnce();
    }

    private T ReadField<T>(string name) =>
        (T)typeof(SessionConnectionCoordinator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(_coordinator);

    private static void ExpectCommitFailures(int count)
    {
        for (int i = 0; i < count; i++) LogAssert.Expect(LogType.Error, CommitFailed);
    }

    /// <summary>Per-loot totals across Stash, Loadout and the pending reservation.</summary>
    private string Totals()
    {
        var totals = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Add(IEnumerable<StashItem> items)
        {
            foreach (StashItem item in items)
                totals[item.LootId.Value] = totals.GetValueOrDefault(item.LootId.Value) + item.Amount;
        }

        Add(_store.GetStash());
        Add(_store.GetLoadout());
        if (_store.PendingReservation != null) Add(_store.PendingReservation.Items);
        return string.Join(";", totals.Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    /// <summary>
    /// The pending reservation holds the loadout and the prepared equipment, and nothing is
    /// left in the live Loadout / prepared selection (no duplication, no loss).
    /// </summary>
    private void AssertReservedExactlyOnce()
    {
        Assert.That(Totals(), Is.EqualTo(_baselineTotals), "Items must be neither lost nor duplicated.");
        PendingLoadoutReservation reservation = _store.PendingReservation;
        Assert.That(reservation, Is.Not.Null);
        Assert.That(reservation.PreparedEquipment, Is.EqualTo(_selected));
        Assert.That(_store.GetPreparedEquipment().HasAnyEquipment, Is.False);
        Assert.That(_store.GetLoadout(), Is.Empty);
    }

    private void SaveReconnectPrefs()
    {
        _savedPrefs = new Dictionary<string, string>();
        foreach (string key in ReconnectPrefsKeys)
            if (PlayerPrefs.HasKey(key)) _savedPrefs[key] = PlayerPrefs.GetString(key);
    }

    private void RestoreReconnectPrefs()
    {
        if (_savedPrefs == null) return;
        foreach (string key in ReconnectPrefsKeys)
        {
            if (_savedPrefs.TryGetValue(key, out string value)) PlayerPrefs.SetString(key, value);
            else PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
        _savedPrefs = null;
    }

    private static (RaidLaunchContext launch, TownPartyContinuationContext party) Launch(int revision, string raidCode = "123456")
    {
        Assert.That(RaidCode.TryParse(raidCode, out var code), Is.True);
        Assert.That(RaidTeamId.TryCreate(1, out var team), Is.True);
        var participants = new[] { new RaidLaunchParticipant(Profile, team) };
        Assert.That(RaidLaunchContext.TryCreate(code, Profile, participants, Profile, revision, out var launch), Is.True);
        Assert.That(TownPartyContinuationContext.TryCreate(Profile, new[] { Profile }, out var party), Is.True);
        return (launch, party);
    }

    /// <summary>
    /// Real in-memory repository whose saves can be made to fail, so a rollback fails through the
    /// store's own persistence path and succeeds again once the failure is lifted.
    /// </summary>
    private sealed class FlakyRepository : ILocalProfileRepository
    {
        private readonly InMemoryLocalProfileRepository _inner = new();

        /// <summary>Number of upcoming saves that fail.</summary>
        public int FailingSaves { get; set; }

        public int SaveFailures { get; private set; }
        public LocalProfilePersistenceStatus Status => _inner.Status;
        public string LastError => _inner.LastError;
        public LocalProfileSnapshot Snapshot => _inner.Snapshot;

        public bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog) =>
            _inner.Initialize(profileId, catalog);

        public bool TrySave(LocalProfileSnapshot snapshot, out string error)
        {
            if (FailingSaves > 0)
            {
                if (FailingSaves != int.MaxValue) FailingSaves--;
                SaveFailures++;
                error = "Simulated persistence failure.";
                return false;
            }

            return _inner.TrySave(snapshot, out error);
        }
    }
}
#endif
