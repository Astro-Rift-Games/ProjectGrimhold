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
    public int TotalSpawnsGenerated { get; private set; }

    [Networked]
    internal int _evaluationTimerTicks { get; set; }

    [Networked]
    internal int _minSpawnTimerTicks { get; set; }

    [Networked]
    internal DungeonPressurePhase _lastPhase { get; set; }

    private NetworkMatchController _matchController;
    private NetworkSpawnManager _spawnManager;
    private DungeonPressureController _pressureController;

    private readonly List<Vector3> _playerPositionsCache = new List<Vector3>();
    private readonly List<Transform> _validPointsBuffer = new List<Transform>();

    public ReinforcementRejection LastRejection { get; private set; } = ReinforcementRejection.None;

    public override void Spawned()
    {
        _matchController = GetComponent<NetworkMatchController>();
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _pressureController = GetComponent<DungeonPressureController>();

        if (HasStateAuthority)
        {
            if (_spawnManager != null && _spawnManager.ShouldInitializeMatchPhase)
            {
                TotalSpawnsGenerated = 0;
                _evaluationTimerTicks = 0;
                _minSpawnTimerTicks = 0;
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

#if UNITY_EDITOR
        if (_forceAttemptRequested)
        {
            _forceAttemptRequested = false;
            if (_matchController.Phase != NetworkMatchController.MatchPhase.InProgress || !_pressureController.IsRunning)
            {
                SetRejectionReason(ReinforcementRejection.Disabled);
                return;
            }
            EvaluateAndSpawn(true);
            return;
        }
#endif

        if (_matchController.Phase != NetworkMatchController.MatchPhase.InProgress || !_pressureController.IsRunning)
        {
            SetRejectionReason(ReinforcementRejection.Disabled);
            return;
        }

        if (_pressureController.Phase != _lastPhase)
        {
            _lastPhase = _pressureController.Phase;
            var newPolicy = _config.GetPolicy(_lastPhase);
            _evaluationTimerTicks = Mathf.CeilToInt(newPolicy.EvaluationIntervalSeconds * Runner.TickRate);
        }

        if (_evaluationTimerTicks > 0)
            _evaluationTimerTicks--;

        if (_minSpawnTimerTicks > 0)
            _minSpawnTimerTicks--;

        if (_evaluationTimerTicks <= 0)
        {
            EvaluateAndSpawn(false);
        }
    }

    private void EvaluateAndSpawn(bool force)
    {
        var policy = _config.GetPolicy(_pressureController.Phase);
        
        // Restart evaluation timer
        _evaluationTimerTicks = Mathf.CeilToInt(policy.EvaluationIntervalSeconds * Runner.TickRate);

        if (_pressureController.Phase == DungeonPressurePhase.Normal)
        {
            SetRejectionReason(ReinforcementRejection.Disabled);
            return;
        }

        bool intervalElapsed = force || (_minSpawnTimerTicks <= 0);
        
        int capacityBudget = _spawnManager.PopulationTracker.GetAvailableCapacity(policy.PopulationBudget, EnemyPopulationOrigin.Reinforcement);
        int capacityGlobal = _spawnManager.PopulationTracker.GetAvailableGlobalCapacity(_config.MaxGlobalEnemies);
        
        var registry = _spawnManager.ReinforcementRegistry;
        bool hasValidPoints = registry != null && registry.Points != null && registry.Points.Length > 0;

        var plan = ReinforcementSpawnPlanner.Plan(policy, capacityBudget, capacityGlobal, intervalElapsed, hasValidPoints);

        if (plan.Count > 0)
        {
            int spawnedCount = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                Transform spawnPoint = SelectSpawnPoint(policy.MinDistanceToPlayer);
                if (spawnPoint != null)
                {
                    bool disableLoot = DungeonPhaseResolver.GeneratesWithoutLoot(_pressureController.Phase);
                    if (_spawnManager.TrySpawnReinforcement(Runner, spawnPoint, disableLoot))
                    {
                        spawnedCount++;
                        TotalSpawnsGenerated++;
                        Debug.Log($"[EnemyReinforcementDirector] Spawned reinforcement at {spawnPoint.name}.");
                    }
                    else
                    {
                        // Stop if manager rejects
                        if (spawnedCount == 0) SetRejectionReason(ReinforcementRejection.SpawnFailed);
                        break;
                    }
                }
                else
                {
                    if (spawnedCount == 0) SetRejectionReason(ReinforcementRejection.AllPointsTooCloseToPlayer);
                    break;
                }
            }

            if (spawnedCount > 0)
            {
                _minSpawnTimerTicks = Mathf.CeilToInt(policy.MinSecondsBetweenSpawns * Runner.TickRate);
                SetRejectionReason(ReinforcementRejection.None);
            }
        }
        else
        {
            SetRejectionReason(plan.Rejection);
        }
    }

    private Transform SelectSpawnPoint(float minDistance)
    {
        var registry = _spawnManager.ReinforcementRegistry;
        if (registry == null || registry.Points == null || registry.Points.Length == 0)
            return null;

        _playerPositionsCache.Clear();
        foreach (var avatarObj in _spawnManager.ActiveAvatarObjects)
        {
            if (avatarObj != null)
            {
                _playerPositionsCache.Add(avatarObj.transform.position);
            }
        }

        return ReinforcementSpawnPlanner.EvaluateAndSelectPoint(registry.Points, _playerPositionsCache, minDistance, _validPointsBuffer);
    }

    private void SetRejectionReason(ReinforcementRejection reason)
    {
        if (LastRejection != reason)
        {
            LastRejection = reason;
            if (reason != ReinforcementRejection.None)
            {
                Debug.Log($"[EnemyReinforcementDirector] Rejection reason changed to: {reason}");
            }
        }
    }

#if UNITY_EDITOR
    public int EditorEvaluationTimerTicks => _evaluationTimerTicks;
    public int EditorMinSpawnTimerTicks => _minSpawnTimerTicks;
    private bool _forceAttemptRequested;

    public void ForceAttempt()
    {
        _forceAttemptRequested = true;
    }
#endif
}
