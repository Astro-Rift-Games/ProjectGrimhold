using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyCollapseDespawner : NetworkBehaviour
{
    [SerializeField]
    private int _despawnsPerTick = 5;

    private NetworkSpawnManager _spawnManager;
    private DungeonPressureController _pressureController;
    
    private readonly EnemyCollapseDespawnQueue _queue = new EnemyCollapseDespawnQueue();
    private bool _isProcessing;

    public override void Spawned()
    {
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _pressureController = GetComponent<DungeonPressureController>();
        _isProcessing = false;
        _queue.Clear();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _spawnManager == null || _pressureController == null)
            return;

        if (_spawnManager.IsHostMigrationRecoveryInProgress)
            return;

        if (_pressureController.Phase != DungeonPressurePhase.Collapse)
        {
            _isProcessing = false;
            return;
        }

        if (!_isProcessing)
        {
            _isProcessing = true;
            _queue.Populate(_spawnManager.PopulationTracker);
            Debug.Log($"[EnemyCollapseDespawner] Began collapse. Scheduled {_queue.Remaining} enemies for despawn.");
        }

        if (_queue.Remaining == 0)
        {
            if (_pressureController.State != DungeonPressureState.Stopped)
            {
                _pressureController.StopForcefully();
                Debug.Log("[EnemyCollapseDespawner] Despawn complete. Pressure controller stopped.");
            }
            return;
        }

        _queue.PrepareNextBatch(_despawnsPerTick);

        foreach (var enemyId in _queue.CurrentBatch)
        {
            var obj = Runner.FindObject(new NetworkId { Raw = enemyId });
            if (obj != null && obj.IsValid)
            {
                var character = obj.GetComponent<EnemyCharacter>();
                if (character != null && character.IsAlive)
                {
                    Runner.Despawn(obj);
                }
            }
        }
    }
}
