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
                    Debug.LogError("[DungeonPressureController] _config is NULL! Timer will not start.");
                    State = DungeonPressureState.Stopped;
                    RemainingTicks = 0;
                }
                else if (!_config.Validate(out string error))
                {
                    Debug.LogError($"[DungeonPressureController] Config is invalid! Timer will not start. Reason: {error}");
                    State = DungeonPressureState.Stopped;
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

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _matchController == null || _spawnManager == null)
        {
            return;
        }

        if (_spawnManager.IsHostMigrationRecoveryInProgress)
        {
            return;
        }

        if (_config == null || State == DungeonPressureState.Stopped)
        {
            return;
        }

        var state = State;
        var phase = Phase;
        var remainingTicks = RemainingTicks;
        
        bool isInProgress = _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
        bool isClosingOrFinished = _matchController.Phase == NetworkMatchController.MatchPhase.Closing || 
                                   _matchController.Phase == NetworkMatchController.MatchPhase.Finished;

        DungeonPressureClock.Step(
            ref remainingTicks,
            ref state,
            ref phase,
            isInProgress,
            isClosingOrFinished,
            _config.ReinforcementsThresholdSeconds,
            _config.CriticalPressureThresholdSeconds,
            Runner.DeltaTime);

        if (State != state)
        {
            State = state;
            if (state == DungeonPressureState.Running)
            {
                Debug.Log($"[DungeonPressureController] State changed to RUNNING.");
            }
            else if (state == DungeonPressureState.Stopped)
            {
                Debug.Log($"[DungeonPressureController] State changed to STOPPED.");
            }
        }
        
        if (Phase != phase)
        {
            Phase = phase;
            Debug.Log($"[DungeonPressureController] Phase changed to {phase}.");
        }
        
        RemainingTicks = remainingTicks;
    }

    public void StopForcefully()
    {
        if (HasStateAuthority && State != DungeonPressureState.Stopped)
        {
            State = DungeonPressureState.Stopped;
            Debug.Log("[DungeonPressureController] Forcefully stopped by external system (Collapse).");
        }
    }
}
