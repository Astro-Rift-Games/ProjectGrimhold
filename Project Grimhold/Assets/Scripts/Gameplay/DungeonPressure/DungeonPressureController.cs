using Fusion;
using UnityEngine;

public enum DungeonPressureState : byte
{
    NotStarted = 0,
    Running = 1,
    Stopped = 2
}

[DisallowMultipleComponent]
public sealed class DungeonPressureController : NetworkBehaviour
{
    [SerializeField]
    private DungeonPressureConfig _config;

    [Networked]
    public int RemainingTicks { get; set; }

    [Networked]
    public DungeonPressurePhase Phase { get; set; }

    [Networked]
    public DungeonPressureState State { get; set; }

    private NetworkMatchController _matchController;
    private NetworkSpawnManager _spawnManager;

    public bool IsRunning => State == DungeonPressureState.Running;
    
    public int RemainingSeconds => Runner != null 
        ? Mathf.CeilToInt(RemainingTicks * Runner.DeltaTime) 
        : 0;

    public override void Spawned()
    {
        _matchController = GetComponent<NetworkMatchController>();
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();

        if (HasStateAuthority)
        {
            if (_spawnManager != null && _spawnManager.ShouldInitializeMatchPhase)
            {
                State = DungeonPressureState.NotStarted;
                Phase = DungeonPressurePhase.Normal;
                
                if (_config == null)
                {
                    Debug.LogError("[DungeonPressureController] _config is NULL! You forgot to assign the DungeonPressureConfig ScriptableObject in the Inspector of DungeonPressureController!");
                    RemainingTicks = 0;
                }
                else if (!_config.IsValid())
                {
                    Debug.LogError($"[DungeonPressureController] Config is invalid! Total: {_config.TotalDurationSeconds}, Reinf: {_config.ReinforcementsThresholdSeconds}, Crit: {_config.CriticalPressureThresholdSeconds}");
                    RemainingTicks = 0;
                }
                else
                {
                    RemainingTicks = Mathf.CeilToInt(_config.TotalDurationSeconds / Runner.DeltaTime);
                    Debug.Log($"[DungeonPressureController] Initialized RemainingTicks to {RemainingTicks} from duration {_config.TotalDurationSeconds}");
                }
            }
        }
    }

    private int _debugFrameCount = 0;

    public override void FixedUpdateNetwork()
    {
        _debugFrameCount++;
        if (!HasStateAuthority || _matchController == null || _spawnManager == null)
        {
            if (_debugFrameCount % 60 == 0) Debug.Log($"[DungeonPressureController] Early Return 1. Auth: {HasStateAuthority}, MatchController null: {_matchController == null}, SpawnManager null: {_spawnManager == null}");
            return;
        }

        if (_spawnManager.IsHostMigrationRecoveryInProgress)
        {
            if (_debugFrameCount % 60 == 0) Debug.Log($"[DungeonPressureController] Early Return 2. Host Migration.");
            return;
        }

        if (_debugFrameCount % 60 == 0) Debug.Log($"[DungeonPressureController] FUN. State: {State}, MatchPhase: {_matchController.Phase}, Phase: {Phase}, Ticks: {RemainingTicks}");

        if (State == DungeonPressureState.NotStarted)
        {
            if (_matchController.Phase == NetworkMatchController.MatchPhase.InProgress)
            {
                State = DungeonPressureState.Running;
                Debug.Log($"[DungeonPressureController] State changed to RUNNING.");
            }
            return;
        }

        if (State == DungeonPressureState.Running)
        {
            if (_matchController.Phase == NetworkMatchController.MatchPhase.Closing || 
                _matchController.Phase == NetworkMatchController.MatchPhase.Finished)
            {
                State = DungeonPressureState.Stopped;
                return;
            }

            if (Phase == DungeonPressurePhase.Collapse)
            {
                RemainingTicks = 0;
                return;
            }

            RemainingTicks--;

            if (RemainingTicks <= 0)
            {
                RemainingTicks = 0;
            }

            int remainingSecs = Mathf.CeilToInt(RemainingTicks * Runner.DeltaTime);
            
            if (_config != null)
            {
                DungeonPressurePhase newPhase = DungeonPhaseResolver.Resolve(
                    remainingSecs, 
                    _config.ReinforcementsThresholdSeconds, 
                    _config.CriticalPressureThresholdSeconds);

                if (newPhase > Phase)
                {
                    Phase = newPhase;
                }
            }
        }
    }
}
