using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// Covers the pure identity rules in <see cref="DevelopmentProfileIdentity"/>, the
/// <see cref="DirectRaidDevelopmentPreparation.Prepare"/> orchestration rule (via a fake
/// <see cref="IDevelopmentProfileEnvironment"/>), and the required-services chain
/// (<see cref="LocalProfileStore"/> -> <see cref="InMemoryPlayerLoadoutService"/> ->
/// <see cref="RaidLaunchContext"/>) that rule relies on.
///
/// No test drives <see cref="DevelopmentProfileBootstrap.TryPrepareForDirectRaid"/> itself: that
/// method only adapts the pure rule to Unity-process statics
/// (<see cref="LocalProfileProvider"/>, <see cref="ApplicationStashServiceBootstrapper"/>), which
/// is exercised manually in Play Mode. <see cref="DevelopmentProfileBootstrap"/> is confirmed
/// here to have no Unity lifecycle side effects at all.
/// </summary>
public sealed class DevelopmentProfileBootstrapTests
{
    private LootDefinitionCatalog _catalog;
    private LocalProfilePersistenceConfiguration _configuration;
    private GameObject _serviceHost;
    private GameObject _componentHost;

    [SetUp]
    public void SetUp()
    {
        _configuration = AssetDatabase.LoadAssetAtPath<LocalProfilePersistenceConfiguration>(
            "Assets/Resources/LocalProfilePersistenceConfiguration.asset");
        Assert.That(_configuration, Is.Not.Null);
        Assert.That(_configuration.LootCatalog, Is.Not.Null);
        _catalog = _configuration.LootCatalog;

        LocalProfileProvider.ClearRemoteCharacterId();
    }

    [TearDown]
    public void TearDown()
    {
        LocalProfileProvider.ClearRemoteCharacterId();
        if (_serviceHost != null)
        {
            UnityEngine.Object.DestroyImmediate(_serviceHost);
            _serviceHost = null;
        }

        if (_componentHost != null)
        {
            UnityEngine.Object.DestroyImmediate(_componentHost);
            _componentHost = null;
        }
    }

    // --- a. TryCreate ---------------------------------------------------

    [Test]
    public void TryCreate_ValidKeyProducesPrefixedProfileId()
    {
        Assert.That(DevelopmentProfileIdentity.TryCreate("host", out ProfileId profileId), Is.True);
        Assert.That(profileId.IsValid, Is.True);
        Assert.That(profileId.Value, Is.EqualTo(DevelopmentProfileIdentity.Prefix + "host"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("Host")]
    [TestCase("a b")]
    [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 33 characters
    public void TryCreate_RejectsInvalidKeys(string key)
    {
        Assert.That(DevelopmentProfileIdentity.TryCreate(key, out ProfileId profileId), Is.False);
        Assert.That(profileId.IsValid, Is.False);
    }

    // --- b. Determinism and IsDevelopmentProfile -------------------------

    [Test]
    public void TryCreate_IsDeterministicPerKeyAndDistinctAcrossKeys()
    {
        Assert.That(DevelopmentProfileIdentity.TryCreate("host", out ProfileId first), Is.True);
        Assert.That(DevelopmentProfileIdentity.TryCreate("host", out ProfileId second), Is.True);
        Assert.That(first, Is.EqualTo(second));

        Assert.That(DevelopmentProfileIdentity.TryCreate("client-1", out ProfileId other), Is.True);
        Assert.That(first, Is.Not.EqualTo(other));
    }

    [Test]
    public void IsDevelopmentProfile_TrueForDevIdentityFalseForBackendLikeId()
    {
        Assert.That(DevelopmentProfileIdentity.TryCreate("host", out ProfileId devProfile), Is.True);
        Assert.That(DevelopmentProfileIdentity.IsDevelopmentProfile(devProfile), Is.True);

        var backendLike = new ProfileId("605c5f8f1c9d440000a1b2c3");
        Assert.That(DevelopmentProfileIdentity.IsDevelopmentProfile(backendLike), Is.False);
    }

    // --- d. Build gate -------------------------------------------------

    [Test]
    public void IsAvailableInThisBuild_IsTrueInTheEditor()
    {
        // The Unity Editor always compiles with UNITY_EDITOR defined, so this test can only
        // observe the Editor branch. Release-build exclusion is a compile-time #if
        // (UNITY_EDITOR || DEVELOPMENT_BUILD) and is not observable from an EditMode test; it is
        // verified by inspection of DevelopmentProfileIdentity.IsAvailableInThisBuild and the
        // #if guard around DevelopmentProfileBootstrap.TryPrepareForDirectRaid().
        Assert.That(DevelopmentProfileIdentity.IsAvailableInThisBuild, Is.True);
    }

    // --- No Unity lifecycle side effects ---------------------------------

    [Test]
    public void DevelopmentProfileBootstrap_HasNoUnityLifecycleMethods()
    {
        Type type = typeof(DevelopmentProfileBootstrap);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        Assert.That(type.GetMethod("Awake", flags), Is.Null, "Awake must not exist: no Unity lifecycle side effects.");
        Assert.That(type.GetMethod("OnEnable", flags), Is.Null, "OnEnable must not exist: no Unity lifecycle side effects.");
        Assert.That(type.GetMethod("Start", flags), Is.Null, "Start must not exist: no Unity lifecycle side effects.");
    }

    [Test]
    public void AddingBootstrapComponent_LeavesLocalProfileProviderUntouched()
    {
        _componentHost = new GameObject(nameof(DevelopmentProfileBootstrapTests) + "-NoLifecycleSideEffects");
        _componentHost.AddComponent<DevelopmentProfileBootstrap>();

        Assert.That(LocalProfileProvider.GetOrCreateLocalProfile().IsValid, Is.False);
    }

    // --- DirectRaidDevelopmentPreparation.Prepare: guards ----------------

    [Test]
    public void Prepare_DisabledReturnsNotRequestedAndTouchesNothing()
    {
        var environment = new FakeDevelopmentProfileEnvironment();

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            buildAllowsDevelopment: true,
            enabled: false,
            profileKey: "host",
            environment: environment,
            out ProfileId profileId,
            out _);

        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.NotRequested));
        Assert.That(profileId.IsValid, Is.False);
        Assert.That(environment.AssignProfileCallCount, Is.EqualTo(0));
        Assert.That(environment.ClearProfileCallCount, Is.EqualTo(0));
        Assert.That(environment.TryInitializeLocalStashCallCount, Is.EqualTo(0));
    }

