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

            int availableConcurrent = _spawnManager.PopulationTracker.GetAvailableCapacity(policy.MaxConcurrentSpawns, EnemyPopulationOrigin.Reinforcement);
            if (availableConcurrent <= 0)
                return;

            int availableGlobal = _spawnManager.PopulationTracker.GetAvailableGlobalCapacity(_config.MaxGlobalEnemies);
            if (availableGlobal <= 0)
                return;

            Transform spawnPoint = SelectSpawnPoint(policy.MinDistanceToPlayer);
            if (spawnPoint != null)
            {
                if (_spawnManager.TrySpawnReinforcement(Runner, spawnPoint))
                {
                    _spawnsConsumedThisPhase++;
                    Debug.Log($"[EnemyReinforcementDirector] Spawned reinforcement at {spawnPoint.name}. Phase Budget: {_spawnsConsumedThisPhase}/{policy.Budget}. Active Threat Capacity: {availableConcurrent - 1}. Global Capacity: {availableGlobal - 1}.");
                }
            }
            else
            {
                // Solo logueamos si el diagnóstico es útil, pero para no spamear cada tick, evitamos logs en tick regular fallido a menos que se desee. El plan pide: "remover logs de debug spam excesivo".
                // Dejaremos un log muy esporádico o simplemente lo quitamos para evitar spam si no hay puntos libres durante mucho tiempo.
                // Lo quitamos.
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
