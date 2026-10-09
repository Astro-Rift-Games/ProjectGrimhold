#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Dev-only spawner for the ability sandbox. State Authority spawns melee, ranged and
/// training-dummy objects directly through the runner (the production spawn manager is
/// gated by match phase and loot seeds). Any peer can request a spawn or a clear.
/// </summary>
/// <remarks>
/// Enemies are spawned with an empty loot override and no patrol route, so they need no
/// match controller. A per-spawn max-health override is rejected on purpose: non-player
/// characters expose no runtime max-health hook (see <see cref="RequestSpawn"/>).
/// </remarks>
[DisallowMultipleComponent]
public sealed class SandboxEnemySpawner : NetworkBehaviour
{
    [Header("Network Prefabs")]
    [SerializeField] private NetworkPrefabRef _meleePrefab;
    [SerializeField] private NetworkPrefabRef _rangedPrefab;
    [SerializeField] private NetworkPrefabRef _dummyPrefab;

    [Header("Limits")]
    [SerializeField, Min(1)] private int _maxCountPerRequest = SandboxSpawnPlanner.DefaultMaxCount;
    [SerializeField, Min(1)] private int _maxAlive = 200;

    private readonly List<NetworkObject> _spawned = new List<NetworkObject>();

    public int SpawnedCount
    {
        get
        {
            PruneInvalid();
            return _spawned.Count;
        }
    }

    /// <summary>
    /// Requests a batch spawn. Returns false when the request is rejected locally.
    /// <paramref name="maxHealthOverride"/> must be NaN or non-positive (prefab default):
    /// a positive value is rejected because no runtime max-health hook exists yet.
    /// </summary>
    public bool RequestSpawn(
        SandboxEnemyKind kind,
        int count,
        SandboxSpawnPattern pattern,
        Vector2 center,
        float spacing,
        float maxHealthOverride)
    {
        if (Object == null || !Object.IsValid)
        {
            Debug.LogError($"{nameof(SandboxEnemySpawner)} is not spawned.", this);
            return false;
        }

        if (!IsFinite(center))
        {
            Debug.LogError($"{nameof(SandboxEnemySpawner)} rejected a non-finite spawn center.", this);
            return false;
        }

        if (maxHealthOverride > 0f)
        {
            Debug.LogError(
                $"{nameof(SandboxEnemySpawner)} cannot override max health ({maxHealthOverride}): " +
                "CharacterBase has no runtime max-health hook. Use NaN or 0 for the prefab default.",
                this);
            return false;
        }

        int clamped = SandboxSpawnPlanner.ClampCount(count, _maxCountPerRequest);
        if (HasStateAuthority)
        {
            ExecuteSpawn(kind, clamped, pattern, center, spacing);
            return true;
        }

        RPC_RequestSpawn((byte)kind, clamped, (byte)pattern, center, spacing);
        return true;
    }

    /// <summary>Despawns everything this spawner created.</summary>
    public bool RequestClearAll()
    {
        if (Object == null || !Object.IsValid)
        {
            Debug.LogError($"{nameof(SandboxEnemySpawner)} is not spawned.", this);
            return false;
        }

        if (HasStateAuthority)
        {
            ExecuteClearAll();
            return true;
        }

        RPC_RequestClearAll();
        return true;
    }

    // Any peer may call: the spawner is a scene object, so no peer holds Input Authority on it.
    // Dev-only; inputs are re-validated on the State Authority.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawn(byte kind, int count, byte pattern, Vector2 center, float spacing)
    {
        if (!Enum.IsDefined(typeof(SandboxEnemyKind), kind) ||
            !Enum.IsDefined(typeof(SandboxSpawnPattern), pattern) ||
            !IsFinite(center))
        {
            Debug.LogError($"{nameof(SandboxEnemySpawner)} rejected an invalid spawn request.", this);
            return;
        }

        ExecuteSpawn(
            (SandboxEnemyKind)kind,
            SandboxSpawnPlanner.ClampCount(count, _maxCountPerRequest),
            (SandboxSpawnPattern)pattern,
            center,
            spacing);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestClearAll()
    {
        ExecuteClearAll();
    }

    private void ExecuteSpawn(
        SandboxEnemyKind kind,
        int count,
        SandboxSpawnPattern pattern,
        Vector2 center,
        float spacing)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (!TryResolvePrefab(kind, out NetworkPrefabRef prefab))
        {
            return;
        }

        PruneInvalid();
        int available = _maxAlive - _spawned.Count;
        if (available <= 0)
        {
            Debug.LogWarning($"{nameof(SandboxEnemySpawner)} reached the live limit ({_maxAlive}).", this);
            return;
        }

        IReadOnlyList<Vector2> positions = SandboxSpawnPlanner.Plan(
            center, Mathf.Min(count, available), pattern, spacing, _maxCountPerRequest);

        for (int i = 0; i < positions.Count; i++)
        {
            SpawnOne(kind, prefab, positions[i]);
        }
    }

    private void SpawnOne(SandboxEnemyKind kind, NetworkPrefabRef prefab, Vector2 position)
    {
        bool lootConfigured = kind == SandboxEnemyKind.Dummy;

        NetworkObject instance = Runner.Spawn(
            prefab,
            new Vector3(position.x, position.y, 0f),
            Quaternion.identity,
            inputAuthority: null,
            onBeforeSpawned: (callbackRunner, spawnedObject) =>
            {
                if (kind == SandboxEnemyKind.Dummy)
                {
                    return;
                }

                if (spawnedObject.TryGetBehaviour(out NetworkLootContainer container))
                {
                    lootConfigured = container.TrySetInitialContentOverride(
                        callbackRunner, spawnedObject, Array.Empty<LootEntry>());
                }

                if (spawnedObject.TryGetBehaviour(out EnemyMovementAIController movement))
                {
                    movement.InitializePatrolRoute(null);
                }
            });

        if (instance == null)
        {
            Debug.LogError($"{nameof(SandboxEnemySpawner)}: Runner.Spawn returned null for {kind}.", this);
            return;
        }

        if (!lootConfigured)
        {
            Debug.LogError(
                $"{nameof(SandboxEnemySpawner)}: could not apply the empty loot override for {kind}; despawning it.",
                instance);
            Runner.Despawn(instance);
            return;
        }

        _spawned.Add(instance);
    }

    private void ExecuteClearAll()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            NetworkObject spawned = _spawned[i];
            if (spawned != null && spawned.IsValid)
            {
                Runner.Despawn(spawned);
            }
        }

        _spawned.Clear();
    }

    private bool TryResolvePrefab(SandboxEnemyKind kind, out NetworkPrefabRef prefab)
    {
        switch (kind)
        {
            case SandboxEnemyKind.Melee: prefab = _meleePrefab; break;
            case SandboxEnemyKind.Ranged: prefab = _rangedPrefab; break;
            case SandboxEnemyKind.Dummy: prefab = _dummyPrefab; break;
            default:
                prefab = default;
                Debug.LogError($"{nameof(SandboxEnemySpawner)}: unknown kind '{kind}'.", this);
                return false;
        }

        if (!prefab.IsValid)
        {
            Debug.LogError($"{nameof(SandboxEnemySpawner)}: network prefab for {kind} is not assigned.", this);
            return false;
        }

        return true;
    }

    private void PruneInvalid()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] == null || !_spawned[i].IsValid)
            {
                _spawned.RemoveAt(i);
            }
        }
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
}
#endif
