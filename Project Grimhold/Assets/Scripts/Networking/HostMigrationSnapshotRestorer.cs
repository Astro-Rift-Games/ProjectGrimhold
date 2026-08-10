using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class HostMigrationSnapshotRestorer : MonoBehaviour
{
    private NetworkRunner _runner;
    private SessionStartupContext _startupContext;
    private NetworkSpawnManager _spawnManager;

    private bool _hasExecuted = false;
    private NetworkObject _objectBeingRestored;
    private readonly Dictionary<NetworkId, NetworkObject> _restoredDynamicObjects = new Dictionary<NetworkId, NetworkObject>();
    private readonly Dictionary<NetworkId, NetworkObject> _restoredSceneObjects = new Dictionary<NetworkId, NetworkObject>();
    private readonly List<NetworkObject> _spawnedThisExecution = new List<NetworkObject>();
    private readonly HashSet<NetworkObject> _allRestoredObjects = new HashSet<NetworkObject>();
    private readonly Dictionary<PlayerRef, NetworkObject> _restoredPlayerObjects = new Dictionary<PlayerRef, NetworkObject>();
    private readonly Dictionary<PlayerRef, NetworkObject> _pendingReconnectPlayerObjects = new Dictionary<PlayerRef, NetworkObject>();

    public IReadOnlyDictionary<PlayerRef, NetworkObject> GetRestoredPlayerObjects() => _restoredPlayerObjects;
    public IReadOnlyDictionary<PlayerRef, NetworkObject> GetPendingReconnectPlayerObjects() => _pendingReconnectPlayerObjects;

    public bool IsRestoringObject(NetworkObject networkObject)
    {
        return networkObject != null &&
               networkObject.Runner == _runner &&
               (_objectBeingRestored == networkObject || _allRestoredObjects.Contains(networkObject));
    }

    public void Initialize(NetworkRunner runner, SessionStartupContext context, NetworkSpawnManager spawnManager)
    {
        _runner = runner;
        _startupContext = context;
        _spawnManager = spawnManager;
    }

    public void HostMigrationResumeCallback(NetworkRunner runner)
    {
        if (runner == null || runner != _runner)
            return;

        if (_hasExecuted)
        {
            Debug.LogError("[HostMigrationSnapshotRestorer] HostMigrationResumeCallback invoked more than once. Rejecting reentrancy.");
            return;
        }

        if (_spawnManager == null)
        {
            Debug.LogError("[HostMigrationSnapshotRestorer] _spawnManager is null during HostMigrationResumeCallback.");
            return;
        }

        if (!_startupContext.IsValid || _startupContext.Mode != SessionStartupMode.HostMigrationResume || !runner.IsRunning || !runner.IsServer || !runner.IsResume)
        {
            Debug.LogError("[HostMigrationSnapshotRestorer] Invalid state, context, not server, or not a resume runner during HostMigrationResumeCallback.");
            _hasExecuted = true;
            _spawnManager.ReportSnapshotRestoreResult(false);
            return;
        }

        if (_startupContext.ShouldExecuteInitialSceneBootstrap)
        {
            Debug.LogWarning("[HostMigrationSnapshotRestorer] Ignored HostMigrationResumeCallback because context does not require migration.");
            _hasExecuted = true;
            _spawnManager.ReportSnapshotRestoreResult(false);
            return;
        }

        _hasExecuted = true;
        _hasExecuted = true;
        _restoredDynamicObjects.Clear();
        _restoredSceneObjects.Clear();
        _spawnedThisExecution.Clear();
        _allRestoredObjects.Clear();
        _restoredPlayerObjects.Clear();
        _pendingReconnectPlayerObjects.Clear();

        Debug.Log("[HostMigrationSnapshotRestorer] Executing snapshot restoration...");
        try
        {
            LogDiagnosticsBeforeRestore(runner);
            RestoreDynamicObjects(runner);
            LogDiagnosticsAfterRestore();
            RestoreSceneObjects(runner);
            ApplyEntityIdFixups();

            if (!ValidateMatchController(out string validationError))
            {
                Debug.LogWarning($"[HostMigrationSnapshotRestorer] Validation failed: {validationError}");
                Rollback();
                _spawnManager.ReportSnapshotRestoreResult(false);
                AbortAndReturnToMainMenu(runner);
                return;
            }

            RestorePlayerAuthorities(runner);

            _spawnManager.ReportSnapshotRestoreResult(true, GetRestoredPlayerObjects(), GetPendingReconnectPlayerObjects());
            Debug.Log("[HostMigrationSnapshotRestorer] Snapshot restoration finished successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            Rollback();
            _spawnManager.ReportSnapshotRestoreResult(false);
            AbortAndReturnToMainMenu(runner);
        }
    }

    private void Rollback()
    {
        Debug.LogWarning("[HostMigrationSnapshotRestorer] Rolling back spawned dynamic objects due to failed restore...");
        for (int i = _spawnedThisExecution.Count - 1; i >= 0; i--)
        {
            var no = _spawnedThisExecution[i];
            if (no != null && no.Runner == _runner && _runner.IsRunning && _runner.IsServer)
            {
                _runner.Despawn(no);
            }
        }
        _spawnedThisExecution.Clear();
        _spawnedThisExecution.Clear();
        _allRestoredObjects.Clear();
        _restoredDynamicObjects.Clear();
        _restoredSceneObjects.Clear();
        _restoredPlayerObjects.Clear();
        _pendingReconnectPlayerObjects.Clear();
    }

    private void AbortAndReturnToMainMenu(NetworkRunner runner)
    {
        Debug.LogWarning("[HostMigrationSnapshotRestorer] Aborting match and returning to Main Menu.");
        if (runner != null && runner.IsRunning)
        {
            runner.Shutdown(shutdownReason: ShutdownReason.Error);
        }
        SceneManager.LoadScene("MainMenu");
    }

    public bool TryGetRestoredDynamicObject(NetworkId previousId, out NetworkObject restoredObject)
    {
        restoredObject = null;
        if (!previousId.IsValid) return false;
        
        if (_restoredDynamicObjects.TryGetValue(previousId, out var obj))
        {
            if (obj != null && obj.Runner == _runner)
            {
                restoredObject = obj;
                return true;
            }
        }
        return false;
    }

    private int _diagnosticSnapshotDynamicCount = 0;

    private void LogDiagnosticsBeforeRestore(NetworkRunner runner)
    {
        _diagnosticSnapshotDynamicCount = 0;
        foreach (var no in runner.GetResumeSnapshotNetworkObjects())
        {
            if (no == null) continue;
            bool isPlayer = no.TryGetBehaviour<PlayerCharacter>(out _);
            Debug.Log($"[HM-DIAG] Snapshot dynamic #{_diagnosticSnapshotDynamicCount}: oldId={no.Id}, name={no.gameObject.name}, isPlayer={isPlayer}, inputAuthority={no.InputAuthority}");
            _diagnosticSnapshotDynamicCount++;
        }

        foreach (var pair in runner.GetResumeSnapshotNetworkObjectPlayerObjects())
        {
            Debug.Log($"[HM-DIAG] Previous PlayerObject: PlayerRef={pair.Key} -> oldNetworkId={pair.Value}");
        }

        foreach (var player in runner.ActivePlayers)
        {
            bool hasPlayerObject = runner.TryGetPlayerObject(player, out NetworkObject existing);
            Debug.Log($"[HM-DIAG] Before manual restore: PlayerRef={player}, hasPlayerObject={hasPlayerObject}, networkId={(hasPlayerObject && existing != null ? existing.Id.ToString() : "N/A")}");
        }
    }

    private void LogDiagnosticsAfterRestore()
    {
        int playerObjectCount = 0;
        foreach (var obj in _restoredDynamicObjects.Values)
        {
            if (obj != null && obj.TryGetBehaviour<PlayerCharacter>(out _))
            {
                playerObjectCount++;
            }
        }
        
        int previousPlayerMappingCount = 0;
        foreach (var _ in _runner.GetResumeSnapshotNetworkObjectPlayerObjects())
        {
            previousPlayerMappingCount++;
        }

        Debug.Log($"[HM-DIAG] Restore Summary: snapshot count={_diagnosticSnapshotDynamicCount}, restored count={_restoredDynamicObjects.Count}, players in restored dynamic={playerObjectCount}, previous PlayerObject mapping count={previousPlayerMappingCount}");
    }

    private void RestoreDynamicObjects(NetworkRunner runner)
    {
        foreach (var resumeNO in runner.GetResumeSnapshotNetworkObjects())
        {
            if (resumeNO == null)
                throw new InvalidOperationException("Fusion delivered a null dynamic NetworkObject in snapshot.");
            if (!resumeNO.Id.IsValid)
                throw new InvalidOperationException("Fusion delivered a dynamic NetworkObject with invalid Id in snapshot.");
            if (_restoredDynamicObjects.ContainsKey(resumeNO.Id))
                throw new InvalidOperationException($"Duplicate old NetworkId {resumeNO.Id} in dynamic snapshot.");

            bool hasTrsp = resumeNO.TryGetBehaviour<NetworkTRSP>(out var trsp);
            Vector3 position = hasTrsp ? trsp.Data.Position : Vector3.zero;
            Quaternion rotation = hasTrsp ? trsp.Data.Rotation : Quaternion.identity;
            
            NetworkId oldId = resumeNO.Id;
            
            NetworkObject newObject = null;
            try
            {
                newObject = runner.Spawn(resumeNO, position, rotation, onBeforeSpawned: (r, no) =>
                {
                    _objectBeingRestored = no;
                    no.CopyStateFrom(resumeNO);
                    no.AssignInputAuthority(PlayerRef.None);
                });
            }
            finally
            {
                _objectBeingRestored = null;
            }

            if (newObject == null)
                throw new InvalidOperationException($"Runner.Spawn returned null for restored object {oldId}.");
            if (newObject.Runner != runner)
                throw new InvalidOperationException($"Runner.Spawn returned object {oldId} belonging to another runner.");
            if (!newObject.Id.IsValid)
                throw new InvalidOperationException($"Runner.Spawn returned object {oldId} with invalid new Id.");

            _restoredDynamicObjects.Add(oldId, newObject);
            _spawnedThisExecution.Add(newObject);
            _allRestoredObjects.Add(newObject);

            bool isPlayerChar = newObject.TryGetBehaviour<PlayerCharacter>(out _);
            Debug.Log($"[HM-DIAG] Restored dynamic: oldId={oldId} -> newId={newObject.Id}, name={newObject.gameObject.name}, isPlayer={isPlayerChar}");
        }
    }

    private void RestoreSceneObjects(NetworkRunner runner)
    {
        var oldScene = runner.SceneManager.MainRunnerScene;
        if (!oldScene.IsValid() || !oldScene.isLoaded)
            throw new InvalidOperationException("MainRunnerScene is not valid or loaded during scene object restoration.");

        foreach (var pair in runner.GetResumeSnapshotNetworkSceneObjects())
        {
            NetworkObject currentSceneObject = pair.Item1;
            NetworkObjectHeaderPtr previousState = pair.Item2;

            if (currentSceneObject == null)
                throw new InvalidOperationException("Fusion delivered a null currentSceneObject in scene snapshot.");
            if (!previousState.Id.IsValid)
                throw new InvalidOperationException("Fusion delivered a scene snapshot with invalid previousState Id.");
            if (_restoredSceneObjects.ContainsKey(previousState.Id))
                throw new InvalidOperationException($"Duplicate previousState.Id {previousState.Id} in scene snapshot.");
            if (currentSceneObject.Runner != runner)
                throw new InvalidOperationException($"Scene object {previousState.Id} belongs to another runner.");
            if (!currentSceneObject.Id.IsValid)
                throw new InvalidOperationException($"Scene object {previousState.Id} has invalid current Id.");
            if (currentSceneObject.gameObject.scene != oldScene)
                throw new InvalidOperationException($"Scene object {previousState.Id} belongs to scene {currentSceneObject.gameObject.scene.name}, not {oldScene.name}.");

            currentSceneObject.CopyStateFrom(previousState);
            _restoredSceneObjects.Add(previousState.Id, currentSceneObject);
            _allRestoredObjects.Add(currentSceneObject);
        }
    }

    private void ApplyEntityIdFixups()
    {
        foreach (var obj in _restoredDynamicObjects.Values)
        {
            if (obj.TryGetBehaviour<NetworkProjectile>(out var p))
            {
                EntityId oldOwner = new EntityId(p.GetRestoredOwnerEntityIdValue());
                if (oldOwner.Value != 0)
                {
                    NetworkId oldNetId = new NetworkId { Raw = (uint)oldOwner.Value };
                    if (_restoredDynamicObjects.TryGetValue(oldNetId, out NetworkObject resolvedDynamic))
                    {
                        p.SetRestoredOwnerEntityId(new EntityId((int)resolvedDynamic.Id.Raw));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot resolve old EntityId {oldOwner.Value} for NetworkProjectile owner in dynamic objects.");
                    }
                }
            }

            if (obj.TryGetBehaviour<PlayerExtractionController>(out var e))
            {
                EntityId oldZone = new EntityId(e.GetRestoredActiveZoneIdValue());
                if (oldZone.Value != 0)
                {
                    NetworkId oldNetId = new NetworkId { Raw = (uint)oldZone.Value };
                    if (_restoredSceneObjects.TryGetValue(oldNetId, out NetworkObject resolvedScene))
                    {
                        e.SetRestoredActiveZoneId(new EntityId((int)resolvedScene.Id.Raw));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot resolve old EntityId {oldZone.Value} for PlayerExtractionController active zone in scene objects.");
                    }
                }
            }

            if (obj.TryGetBehaviour<NetworkRaidParticipant>(out var participant))
            {
                NetworkId oldAvatarId = participant.CurrentAvatarId;
                if (oldAvatarId.IsValid)
                {
                    if (_restoredDynamicObjects.TryGetValue(oldAvatarId, out NetworkObject restoredAvatar))
                    {
                        participant.SetRestoredCurrentAvatar(restoredAvatar.Id);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot resolve old avatar {oldAvatarId} for restored raid participant.");
                    }
                }
            }

            if (obj.TryGetBehaviour<RaidAvatarParticipantLink>(out var avatarLink))
            {
                NetworkId oldParticipantId = avatarLink.ParticipantId;
                if (oldParticipantId.IsValid)
                {
                    if (_restoredDynamicObjects.TryGetValue(oldParticipantId, out NetworkObject restoredParticipant))
                    {
                        avatarLink.SetRestoredParticipant(restoredParticipant.Id);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot resolve old participant {oldParticipantId} for restored avatar.");
                    }
                }
            }
        }

        foreach (var obj in _restoredSceneObjects.Values)
        {
            if (obj.TryGetBehaviour<ExtractionSanctuary>(out var s))
            {
                EntityId oldOwner = new EntityId(s.GetRestoredOwnerIdValue());
                if (oldOwner.Value != 0)
                {
                    NetworkId oldNetId = new NetworkId { Raw = (uint)oldOwner.Value };
                    if (_restoredDynamicObjects.TryGetValue(oldNetId, out NetworkObject resolvedDynamic))
                    {
                        s.SetRestoredOwnerId(new EntityId((int)resolvedDynamic.Id.Raw));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot resolve old EntityId {oldOwner.Value} for ExtractionSanctuary owner in dynamic objects.");
                    }
                }
            }
        }
    }

    private bool ValidateMatchController(out string errorMessage)
    {
        NetworkMatchController matchController = null;
        int count = 0;

        foreach (var obj in _restoredDynamicObjects.Values)
        {
            if (obj.TryGetBehaviour<NetworkMatchController>(out var mc))
            {
                matchController = mc;
                count++;
            }
        }

        if (count != 1 || matchController == null)
        {
            errorMessage = $"Expected exactly 1 NetworkMatchController in dynamic objects, found {count}.";
            return false;
        }

        if (matchController != _spawnManager.MatchController)
        {
            errorMessage = "NetworkSpawnManager.MatchController does not match the restored NetworkMatchController.";
            return false;
        }
            
        if (!matchController.HasStateAuthority)
        {
            errorMessage = "Host does not have State Authority over the NetworkMatchController.";
            return false;
        }

        if (matchController.Phase != NetworkMatchController.MatchPhase.InProgress)
        {
            errorMessage = $"MatchPhase is {matchController.Phase}, expected InProgress. Cannot resume safely.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void RestorePlayerAuthorities(NetworkRunner runner)
    {
        var activePlayers = new HashSet<PlayerRef>(runner.ActivePlayers);
        
        foreach (var pair in runner.GetResumeSnapshotNetworkObjectPlayerObjects())
        {
            PlayerRef playerRef = pair.Key;
            NetworkId oldNetworkId = pair.Value;

            if (activePlayers.Contains(playerRef))
            {
                if (_restoredDynamicObjects.TryGetValue(oldNetworkId, out NetworkObject newObject))
                {
                    newObject.AssignInputAuthority(playerRef);
                    runner.SetPlayerObject(playerRef, newObject);

                    if (newObject.TryGetBehaviour(out NetworkRaidParticipant participant) &&
                        participant.TryResolveCurrentAvatar(out NetworkObject avatar) &&
                        (participant.State == RaidParticipantState.Raiding ||
                         participant.State == RaidParticipantState.Extracted))
                    {
                        avatar.AssignInputAuthority(playerRef);
                    }

                    Debug.Log($"[HM-DIAG-TEMP] Checking rebind: playerRef={playerRef}, runner.LocalPlayer={runner.LocalPlayer}, match={playerRef == runner.LocalPlayer}");

                    if (playerRef == runner.LocalPlayer)
                    {
                        Debug.Log($"[HM-DIAG-TEMP] LocalCameraController.Instance is {(LocalCameraController.Instance == null ? "NULL" : "valid")}");

                        if (newObject.TryGetBehaviour(out LocalPlayerCameraBinder cameraBinder))
                        {
                            cameraBinder.TryBindAsLocalPlayer();
                        }

                        if (newObject.TryGetBehaviour(out LocalPlayerHudBinder hudBinder))
                        {
                            hudBinder.TryBindAsLocalPlayer();
                        }
                    }

                    _restoredPlayerObjects.Add(playerRef, newObject);
                    Debug.Log($"[HostMigrationSnapshotRestorer] Restored authority for PlayerRef {playerRef} to new NetworkId {newObject.Id}.");
                }
                else
                {
                    throw new InvalidOperationException($"Cannot resolve old NetworkId {oldNetworkId} for PlayerRef {playerRef} in restored dynamic objects.");
                }
            }
            else
            {
                if (_restoredDynamicObjects.TryGetValue(oldNetworkId, out NetworkObject newObject))
                {
                    _pendingReconnectPlayerObjects.Add(playerRef, newObject);
                    Debug.Log($"[HostMigrationSnapshotRestorer] PlayerRef {playerRef} is no longer active. Tracking as pending reconnect for NetworkId {newObject.Id}.");
                }
                else
                {
                    throw new InvalidOperationException($"Cannot resolve old NetworkId {oldNetworkId} for pending PlayerRef {playerRef} in restored dynamic objects.");
                }
            }
        }
    }
}

public static class HostMigrationRestoreUtility
{
    /// <summary>
    /// Evaluates if the specific NetworkObject associated with the given NetworkBehaviour
    /// is currently being restored by the HostMigrationSnapshotRestorer during a Host Migration.
    /// Used to suppress fresh initializations (e.g. restoring health, transitions) during the HM restore phase,
    /// without suppressing them for newly spawned objects created after the migration completes.
    /// </summary>
    public static bool IsRestoreSpawn(NetworkBehaviour behaviour)
    {
        if (behaviour == null || behaviour.Object == null || behaviour.Runner == null)
            return false;

        var runner = behaviour.Runner;
        
        if (!runner.IsResume)
            return false;

        var restorer = runner.GetComponent<HostMigrationSnapshotRestorer>();
        if (restorer == null)
            return false;

        return restorer.IsRestoringObject(behaviour.Object);
    }
}
