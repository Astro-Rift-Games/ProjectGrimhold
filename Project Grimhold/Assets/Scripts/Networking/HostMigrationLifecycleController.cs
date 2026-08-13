using System;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

internal enum HostMigrationReplacementRole
{
    Host,
    Client
}

/// <summary>
/// Orchestrates the recovery-only replacement runner lifecycle. The replacement
/// Host owns snapshot restore and authoritative roster sealing; a replacement
/// Client only waits for and adopts its replicated local mapping.
/// </summary>
[DisallowMultipleComponent]
public sealed class HostMigrationLifecycleController : NetworkRunnerCallbacksAdapter
{
    internal static readonly TimeSpan MigrationLifecycleBudget = TimeSpan.FromSeconds(65);
    internal static readonly TimeSpan RestoreBudget = TimeSpan.FromSeconds(30);

    private NetworkRunner _associatedRunner;
    private PlayerClassCatalog _playerClassCatalog;
    private NetworkPrefabRef _raidParticipantPrefab;
    private NetworkPrefabRef[] _enemyPrefabs;
    private PlayerJoinData _joinData;
    private byte[] _connectionToken;
    private RaidLaunchContext _launchContext;
    private FusionSessionLauncher _runnerOwner;
    private CancellationTokenSource _migrationCancellation;
    private bool _isMigrating;

    public void Initialize(
        NetworkRunner runner,
        PlayerClassCatalog playerClassCatalog,
        NetworkPrefabRef raidParticipantPrefab,
        NetworkPrefabRef[] enemyPrefabs,
        in PlayerJoinData joinData,
        byte[] connectionToken,
        RaidLaunchContext launchContext,
        FusionSessionLauncher runnerOwner)
    {
        _associatedRunner = runner;
        _playerClassCatalog = playerClassCatalog;
        _raidParticipantPrefab = raidParticipantPrefab;
        _enemyPrefabs = enemyPrefabs;
        _joinData = joinData;
        _connectionToken = connectionToken;
        _launchContext = launchContext;
        _runnerOwner = runnerOwner;
    }

    public override void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        if (runner != _associatedRunner)
        {
            return;
        }

        if (_isMigrating)
        {
            Debug.LogWarning("[HM-MULTI] Duplicate OnHostMigration ignored.", this);
            return;
        }

        NetworkSpawnManager spawnManager = runner.GetComponent<NetworkSpawnManager>();
        NetworkMatchController matchController = spawnManager != null
            ? spawnManager.MatchController
            : null;
        if (_runnerOwner == null || _launchContext == null ||
            !_launchContext.RaidCode.IsValid ||
            !_launchContext.LocalProfileId.IsValid ||
            !_launchContext.HostProfileId.IsValid ||
            !RaidSessionRules.ContainsProfile(
                _launchContext.ParticipantProfileIds,
                _launchContext.LocalProfileId) ||
            matchController == null ||
            matchController.Phase != NetworkMatchController.MatchPhase.InProgress)
        {
            Debug.LogError(
                "[HM-MULTI] Host Migration rejected because the source Raid is not a valid InProgress generation.",
                this);
            return;
        }

        if (!_runnerOwner.TryBeginHostMigration(runner))
        {
            Debug.LogError("[HM-MULTI] Launcher rejected migration ownership.", this);
            return;
        }

