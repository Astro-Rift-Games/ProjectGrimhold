using Fusion;
using UnityEngine;
using Spawning;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class EnemyReinforcementDirector : NetworkBehaviour
{
    [SerializeField]
    private DungeonPressureConfig _config;

    [Networked]
    private int _spawnsConsumedThisPhase { get; set; }

    [Networked]
    private float _spawnCooldownTimer { get; set; }

    [Networked]
    private DungeonPressurePhase _lastPhase { get; set; }

    private NetworkMatchController _matchController;
    private NetworkSpawnManager _spawnManager;
    private DungeonPressureController _pressureController;

    private readonly List<Vector3> _playerPositionsCache = new List<Vector3>();

    public override void Spawned()
    {
        _matchController = GetComponent<NetworkMatchController>();
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _pressureController = GetComponent<DungeonPressureController>();

        if (HasStateAuthority)
        {
            if (_spawnManager != null && _spawnManager.ShouldInitializeMatchPhase)
            {
                _spawnsConsumedThisPhase = 0;
                _spawnCooldownTimer = 0f;
                _lastPhase = DungeonPressurePhase.Normal;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _matchController == null || _spawnManager == null || _pressureController == null || _config == null)
            return;

        if (_spawnManager.IsHostMigrationRecoveryInProgress)
            return;

        if (_matchController.Phase != NetworkMatchController.MatchPhase.InProgress || !_pressureController.IsRunning)
            return;

        if (_pressureController.Phase != _lastPhase)
        {
            _lastPhase = _pressureController.Phase;
            _spawnsConsumedThisPhase = 0;
            _spawnCooldownTimer = 0f;
        }

        var policy = _config.GetPolicy(_pressureController.Phase);

        if (policy.Budget <= 0 || policy.SpawnIntervalSeconds <= 0f)
            return;

        _spawnCooldownTimer -= Runner.DeltaTime;

        if (_spawnCooldownTimer <= 0f)
        {
            _spawnCooldownTimer = policy.SpawnIntervalSeconds;

            if (_spawnsConsumedThisPhase >= policy.Budget)
                return;

            if (_spawnManager.PopulationTracker.ActiveReinforcements >= policy.MaxConcurrentSpawns)
                return;

            Transform spawnPoint = SelectSpawnPoint(policy.MinDistanceToPlayer);
            if (spawnPoint != null)
            {
                if (_spawnManager.TrySpawnReinforcement(Runner, spawnPoint))
                {
                    _spawnsConsumedThisPhase++;
                    Debug.Log($"[EnemyReinforcementDirector] Spawned reinforcement at {spawnPoint.position}. Budget used: {_spawnsConsumedThisPhase}/{policy.Budget}. Phase: {_pressureController.Phase}");
                }
            }
            else
            {
                Debug.LogWarning($"[EnemyReinforcementDirector] Failed to find a valid spawn point! (All points are closer than {policy.MinDistanceToPlayer} units to players, or no points exist).");
            }
        }
    }

    private Transform SelectSpawnPoint(float minDistance)
    {
        var registry = _spawnManager.ReinforcementRegistry;
        if (registry == null || registry.Points == null || registry.Points.Length == 0)
            return null;

        _playerPositionsCache.Clear();
        foreach (var playerObj in _spawnManager.ActivePlayerObjects)
        {
            if (playerObj != null)
            {
                _playerPositionsCache.Add(playerObj.transform.position);
            }
        }

        return ReinforcementSpawnPlanner.EvaluateAndSelectPoint(registry.Points, _playerPositionsCache, minDistance);
    }
}
