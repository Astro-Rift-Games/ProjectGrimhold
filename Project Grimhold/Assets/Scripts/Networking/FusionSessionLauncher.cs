using Fusion;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FusionSessionLauncher : MonoBehaviour, ISessionRunnerOwner
{
    [Header("Session")]
    [SerializeField]
    private string _sessionName;

    [SerializeField, Min(1)]
    private int _maxPlayers = 4;

    [Header("Spawning Configuration")]
    [SerializeField]
    private PlayerClassCatalog _playerClassCatalog;

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

    public NetworkRunner Runner => _runner;
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

    public Task<bool> StartSessionAsync(string sessionName, GameMode mode, PlayerClassId selectedClass)
    {
        return StartSessionInternalAsync(
            sessionName,
            mode,
            selectedClass,
            SessionStartupContext.FreshSession,
            null);
    }

    public Task<bool> StartCoordinatedSessionAsync(
        string sessionName,
        GameMode mode,
        PlayerClassId selectedClass,
        string gameplaySceneName,
        RaidLaunchManifest raidManifest = default,
        PendingLoadoutReservation loadoutReservation = null)
    {
        return StartSessionInternalAsync(
            sessionName,
            mode,
            selectedClass,
            SessionStartupContext.FreshSession,
            gameplaySceneName,
            raidManifest,
            loadoutReservation);
    }

    private async Task<bool> StartSessionInternalAsync(
        string sessionName,
        GameMode mode,
        PlayerClassId selectedClass,
        SessionStartupContext startupContext,
        string initialSceneName,
        RaidLaunchManifest raidManifest = default,
        PendingLoadoutReservation loadoutReservation = null)
    {
        if (!startupContext.IsValid)
            throw new ArgumentException("Invalid startup context provided to session launcher.");

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
        var joinData = new PlayerJoinData(selectedClass, profileId);
        byte[] token;
        if (raidManifest.IsValid)
        {
            if (loadoutReservation == null)
            {
                throw new ArgumentException("A coordinated raid requires a reserved loadout.", nameof(loadoutReservation));
            }

            var reservedLoadout = new System.Collections.Generic.List<LootEntry>(loadoutReservation.Items.Count);
            for (int index = 0; index < loadoutReservation.Items.Count; index++)
            {
                StashItem item = loadoutReservation.Items[index];
                reservedLoadout.Add(new LootEntry(item.LootId, item.Amount));
            }

            var admissionData = raidManifest.RaidCode.IsValid
                ? new RaidAdmissionData(
                    raidManifest.RaidCode,
                    profileId,
                    selectedClass,
                    loadoutReservation.ReservationId,
                    reservedLoadout)
                : new RaidAdmissionData(
                    raidManifest.RaidId,
                    raidManifest.AccessSecret,
                    profileId,
                    selectedClass,
                    loadoutReservation.ReservationId,
                    reservedLoadout);
            if (!raidManifest.Contains(profileId) || !RaidAdmissionDataCodec.TryEncode(admissionData, out token))
            {
                throw new ArgumentException("The local profile is not admitted by the supplied raid manifest.");
            }
        }
        else if (!PlayerJoinDataCodec.TryEncode(joinData, out token))
        {
            throw new ArgumentException($"Invalid or unsupported selected class: {selectedClass}");
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
                _playerClassCatalog,
                _raidParticipantPrefab,
                _enemyPrefabs,
                in joinData,
                token,
                raidManifest,
                loadoutReservation,
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
                PlayerCount = _maxPlayers,
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

            if (!result.Ok)
            {
                LastStartShutdownReason = result.ShutdownReason;
                if (mode != GameMode.Client || !raidManifest.IsValid ||
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
                !await _shutdownListener.WaitForInitialSceneAsync())
            {
                Debug.LogError("[FusionSessionLauncher] Gameplay scene did not finish loading on the active runner.", this);
                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            Debug.Log(
                $"Fusion session started. " +
                $"Session: {_runner.SessionInfo.Name}. " +
                $"Peer: {(_runner.IsServer ? "Host" : "Client")}.",
                this);

            if (_runner.IsServer)
            {
                if (startupContext.ShouldExecuteHostBootstrap)
                {
                    string generationId = raidManifest.IsValid
                        ? raidManifest.RaidId
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
                    _runner.SessionInfo.IsVisible = !raidManifest.IsValid;

                    if (raidManifest.IsValid)
                    {
                        if (!raidManifest.AllowsCodeAdmission)
                        {
                            _matchController.ConfigurePreloadedRaidAdmission(
                                raidManifest.AdmittedProfiles.Count);
                        }
                    }

                    Debug.Log($"[FusionSessionLauncher] Host bootstrap completed ({bootstrapResult}). Session is now open.");
                }
                else
                {
                    // Host Migration Resume
                    Debug.Log("[FusionSessionLauncher] Host Migration Resume: skipping fresh host bootstrap (coordinator creation & opening session handled by restore).");
                }
            }

            if (raidManifest.IsValid &&
                !await WaitForLocalRaidAdmissionAsync(loadoutReservation))
            {
                Debug.LogError("[FusionSessionLauncher] Local raid admission did not complete with the exact reserved loadout.", this);
                await ShutdownAndDestroyRunnerAsync();
                return false;
            }

            return true;
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

    private async Task<bool> WaitForLocalRaidAdmissionAsync(PendingLoadoutReservation reservation)
    {
        if (_runner == null || reservation == null)
        {
            return false;
        }

        ProfileId localProfile = LocalProfileProvider.GetOrCreateLocalProfile();
        while (_runner != null && _runner.IsRunning)
        {
            NetworkObject participantObject = _runner.GetPlayerObject(_runner.LocalPlayer);
            if (participantObject != null &&
                participantObject.TryGetBehaviour(out NetworkRaidParticipant participant) &&
                string.Equals(participant.ProfileId.ToString(), localProfile.Value, StringComparison.Ordinal) &&
                string.Equals(participant.LoadoutReservationId.ToString(), reservation.ReservationId, StringComparison.Ordinal) &&
                participant.TryResolveCurrentAvatar(out NetworkObject avatarObject) &&
                avatarObject.TryGetBehaviour(out PlayerLootReceiver receiver) &&
                receiver.TryGetLootContent(out System.Collections.Generic.IReadOnlyList<LootEntry> actual) &&
                MatchesLoadout(reservation.Items, actual))
            {
                return true;
            }

            await Task.Delay(50);
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
        return succeeded;
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

        ClearReferencesOnShutdown(runner);
        RunnerShutdownObserved?.Invoke(runner, shutdownReason);
    }
}