        _migrationCancellation?.Cancel();
        _migrationCancellation?.Dispose();
        _migrationCancellation = new CancellationTokenSource();
        _isMigrating = true;
        Debug.Log("[HM-MULTI] Recovery-only migration lifecycle started.", this);
        _ = HandleHostMigrationAsync(
            runner,
            hostMigrationToken,
            _migrationCancellation.Token);
    }

    internal static bool TryResolveReplacementRole(
        GameMode gameMode,
        bool isServer,
        out HostMigrationReplacementRole role)
    {
        if (gameMode == GameMode.Host && isServer)
        {
            role = HostMigrationReplacementRole.Host;
            return true;
        }

        if (gameMode == GameMode.Client && !isServer)
        {
            role = HostMigrationReplacementRole.Client;
            return true;
        }

        role = default;
        return false;
    }

    private async Task HandleHostMigrationAsync(
        NetworkRunner oldRunner,
        HostMigrationToken token,
        CancellationToken cancellationToken)
    {
        NetworkRunnerFactory.RunnerComposition replacement = default;
        GameObject oldRunnerObject = oldRunner != null ? oldRunner.gameObject : null;
        long lifecycleDeadline = CreateDeadline(MigrationLifecycleBudget);
        long restoreDeadline = Math.Min(
            lifecycleDeadline,
            CreateDeadline(RestoreBudget));
        try
        {
            Scene oldScene = oldRunner.SceneManager.MainRunnerScene;
            int oldSceneBuildIndex = oldScene.buildIndex;
            if (!oldScene.IsValid() || !oldScene.isLoaded || oldSceneBuildIndex < 0 ||
                (token.GameMode != GameMode.Host && token.GameMode != GameMode.Client))
            {
                throw new InvalidOperationException(
                    "The Host Migration token or source Raid scene is invalid.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await AwaitWithinDeadline(
                oldRunner.Shutdown(
                    destroyGameObject: false,
                    shutdownReason: ShutdownReason.HostMigration),
                restoreDeadline,
                cancellationToken,
                "source runner shutdown");

            Scene temporaryScene = SceneManager.CreateScene(
                $"HostMigrationTemp_{Guid.NewGuid():N}");
            if (!temporaryScene.IsValid() || !temporaryScene.isLoaded ||
                !SceneManager.SetActiveScene(temporaryScene))
            {
                throw new InvalidOperationException(
                    "Failed to prepare the temporary migration scene.");
            }

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(oldScene);
            if (unloadOperation != null)
            {
                await AwaitAsyncOperationWithinDeadline(
                    unloadOperation,
                    restoreDeadline,
                    cancellationToken,
                    "source Raid scene unload");
            }

            if (oldScene.isLoaded || !NetworkRunnerFactory.TryCreate(
                    token.GameMode,
                    SessionStartupContext.HostMigrationResume,
                    _playerClassCatalog,
                    _raidParticipantPrefab,
                    _enemyPrefabs,
                    in _joinData,
                    _connectionToken,
                    _launchContext,
                    null,
                    _runnerOwner,
                    out replacement))
            {
                throw new InvalidOperationException(
                    "Failed to create the replacement runner composition.");
            }

            var sceneInfo = new NetworkSceneInfo();
            if (sceneInfo.AddSceneRef(
                    SceneRef.FromIndex(oldSceneBuildIndex),
                    LoadSceneMode.Single) < 0)
            {
                throw new InvalidOperationException(
                    "Failed to add the Raid scene to the replacement runner.");
            }

            var startGameArgs = new StartGameArgs
            {
                GameMode = token.GameMode,
                HostMigrationToken = token,
                HostMigrationResume = token.GameMode == GameMode.Host
                    ? replacement.SnapshotRestorer.HostMigrationResumeCallback
                    : null,
                ConnectionToken = _connectionToken,
                PlayerCount = RaidSessionRules.MaxParticipants,
                Scene = sceneInfo,
                IsOpen = true,
                IsVisible = false
            };

            Debug.Log(
                $"[HM-MULTI] Starting replacement runner as {token.GameMode}.",
                this);
            StartGameResult result = await AwaitWithinDeadline(
                replacement.Runner.StartGame(startGameArgs),
                restoreDeadline,
                cancellationToken,
                "replacement start and snapshot restore");
            if (!result.Ok)
            {
                throw new InvalidOperationException(
                    $"Replacement StartGame failed with {result.ShutdownReason}.");
            }

            if (!TryResolveReplacementRole(
                    replacement.Runner.GameMode,
                    replacement.Runner.IsServer,
                    out HostMigrationReplacementRole role))
            {
                throw new InvalidOperationException(
                    $"Invalid replacement authority combination: " +
                    $"GameMode={replacement.Runner.GameMode}, IsServer={replacement.Runner.IsServer}.");
            }

            TimeSpan remaining = GetRemaining(lifecycleDeadline);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    "The local 65 second migration lifecycle budget expired.");
            }

            if (role == HostMigrationReplacementRole.Host)
            {
                TimeSpan restoreRemaining = GetRemaining(restoreDeadline);
                if (restoreRemaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException(
                        "Snapshot restore exceeded the local 30 second restore budget.");
                }

                await replacement.SpawnManager.WaitForHostMigrationRecoveryWindowOpenAsync(
                    restoreRemaining,
                    cancellationToken);
                remaining = GetRemaining(lifecycleDeadline);
                NetworkSpawnManager.HostMigrationCompletionResult completion =
                    await replacement.SpawnManager.WaitForHostMigrationCompletionAsync(
                        remaining,
                        cancellationToken);
                if (!completion.Succeeded)
                {
                    throw completion.Status ==
                        NetworkSpawnManager.HostMigrationCompletionStatus.Timeout
                            ? new TimeoutException(completion.Details)
                            : new InvalidOperationException(completion.Details);
                }

                ValidateHostCompletion(replacement);
            }
            else
            {
                await replacement.SpawnManager.WaitForLocalHostMigrationRecoveryAsync(
                    _launchContext.LocalProfileId,
                    remaining,
                    cancellationToken);
            }

            if (!_runnerOwner.TryAdoptMigratedRunner(oldRunner, in replacement))
            {
                throw new InvalidOperationException(
                    "FusionSessionLauncher rejected replacement runner adoption.");
            }

            Debug.Log(
                $"[HM-MULTI] Replacement adopted as RecoveredAs{role}.",
                this);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            await CleanupFailedReplacementAsync(
                replacement.Runner,
                replacement.RunnerObject);
            _runnerOwner?.ReportHostMigrationFailure(oldRunner, exception.Message);
        }
        finally
        {
            _isMigrating = false;
            if (oldRunnerObject != null)
            {
                Destroy(oldRunnerObject);
            }
        }
    }

    private static void ValidateHostCompletion(
        in NetworkRunnerFactory.RunnerComposition replacement)
    {
        if (replacement.Runner == null || !replacement.Runner.IsRunning ||
            !replacement.Runner.IsServer)
        {
            throw new InvalidOperationException(
                "RecoveredAsHost runner is not an active server.");
        }

        NetworkMatchController matchController = replacement.SpawnManager.MatchController;
        if (matchController == null)
        {
            throw new InvalidOperationException(
                "RecoveredAsHost has no restored MatchController.");
        }

        bool hasRaiding = replacement.SpawnManager.HasRaidingParticipants;
        if (hasRaiding &&
            matchController.Phase != NetworkMatchController.MatchPhase.InProgress)
        {
            throw new InvalidOperationException(
                $"A sealed roster with Raiding participants cannot be in {matchController.Phase}.");
        }

        if (!hasRaiding &&
            matchController.Phase != NetworkMatchController.MatchPhase.InProgress &&
            !((matchController.Phase == NetworkMatchController.MatchPhase.Closing ||
               matchController.Phase == NetworkMatchController.MatchPhase.Finished) &&
              matchController.ClosureReason == RaidClosureReason.NaturalCompletion))
        {
            throw new InvalidOperationException(
                "A post-recovery phase change is valid only for NaturalCompletion with zero Raiding participants.");
        }
    }

    private static long CreateDeadline(TimeSpan budget)
    {
        return Stopwatch.GetTimestamp() +
               (long)(budget.TotalSeconds * Stopwatch.Frequency);
    }

    private static TimeSpan GetRemaining(long deadline)
    {
        long ticks = deadline - Stopwatch.GetTimestamp();
        return ticks <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);
    }

    private static async Task<T> AwaitWithinDeadline<T>(
        Task<T> operation,
        long deadline,
        CancellationToken cancellationToken,
        string stage)
    {
        TimeSpan remaining = GetRemaining(deadline);
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException($"Host Migration {stage} exceeded its budget.");
        }

        Task delay = Task.Delay(remaining, cancellationToken);
        Task completed = await Task.WhenAny(operation, delay);
        cancellationToken.ThrowIfCancellationRequested();
        if (completed != operation)
        {
            throw new TimeoutException($"Host Migration {stage} exceeded its budget.");
        }

        return await operation;
    }

    private static async Task AwaitWithinDeadline(
        Task operation,
        long deadline,
        CancellationToken cancellationToken,
        string stage)
    {
        TimeSpan remaining = GetRemaining(deadline);
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException($"Host Migration {stage} exceeded its budget.");
        }

        Task delay = Task.Delay(remaining, cancellationToken);
        Task completed = await Task.WhenAny(operation, delay);
        cancellationToken.ThrowIfCancellationRequested();
        if (completed != operation)
        {
            throw new TimeoutException($"Host Migration {stage} exceeded its budget.");
        }

        await operation;
    }

    private static async Task AwaitAsyncOperationWithinDeadline(
        AsyncOperation operation,
        long deadline,
        CancellationToken cancellationToken,
        string stage)
    {
        while (!operation.isDone)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GetRemaining(deadline) <= TimeSpan.Zero)
            {
                throw new TimeoutException($"Host Migration {stage} exceeded its budget.");
            }

            await Task.Yield();
        }
    }

    private async Task CleanupFailedReplacementAsync(
        NetworkRunner replacementRunner,
        GameObject replacementObject)
    {
        if (replacementRunner != null && replacementRunner.IsRunning)
        {
            try
            {
                await replacementRunner.Shutdown(
                    destroyGameObject: false,
                    shutdownReason: ShutdownReason.Error);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        if (replacementObject == null)
        {
            return;
        }

        Destroy(replacementObject);
        while (replacementObject != null)
        {
            await Task.Yield();
        }
    }

    private void OnDestroy()
    {
        _migrationCancellation?.Cancel();
        _migrationCancellation?.Dispose();
        _migrationCancellation = null;
    }
}