    [Test]
    public void Prepare_UnavailableInBuildTouchesNothing()
    {
        var environment = new FakeDevelopmentProfileEnvironment();

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            buildAllowsDevelopment: false,
            enabled: true,
            profileKey: "host",
            environment: environment,
            out ProfileId profileId,
            out _);

        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.UnavailableInBuild));
        Assert.That(profileId.IsValid, Is.False);
        Assert.That(environment.AssignProfileCallCount, Is.EqualTo(0));
        Assert.That(environment.TryInitializeLocalStashCallCount, Is.EqualTo(0));
    }

    [Test]
    public void Prepare_InvalidKeyTouchesNothing()
    {
        var environment = new FakeDevelopmentProfileEnvironment();

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "Not Valid", environment, out ProfileId profileId, out _);

        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.InvalidKey));
        Assert.That(profileId.IsValid, Is.False);
        Assert.That(environment.AssignProfileCallCount, Is.EqualTo(0));
        Assert.That(environment.TryInitializeLocalStashCallCount, Is.EqualTo(0));
    }

    [Test]
    public void Prepare_StashInitFailureClearsOnlyWhatItAssigned()
    {
        var environment = new FakeDevelopmentProfileEnvironment { StashInitializationSucceeds = false };

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId profileId, out _);

        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.StashUnavailable));
        Assert.That(profileId.IsValid, Is.False);
        Assert.That(environment.AssignProfileCallCount, Is.EqualTo(1));
        Assert.That(environment.ClearProfileCallCount, Is.EqualTo(1));
        Assert.That(environment.CurrentProfile.IsValid, Is.False);
    }

    [Test]
    public void Prepare_NullLoadoutServiceReturnsStashUnavailable()
    {
        var environment = new FakeDevelopmentProfileEnvironment { LoadoutService = null };

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId profileId, out _);

        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.StashUnavailable));
        Assert.That(profileId.IsValid, Is.True, "The profile was already assigned and the stash already initialized.");
    }

    [Test]
    public void Prepare_FailingLoadoutPreparationReturnsLoadoutNotLaunchable()
    {
        var loadout = new FakeLoadoutService(ExpeditionPreparationResult.RecoveryWeaponUnavailable);
        var environment = new FakeDevelopmentProfileEnvironment { LoadoutService = loadout };

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out _, out ExpeditionPreparationResult loadoutPreparation);

        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.LoadoutNotLaunchable));
        Assert.That(loadoutPreparation, Is.EqualTo(ExpeditionPreparationResult.RecoveryWeaponUnavailable));
    }

    // --- DirectRaidDevelopmentPreparation.Prepare: happy path + determinism ---

    [Test]
    public void Prepare_EnabledNoProfileAssignsDeterministicDevProfile()
    {
        var environmentA = new FakeDevelopmentProfileEnvironment
        {
            LoadoutService = new FakeLoadoutService(ExpeditionPreparationResult.Success)
        };
        var environmentB = new FakeDevelopmentProfileEnvironment
        {
            LoadoutService = new FakeLoadoutService(ExpeditionPreparationResult.Success)
        };

        DirectRaidDevelopmentPreparationResult resultA = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environmentA, out ProfileId profileA, out _);
        DirectRaidDevelopmentPreparationResult resultB = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environmentB, out ProfileId profileB, out _);

        Assert.That(resultA, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));
        Assert.That(resultB, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));
        Assert.That(profileA, Is.EqualTo(profileB));
        Assert.That(profileA, Is.EqualTo(new ProfileId(DevelopmentProfileIdentity.Prefix + "host")));
        Assert.That(environmentA.AssignProfileCallCount, Is.EqualTo(1));
        Assert.That(environmentA.TryInitializeLocalStashCallCount, Is.EqualTo(1));
    }

    [Test]
    public void Prepare_RealProfilePresentNeverAssignsOrClearsButStillPreparesLoadout()
    {
        var realProfile = new ProfileId("605c5f8f1c9d440000a1b2c3");
        var loadout = new FakeLoadoutService(ExpeditionPreparationResult.Success);
        var environment = new FakeDevelopmentProfileEnvironment(realProfile) { LoadoutService = loadout };

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId profileId, out _);

        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));
        Assert.That(profileId, Is.EqualTo(realProfile));
        Assert.That(environment.AssignProfileCallCount, Is.EqualTo(0));
        Assert.That(environment.ClearProfileCallCount, Is.EqualTo(0));
        Assert.That(environment.TryInitializeLocalStashCallCount, Is.EqualTo(0));
        Assert.That(loadout.LastProfileRequested, Is.EqualTo(realProfile));
    }

    [Test]
    public void Prepare_IdempotentAcrossTwoCallsAssignsOnceTotal()
    {
        var loadout = new FakeLoadoutService(ExpeditionPreparationResult.Success);
        var environment = new FakeDevelopmentProfileEnvironment { LoadoutService = loadout };

        DirectRaidDevelopmentPreparationResult firstResult = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId firstProfile, out _);
        DirectRaidDevelopmentPreparationResult secondResult = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId secondProfile, out _);

        Assert.That(firstResult, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));
        Assert.That(secondResult, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));
        Assert.That(firstProfile, Is.EqualTo(secondProfile));
        Assert.That(environment.AssignProfileCallCount, Is.EqualTo(1));
        Assert.That(environment.TryInitializeLocalStashCallCount, Is.EqualTo(1));
        Assert.That(loadout.CallCount, Is.EqualTo(2), "TryPrepareExpeditionLoadout always runs, once per attempt.");
    }

    // --- Real loadout boundary: LocalProfileStore -> InMemoryPlayerLoadoutService ---

    [Test]
    public void Prepare_NoAuthRequired_ApplicationAuthContextNeverPresent()
    {
        Assert.That(
            UnityEngine.Object.FindAnyObjectByType<ApplicationAuthContext>(),
            Is.Null,
            "No ApplicationAuthContext should exist before a direct raid preparation.");

        (FakeDevelopmentProfileEnvironment environment, _) = CreateRealEnvironment();
        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out _, out _);

        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));
        Assert.That(
            UnityEngine.Object.FindAnyObjectByType<ApplicationAuthContext>(),
            Is.Null,
            "Preparing an offline development profile must never create or require an auth context.");
    }

    [Test]
    public void Prepare_ThroughRealBoundary_ReservationCarriesConfiguredRecoveryWeapon()
    {
        (FakeDevelopmentProfileEnvironment environment, IPlayerLoadoutService loadoutService) = CreateRealEnvironment();

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId profileId, out _);
        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));

        Assert.That(
            loadoutService.TryCreateLoadoutReservation(profileId, "reservation-1", out PendingLoadoutReservation reservation),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(reservation, Is.Not.Null);
        Assert.That(reservation.PreparedEquipment.WeaponSetAMainHand, Is.EqualTo(_configuration.RecoveryWeaponLootId));
    }

    [Test]
    public void Prepare_SecondDirectRaidAfterConfirmGrantsRecoveryWeaponAgain()
    {
        (FakeDevelopmentProfileEnvironment environment, IPlayerLoadoutService loadoutService) = CreateRealEnvironment();

        DirectRaidDevelopmentPreparationResult firstResult = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId profileId, out _);
        Assert.That(firstResult, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));

        Assert.That(
            loadoutService.TryCreateLoadoutReservation(profileId, "reservation-1", out PendingLoadoutReservation firstReservation),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(firstReservation.PreparedEquipment.WeaponSetAMainHand, Is.EqualTo(_configuration.RecoveryWeaponLootId));
        Assert.That(
            loadoutService.TryConfirmLoadoutReservation(profileId, "reservation-1"),
            Is.EqualTo(StashOperationResult.Success));

        // Second direct raid attempt in the same session, after the first reservation was
        // confirmed (the prepared weapon was consumed, not restored).
        DirectRaidDevelopmentPreparationResult secondResult = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId secondProfile, out _);
        Assert.That(secondResult, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));
        Assert.That(secondProfile, Is.EqualTo(profileId));
        Assert.That(environment.AssignProfileCallCount, Is.EqualTo(1), "The same dev profile is reused, never reassigned.");

        Assert.That(
            loadoutService.TryCreateLoadoutReservation(profileId, "reservation-2", out PendingLoadoutReservation secondReservation),
            Is.EqualTo(StashOperationResult.Success));
        Assert.That(secondReservation.PreparedEquipment.WeaponSetAMainHand, Is.EqualTo(_configuration.RecoveryWeaponLootId));
    }

    [Test]
    public void Prepare_DirectRaidContextIsValidForPreparedDevProfile()
    {
        (FakeDevelopmentProfileEnvironment environment, _) = CreateRealEnvironment();

        DirectRaidDevelopmentPreparationResult result = DirectRaidDevelopmentPreparation.Prepare(
            true, true, "host", environment, out ProfileId profileId, out _);
        Assert.That(result, Is.EqualTo(DirectRaidDevelopmentPreparationResult.Prepared));

        Assert.That(RaidCode.TryParse("900001", out RaidCode raidCode), Is.True);
        Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId teamId), Is.True);
        var participants = new[] { new RaidLaunchParticipant(profileId, teamId) };

        bool created = RaidLaunchContext.TryCreate(
            raidCode,
            profileId,
            participants,
            profileId,
            1,
            out RaidLaunchContext context);

        Assert.That(created, Is.True);
        Assert.That(context.HostProfileId, Is.EqualTo(profileId));
        Assert.That(context.LocalProfileId, Is.EqualTo(profileId));
        Assert.That(context.ParticipantProfileIds, Is.EqualTo(new[] { profileId }));
    }

    // --- h. Normal auth path unchanged -----------------------------------

    [Test]
    public void LocalProfileProvider_StillDefaultsAfterClear()
    {
        LocalProfileProvider.SetRemoteCharacterId(new ProfileId("605c5f8f1c9d440000a1b2c3"));
        LocalProfileProvider.ClearRemoteCharacterId();

        Assert.That(LocalProfileProvider.GetOrCreateLocalProfile().IsValid, Is.False);
    }

    private (FakeDevelopmentProfileEnvironment environment, IPlayerLoadoutService loadoutService) CreateRealEnvironment()
    {
        Assert.That(DevelopmentProfileIdentity.TryCreate("host", out ProfileId devProfileId), Is.True);

        var fileStore = new MemoryFileStore();
        var repository = new LocalProfileRepository(fileStore, ".", _configuration.AbilityCatalog);
        Assert.That(repository.Initialize(devProfileId, _catalog), Is.True, repository.LastError);

        var store = new LocalProfileStore(
            repository,
            devProfileId,
            _configuration.LootCatalog,
            _configuration.RecoveryWeaponLootId,
            _configuration.MissionCatalog,
            _configuration.AbilityCatalog);

        _serviceHost = new GameObject(nameof(DevelopmentProfileBootstrapTests));
        var loadoutService = _serviceHost.AddComponent<InMemoryPlayerLoadoutService>();
        loadoutService.Initialize(store);

        var environment = new FakeDevelopmentProfileEnvironment { LoadoutService = loadoutService };
        return (environment, loadoutService);
    }

    /// <summary>Fake <see cref="IDevelopmentProfileEnvironment"/> that records call counts and
    /// mirrors the real assign/clear semantics without touching any Unity static.</summary>
    private sealed class FakeDevelopmentProfileEnvironment : IDevelopmentProfileEnvironment
    {
        private ProfileId _currentProfile;

        public FakeDevelopmentProfileEnvironment(ProfileId initialProfile = default)
        {
            _currentProfile = initialProfile;
        }

        public int AssignProfileCallCount { get; private set; }
        public int ClearProfileCallCount { get; private set; }
        public int TryInitializeLocalStashCallCount { get; private set; }
        public bool StashInitializationSucceeds { get; set; } = true;
        public IPlayerLoadoutService LoadoutService { get; set; }

        public ProfileId CurrentProfile => _currentProfile;

        public void AssignProfile(ProfileId profileId)
        {
            AssignProfileCallCount++;
            _currentProfile = profileId;
        }

        public void ClearProfile()
        {
            ClearProfileCallCount++;
            _currentProfile = default;
        }

        public bool TryInitializeLocalStash(ProfileId profileId)
        {
            TryInitializeLocalStashCallCount++;
            return StashInitializationSucceeds;
        }
    }

    /// <summary>Fake <see cref="IPlayerLoadoutService"/> whose only meaningful member is
    /// <see cref="TryPrepareExpeditionLoadout"/>; every other member is unused by this rule.</summary>
    private sealed class FakeLoadoutService : IPlayerLoadoutService
    {
        private readonly ExpeditionPreparationResult _result;

        public FakeLoadoutService(ExpeditionPreparationResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }
        public ProfileId LastProfileRequested { get; private set; }

        public event Action<ProfileId> LoadoutChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<StashItem> GetLoadout(ProfileId profileId) => Array.Empty<StashItem>();

        public PreparedEquipmentLoadout GetPreparedEquipment(ProfileId profileId) => default;

        public StashOperationResult TryAssignPreparedEquipment(ProfileId profileId, EquipmentSlot slot, LootId lootId) =>
            StashOperationResult.InvalidInventory;

        public StashOperationResult TryClearPreparedEquipment(ProfileId profileId, EquipmentSlot slot) =>
            StashOperationResult.InvalidInventory;

        public StashOperationResult TryEquipFromStash(ProfileId profileId, LootId lootId, EquipmentSlot slot) =>
            StashOperationResult.InvalidInventory;

        public ExpeditionPreparationResult TryPrepareExpeditionLoadout(ProfileId profileId)
        {
            CallCount++;
            LastProfileRequested = profileId;
            return _result;
        }

        public StashOperationResult TryTransferToLoadout(ProfileId profileId, LootId lootId, int amount) =>
            StashOperationResult.InvalidInventory;

        public StashOperationResult TryTransferToStash(ProfileId profileId, LootId lootId, int amount) =>
            StashOperationResult.InvalidInventory;

        public StashOperationResult TryTransferAllToLoadout(ProfileId profileId) =>
            StashOperationResult.InvalidInventory;

        public StashOperationResult TryTransferAllToStash(ProfileId profileId) =>
            StashOperationResult.InvalidInventory;

        public StashOperationResult TryImportItems(ProfileId profileId, IReadOnlyList<StashItem> items) =>
            StashOperationResult.InvalidInventory;

        public StashOperationResult TryCreateLoadoutReservation(
            ProfileId profileId,
            string reservationId,
            out PendingLoadoutReservation reservation)
        {
            reservation = null;
            return StashOperationResult.InvalidInventory;
        }

        public StashOperationResult TryConfirmLoadoutReservation(ProfileId profileId, string reservationId) =>
            StashOperationResult.InvalidInventory;

        public StashOperationResult TryRollbackLoadoutReservation(ProfileId profileId, string reservationId) =>
            StashOperationResult.InvalidInventory;
    }

    /// <summary>In-memory <see cref="ILocalProfileFileStore"/> fake, mirroring the pattern used by
    /// LocalProfilePersistenceEditModeTests. No file is ever written to disk.</summary>
    private sealed class MemoryFileStore : ILocalProfileFileStore
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public bool Exists(string path) => _files.ContainsKey(path);

        public bool TryRead(string path, out string contents, out string error)
        {
            if (_files.TryGetValue(path, out contents))
            {
                error = null;
                return true;
            }

            error = "Missing file";
            return false;
        }

        public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
        {
            if (_files.TryGetValue(mainPath, out string previous))
            {
                _files[backupPath] = previous;
            }

            _files[mainPath] = contents;
            error = null;
            return true;
        }

        public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
        {
            if (!_files.TryGetValue(backupPath, out string backup))
            {
                error = "Missing backup";
                return false;
            }

            _files[mainPath] = backup;
            error = null;
            return true;
        }
    }
}
