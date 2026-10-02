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

#if UNITY_EDITOR
        if (_forceAttemptRequested)
        {
            _forceAttemptRequested = false;
            var forcePolicy = _config.GetPolicy(_pressureController.Phase);
            TrySpawn(forcePolicy);
            return;
        }
#endif

        var policy = _config.GetPolicy(_pressureController.Phase);

        if (policy.Budget <= 0)
        {
            SetRejectionReason("Disabled / No Budget");
            return;
        }

        if (policy.SpawnIntervalSeconds <= 0f)
        {
            SetRejectionReason("Invalid Interval");
            return;
        }

        _spawnCooldownTimer -= Runner.DeltaTime;

        if (_spawnCooldownTimer <= 0f)
        {
            _spawnCooldownTimer = policy.SpawnIntervalSeconds;
            TrySpawn(policy);
        }
    }

    private void TrySpawn(ReinforcementPolicy policy)
    {
        if (_spawnsConsumedThisPhase >= policy.Budget)
        {
            SetRejectionReason("Population Budget Exhausted");
            return;
        }

        int availableConcurrent = _spawnManager.PopulationTracker.GetAvailableCapacity(policy.MaxConcurrentSpawns, EnemyPopulationOrigin.Reinforcement);
        if (availableConcurrent <= 0)
        {
            SetRejectionReason("Active Threat Cap Reached");
            return;
        }

        int availableGlobal = _spawnManager.PopulationTracker.GetAvailableGlobalCapacity(_config.MaxGlobalEnemies);
        if (availableGlobal <= 0)
        {
            SetRejectionReason("Global Cap Reached");
            return;
        }

        Transform spawnPoint = SelectSpawnPoint(policy.MinDistanceToPlayer);
        if (spawnPoint != null)
        {
            if (_spawnManager.TrySpawnReinforcement(Runner, spawnPoint))
            {
                _spawnsConsumedThisPhase++;
                SetRejectionReason("None (Success)");
                Debug.Log($"[EnemyReinforcementDirector] Spawned reinforcement at {spawnPoint.name}. Phase Budget: {_spawnsConsumedThisPhase}/{policy.Budget}. Active Threat Capacity: {availableConcurrent - 1}. Global Capacity: {availableGlobal - 1}.");
            }
            else
            {
                SetRejectionReason("Spawn Failed (Manager Rejected)");
            }
        }
        else
        {
            SetRejectionReason("No valid Spawn Points (or all too close to player)");
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

#if UNITY_EDITOR
    public string LastRejectionReason { get; private set; } = "None";
    public int EditorSpawnsConsumedThisPhase => _spawnsConsumedThisPhase;
    public float EditorSpawnCooldownTimer => _spawnCooldownTimer;
    private bool _forceAttemptRequested;

    private void SetRejectionReason(string reason)
    {
        LastRejectionReason = reason;
    }

    public void ForceAttempt()
    {
        _forceAttemptRequested = true;
    }
#else
    private void SetRejectionReason(string reason)
    {
        // No-op outside editor
    }
#endif
}
