using Fusion;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FusionSessionLauncher : MonoBehaviour, ISessionRunnerOwner
{
    private const float LocalAdmissionTimeoutSeconds = 30f;
    [Header("Session")]
    [SerializeField]
    private string _sessionName;

    [Header("Spawning Configuration")]
    [SerializeField]
    private NetworkPrefabRef _raidPlayerPrefab;

    [SerializeField]
    private NetworkPrefabRef _raidParticipantPrefab;

    [SerializeField]
    private NetworkPrefabRef[] _enemyPrefabs;

    [Header("Coordinator Configuration")]
    [SerializeField]
    private NetworkPrefabRef _matchControllerPrefab;

    private NetworkRunner _runner;
    private GameObject _runnerObject;
    private NetworkSpawnManager _spawnManager;
    private NetworkMatchController _matchController;
    private ExtractionSanctuaryAssignmentService _sanctuaryAssignmentService;
    private bool _isStarting;
    private LauncherShutdownListener _shutdownListener;
    private NetworkRunner _hostMigrationSourceRunner;

    public NetworkRunner Runner => _runner;
    public static int RaidPlayerCapacity => RaidSessionRules.MaxParticipants;
    public NetworkMatchController MatchController => _matchController;
    public ShutdownReason LastStartShutdownReason { get; private set; } = ShutdownReason.Ok;
    public event Action<NetworkRunner, ShutdownReason> RunnerShutdownObserved;

    /// <summary>
    /// Returns whether a coordinated Client should keep waiting for its exact Host session.
    /// Only a missing session is transient while the Host is still creating it. A closed
    /// session already exists and is therefore a terminal transition failure.
    /// </summary>
    public static bool IsSessionAvailabilityPending(ShutdownReason shutdownReason) =>
        shutdownReason == ShutdownReason.GameNotFound;

    public Task<bool> StartCoordinatedSessionAsync(
        string sessionName,
        GameMode mode,
        string gameplaySceneName,
        RaidLaunchContext launchContext,
        PendingLoadoutReservation loadoutReservation = null,
        CancellationToken cancellationToken = default)
    {
        return StartSessionInternalAsync(
            sessionName,
            mode,
            SessionStartupContext.FreshSession,
            gameplaySceneName,
            launchContext,
            loadoutReservation,
            cancellationToken);
    }

    private async Task<bool> StartSessionInternalAsync(
        string sessionName,
        GameMode mode,
        SessionStartupContext startupContext,
        string initialSceneName,
        RaidLaunchContext launchContext = null,
        PendingLoadoutReservation loadoutReservation = null,
        CancellationToken cancellationToken = default)
    {
        if (!startupContext.IsValid)
            throw new ArgumentException("Invalid startup context provided to session launcher.");

        if (launchContext == null)
            throw new ArgumentNullException(nameof(launchContext), "Raid startup requires a canonical launch context.");

        if (string.IsNullOrEmpty(sessionName) || string.IsNullOrWhiteSpace(sessionName))
            throw new Exception("Invalid session code. The code cannot be empty or null.");

        // Acepta exclusivamente Host y Client
        if (mode != GameMode.Host && mode != GameMode.Client)
        {
            throw new ArgumentException($"Unsupported game mode: {mode}. Only GameMode.Host and GameMode.Client are supported.");
        }

        int initialSceneBuildIndex = -1;
        if (!string.IsNullOrWhiteSpace(initialSceneName))
        {
            initialSceneBuildIndex = NetworkSceneBuildIndexResolver.Resolve(initialSceneName);
            if (initialSceneBuildIndex < 0)
            {
                throw new ArgumentException(
                    $"Raid scene '{initialSceneName}' is not enabled in build settings.",
                    nameof(initialSceneName));
            }
        }

        // Valida el prefab del coordinador en modo Host, sólo si se requiere bootstrap del Host
        if (mode == GameMode.Host && startupContext.ShouldExecuteHostBootstrap && !_matchControllerPrefab.IsValid)
        {
            throw new InvalidOperationException("[FusionSessionLauncher] Match coordinator prefab is invalid or missing.");
        }

        var profileId = LocalProfileProvider.GetOrCreateLocalProfile();
        var joinData = new PlayerJoinData(profileId);
        byte[] token;
        if (launchContext != null)
        {
            if (loadoutReservation == null)
            {
                throw new ArgumentException("A coordinated raid requires a reserved loadout.", nameof(loadoutReservation));
            }

            ApplicationStashContext profileContext =
                FindAnyObjectByType<ApplicationStashContext>();
            if (profileContext?.Store == null ||
                profileContext.Store.ProfileId != profileId ||
                !RaidSessionRules.ContainsProfile(launchContext.ParticipantProfileIds, profileId) ||
                !profileContext.Store.TryGetCharacterAttributeState(
                    out CharacterAttributeState characterAttributes) ||
                !RaidAdmissionData.TryCreate(
                    launchContext.RaidCode,
                    profileId,
                    loadoutReservation,
                    characterAttributes,
                    profileContext.Store.GetLevel(),
                    profileContext.Store.GetCurrentExperience(),
                    profileContext.Store.GetLastAppliedProgressionResultSequence(),
                    out RaidAdmissionData admissionData) ||
                !RaidAdmissionDataCodec.TryEncode(admissionData, out token))
            {
                throw new ArgumentException("The local profile is not admitted by the supplied raid manifest.");
            }
        }
        else if (!PlayerJoinDataCodec.TryEncode(joinData, out token))
        {
            throw new ArgumentException($"Invalid or unsupported local profile id.");
        }

        if (_isStarting || _runner != null)
        {
            LastStartShutdownReason = ShutdownReason.Error;
            return false;
        }

        _isStarting = true;
        LastStartShutdownReason = ShutdownReason.Ok;

        try
        {
            if (!NetworkRunnerFactory.TryCreate(
                mode,
                startupContext,
                _raidPlayerPrefab,
                _raidParticipantPrefab,
                _enemyPrefabs,
                in joinData,
                token,
                launchContext,
                loadoutReservation,
                this,
                out var composition))
            {
                LastStartShutdownReason = ShutdownReason.Error;
                Debug.LogError("[FusionSessionLauncher] Failed to create runner composition via factory.", this);
                return false;
            }

            _runnerObject = composition.RunnerObject;
            _runner = composition.Runner;
            _spawnManager = composition.SpawnManager;
            _sanctuaryAssignmentService = composition.SanctuaryAssignmentService;

            _shutdownListener = _runnerObject.AddComponent<LauncherShutdownListener>();
            _shutdownListener.Initialize(_runner, HandleRunnerShutdown);

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = RaidPlayerCapacity,
                ConnectionToken = token,
                SceneManager = composition.SceneManager
            };

            if (initialSceneBuildIndex >= 0)
            {
                var sceneInfo = new NetworkSceneInfo();
                sceneInfo.AddSceneRef(
                    SceneRef.FromIndex(initialSceneBuildIndex),
                    LoadSceneMode.Single);
                args.Scene = sceneInfo;
            }

            if (mode == GameMode.Client)
            {
                args.EnableClientSessionCreation = false;
            }
            else if (mode == GameMode.Host)
            {
                // Create host session initially closed and hidden to prevent race conditions
                args.IsOpen = false;
                args.IsVisible = false;
            }

            StartGameResult result = await _runner.StartGame(args);
            cancellationToken.ThrowIfCancellationRequested();

            if (!result.Ok)
            {
                LastStartShutdownReason = result.ShutdownReason;
                if (mode != GameMode.Client || launchContext == null ||
                    !IsSessionAvailabilityPending(result.ShutdownReason))
                {
                    Debug.LogError(
                        $"Fusion failed to start. Reason: {result.ShutdownReason}",
                        this);
                }

                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            LastStartShutdownReason = ShutdownReason.Ok;

            if (initialSceneBuildIndex >= 0 &&
                !await _shutdownListener.WaitForInitialSceneAsync(cancellationToken))
            {
                Debug.LogError("[FusionSessionLauncher] Gameplay scene did not finish loading on the active runner.", this);
                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            Debug.Log(
                $"Fusion session started. " +
                $"Session: {_runner.SessionInfo.Name}. " +
                $"Peer: {(_runner.IsServer ? "Host" : "Client")}.",
                this);

            if (_runner.IsServer)
            {
                if (startupContext.ShouldExecuteHostBootstrap)
                {
                    string generationId = launchContext != null
                        ? launchContext.RaidCode.RaidId
                        : Guid.NewGuid().ToString("N");
                    NetworkObject coordObj = _runner.Spawn(
                        _matchControllerPrefab,
                        flags: NetworkSpawnFlags.DontDestroyOnLoad);
                    if (coordObj == null)
                    {
                        Debug.LogError("[FusionSessionLauncher] Host bootstrap failed: Could not spawn match coordinator prefab.");
                        await ShutdownAndDestroyRunnerAsync();
                        return false;
                    }

                    _matchController = coordObj.GetComponent<NetworkMatchController>();
                    if (_matchController == null)
                    {
                        Debug.LogError("[FusionSessionLauncher] Host bootstrap failed: NetworkMatchController component not found on spawned prefab.");
                        await ShutdownAndDestroyRunnerAsync();
                        return false;
                    }

                    _matchController.InitializeRaidGeneration(generationId);

                    // 2. Bind coordinator to NetworkSpawnManager
                    if (!_spawnManager.BindMatchController(_matchController))
                    {
                        Debug.LogError("[FusionSessionLauncher] Host bootstrap failed: Could not bind coordinator to spawn manager.");
                        await ShutdownAndDestroyRunnerAsync();
                        return false;
                    }

                    // 3. Atomically perform Host player bootstrap (admission + spawn attempt)
                    HostBootstrapResult bootstrapResult = _spawnManager.TryBootstrapHost(_runner, _matchController);
                    if (bootstrapResult != HostBootstrapResult.BootstrapCompleted && bootstrapResult != HostBootstrapResult.HostAdmittedSpawnPending)
                    {
                        Debug.LogError($"[FusionSessionLauncher] Host bootstrap failed: {bootstrapResult}.");
                        await ShutdownAndDestroyRunnerAsync();
                        return false;
                    }

                    // 4. Open the session after successful coordinator and Host initialization.
                    _runner.SessionInfo.IsOpen = true;
                    _runner.SessionInfo.IsVisible = false;
                    _matchController.ConfigurePreloadedRaidAdmission(
                        launchContext.ParticipantProfileIds.Count);

                    Debug.Log($"[FusionSessionLauncher] Host bootstrap completed ({bootstrapResult}). Session is now open.");
                }
                else
                {
                    // Host Migration Resume
                    Debug.Log("[FusionSessionLauncher] Host Migration Resume: skipping fresh host bootstrap (coordinator creation & opening session handled by restore).");
                }
            }

            if (launchContext != null &&
                !await WaitForLocalRaidAdmissionAsync(loadoutReservation, cancellationToken))
            {
                Debug.LogError("[FusionSessionLauncher] Local raid admission did not complete with the exact reserved loadout.", this);
                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            LastStartShutdownReason = ShutdownReason.Error;
            await ShutdownAndDestroyRunnerAsync();
            return false;
        }
        catch (Exception ex)
        {
            LastStartShutdownReason = ShutdownReason.Error;
            Debug.LogException(ex, this);
            await ShutdownAndDestroyRunnerAsync();
            throw;
        }
        finally
        {
            _isStarting = false;
        }
    }

    private async Task<bool> WaitForLocalRaidAdmissionAsync(
        PendingLoadoutReservation reservation,
        CancellationToken cancellationToken)
    {
        if (_runner == null || reservation == null)
        {
            return false;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        float deadline = Time.realtimeSinceStartup + LocalAdmissionTimeoutSeconds;
        while (_runner != null && _runner.IsRunning && Time.realtimeSinceStartup < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NetworkObject participantObject = _runner.GetPlayerObject(_runner.LocalPlayer);
            if (participantObject != null &&
                participantObject.TryGetBehaviour(out NetworkRaidParticipant participant) &&
                string.Equals(participant.ProfileId.ToString(), localProfile.Value, StringComparison.Ordinal) &&
                string.Equals(participant.LoadoutReservationId.ToString(), reservation.ReservationId, StringComparison.Ordinal) &&
                participant.TryResolveCurrentAvatar(out NetworkObject avatarObject) &&
                avatarObject.TryGetBehaviour(out PlayerLootReceiver receiver) &&
                avatarObject.TryGetBehaviour(out PlayerWeaponEquipmentNetworkController equipment) &&
                PlayerExpeditionLootSnapshot.TryCapture(
                    receiver,
                    equipment,
                    out PlayerExpeditionLootSnapshot ownership,
                    out _) &&
                MatchesLoadout(reservation.Items, ownership.Inventory) &&
                MatchesPreparedEquipment(reservation.PreparedEquipment, equipment))
            {
                return true;
            }

            await Task.Delay(50, cancellationToken);
        }

        return false;
    }

    private static bool MatchesLoadout(
        System.Collections.Generic.IReadOnlyList<StashItem> expected,
        System.Collections.Generic.IReadOnlyList<LootEntry> actual)
    {
        if (expected == null || actual == null || expected.Count != actual.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.Count; index++)
        {
            bool found = false;
            for (int actualIndex = 0; actualIndex < actual.Count; actualIndex++)
            {
                if (expected[index].LootId == actual[actualIndex].LootId &&
                    expected[index].Amount == actual[actualIndex].Amount)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Confirms that every replicated Equipment slot matches the reserved preparation.</summary>
    private static bool MatchesPreparedEquipment(
        PreparedEquipmentLoadout expected,
        PlayerWeaponEquipmentNetworkController equipment)
    {
        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int index = 0; index < slots.Length; index++)
        {
            LootId actual = equipment.TryGetSlotLoot(slots[index], out LootEntry entry)
                ? entry.LootId
                : default;
            if (actual != expected.Get(slots[index]))
            {
                return false;
            }
        }

        return equipment.ActiveWeaponSlot ==
            (expected.HasWeaponSlot1 ? WeaponSlot.Slot1 : WeaponSlot.Slot2);
    }

    public async Task<bool> ShutdownAndDestroyRunnerAsync()
    {
        NetworkRunner runner = _runner;
        GameObject runnerObject = _runnerObject;
        if (runner == null && runnerObject == null)
        {
            return true;
        }

        _shutdownListener?.Detach();
        _shutdownListener = null;
        bool succeeded = await RunnerShutdownUtility.ShutdownAndDestroyAsync(runner, runnerObject);
        ClearReferencesOnShutdown(runner);
        _hostMigrationSourceRunner = null;
        return succeeded;
    }

    /// <summary>
    /// Marks the current Client runner as the source of an in-progress Host Migration.
    /// </summary>
    internal bool TryBeginHostMigration(NetworkRunner sourceRunner)
    {
        if (sourceRunner == null || _runner != sourceRunner || _hostMigrationSourceRunner != null)
        {
            return false;
        }

        _hostMigrationSourceRunner = sourceRunner;
        Debug.Log("[HM-MULTI] Launcher marked the current Raid runner as migrating.", this);
        return true;
    }

    /// <summary>
    /// Adopts a successfully started and restored Host Migration composition.
    /// </summary>
    internal bool TryAdoptMigratedRunner(
        NetworkRunner sourceRunner,
        in NetworkRunnerFactory.RunnerComposition replacement)
    {
        NetworkMatchController restoredMatchController = replacement.SpawnManager != null
            ? replacement.SpawnManager.MatchController
            : null;
        if (_hostMigrationSourceRunner != sourceRunner || replacement.RunnerObject == null ||
            replacement.Runner == null || replacement.SpawnManager == null ||
            replacement.SanctuaryAssignmentService == null || restoredMatchController == null)
        {
            return false;
        }

        _shutdownListener?.Detach();
        _runnerObject = replacement.RunnerObject;
        _runner = replacement.Runner;
        _spawnManager = replacement.SpawnManager;
        _matchController = restoredMatchController;
        _sanctuaryAssignmentService = replacement.SanctuaryAssignmentService;
        _shutdownListener = _runnerObject.AddComponent<LauncherShutdownListener>();
        _shutdownListener.Initialize(_runner, HandleRunnerShutdown);
        if (_runner.IsRunning)
        {
            _runner.AddCallbacks(_shutdownListener);
        }
        _hostMigrationSourceRunner = null;

        Debug.Log("[HM-MULTI] Launcher adopted replacement runner and shutdown listener.", this);
        return true;
    }

    /// <summary>
    /// Routes a failed Host Migration back through the launcher's normal shutdown observer.
    /// </summary>
    internal void ReportHostMigrationFailure(NetworkRunner sourceRunner, string reason)
    {
        if (_hostMigrationSourceRunner != sourceRunner)
        {
            return;
        }

        _hostMigrationSourceRunner = null;
        Debug.LogError($"[HM-MULTI] Host Migration failed: {reason}", this);
        RunnerShutdownObserved?.Invoke(sourceRunner, ShutdownReason.Error);
    }

    public void ClearReferencesOnShutdown(NetworkRunner shutdownRunner)
    {
        if (_runner != shutdownRunner)
        {
            return;
        }

        _runner = null;
        _runnerObject = null;
        _spawnManager = null;
        _matchController = null;
        _sanctuaryAssignmentService = null;
        _shutdownListener = null;
    }

    private void HandleRunnerShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (_runner != runner)
        {
            return;
        }

        Debug.Log(
            $"[HM-MULTI] FusionSessionLauncher observed runner shutdown. " +
            $"Reason={shutdownReason}.",
            this);
        ClearReferencesOnShutdown(runner);
        RunnerShutdownObserved?.Invoke(runner, shutdownReason);
    }
}
