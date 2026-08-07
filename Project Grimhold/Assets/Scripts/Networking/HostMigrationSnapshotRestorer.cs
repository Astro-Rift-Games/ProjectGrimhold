using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HostMigrationSnapshotRestorer : MonoBehaviour
{
    private NetworkRunner _runner;
    private SessionStartupContext _startupContext;
    private NetworkSpawnManager _spawnManager;

    public void Initialize(NetworkRunner runner, SessionStartupContext context, NetworkSpawnManager spawnManager)
    {
        _runner = runner;
        _startupContext = context;
        _spawnManager = spawnManager;
    }

    public void HostMigrationResumeCallback(NetworkRunner runner)
    {
        if (runner != _runner)
            return;

        if (_startupContext.ShouldExecuteInitialSceneBootstrap)
        {
            Debug.LogWarning("[HostMigrationSnapshotRestorer] Ignored HostMigrationResumeCallback because context does not require migration.");
            return;
        }

        Debug.Log("[HostMigrationSnapshotRestorer] Executing snapshot restoration...");
        try
        {
            var dynamicMap = RestoreDynamicObjects(runner);
            var sceneMap = RestoreSceneObjects(runner);

            ApplyEntityIdFixups(runner, dynamicMap, sceneMap);

            _spawnManager.ReportSnapshotRestoreResult(true);
            Debug.Log("[HostMigrationSnapshotRestorer] Snapshot restoration finished successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            _spawnManager.ReportSnapshotRestoreResult(false);
        }
    }

    private Dictionary<NetworkId, NetworkObject> RestoreDynamicObjects(NetworkRunner runner)
    {
        var map = new Dictionary<NetworkId, NetworkObject>();

        foreach (var resumeNO in runner.GetResumeSnapshotNetworkObjects())
        {
            if (resumeNO == null)
                continue;

            Vector3 position = resumeNO.transform.position;
            Quaternion rotation = resumeNO.transform.rotation;
            NetworkId oldId = resumeNO.Id;
            
            NetworkObject newObject = runner.Spawn(resumeNO, position, rotation, onBeforeSpawned: (runner, no) =>
            {
                no.CopyStateFrom(resumeNO);
                no.AssignInputAuthority(PlayerRef.None);
            });

            if (newObject != null)
            {
                map[oldId] = newObject;
            }
        }
        return map;
    }

    private Dictionary<NetworkId, NetworkObject> RestoreSceneObjects(NetworkRunner runner)
    {
        var map = new Dictionary<NetworkId, NetworkObject>();
        
        var oldScene = runner.SceneManager.MainRunnerScene;

        foreach (var pair in runner.GetResumeSnapshotNetworkSceneObjects())
        {
            NetworkObject currentSceneObject = pair.Item1;
            NetworkObjectHeaderPtr previousState = pair.Item2;

            if (currentSceneObject != null && currentSceneObject.Runner == runner && currentSceneObject.gameObject.scene == oldScene)
            {
                currentSceneObject.CopyStateFrom(previousState);
                map[previousState.Id] = currentSceneObject;
            }
        }

        return map;
    }

    private void ApplyEntityIdFixups(
        NetworkRunner runner,
        Dictionary<NetworkId, NetworkObject> dynamicMap,
        Dictionary<NetworkId, NetworkObject> sceneMap)
    {
        EntityId ResolveId(EntityId oldId)
        {
            if (oldId.Value == 0)
                return oldId; 

            NetworkId oldNetId = new NetworkId { Raw = (uint)oldId.Value };
            
            if (dynamicMap.TryGetValue(oldNetId, out NetworkObject resolvedDynamic))
            {
                return new EntityId((int)resolvedDynamic.Id.Raw);
            }

            if (sceneMap.TryGetValue(oldNetId, out NetworkObject resolvedScene))
            {
                return new EntityId((int)resolvedScene.Id.Raw);
            }
            
            throw new InvalidOperationException($"Cannot resolve old EntityId {oldId.Value}. Migration cannot complete safely.");
        }

        var projectiles = runner.GetComponentsInChildren<NetworkProjectile>(true);
        foreach (var p in projectiles)
        {
            EntityId oldOwner = new EntityId(p.GetRestoredOwnerEntityIdValue());
            if (oldOwner.Value != 0)
            {
                EntityId newOwner = ResolveId(oldOwner);
                p.SetRestoredOwnerEntityId(newOwner);
            }
        }

        var sanctuaries = runner.GetComponentsInChildren<ExtractionSanctuary>(true);
        foreach (var s in sanctuaries)
        {
            EntityId oldOwner = new EntityId(s.GetRestoredOwnerIdValue());
            if (oldOwner.Value != 0)
            {
                EntityId newOwner = ResolveId(oldOwner);
                s.SetRestoredOwnerId(newOwner);
            }
        }

        var extractors = runner.GetComponentsInChildren<PlayerExtractionController>(true);
        foreach (var e in extractors)
        {
            EntityId oldZone = new EntityId(e.GetRestoredActiveZoneIdValue());
            if (oldZone.Value != 0)
            {
                EntityId newZone = ResolveId(oldZone);
                e.SetRestoredActiveZoneId(newZone);
            }
        }
    }
}
