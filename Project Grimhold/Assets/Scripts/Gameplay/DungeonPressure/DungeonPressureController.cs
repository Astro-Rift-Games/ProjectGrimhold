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
    public int RemainingTicks { get; internal set; }

    [Networked]
    public DungeonPressurePhase Phase { get; internal set; }

    [Networked]
    public DungeonPressureState State { get; internal set; }

    private NetworkMatchController _matchController;
    private NetworkSpawnManager _spawnManager;

    public bool IsRunning => State == DungeonPressureState.Running;
    
    public int RemainingSeconds => Runner != null 
        ? Mathf.CeilToInt(RemainingTicks * Runner.DeltaTime) 
        : 0;

    public override void Spawned()
    {
        _matchController = Runner.GetComponent<NetworkMatchController>();
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();

        if (HasStateAuthority)
        {
            if (_spawnManager != null && _spawnManager.ShouldInitializeMatchPhase)
            {
                State = DungeonPressureState.NotStarted;
                Phase = DungeonPressurePhase.Normal;
                
                if (_config != null && _config.IsValid())
                {
                    RemainingTicks = Mathf.CeilToInt(_config.TotalDurationSeconds / Runner.DeltaTime);
                }
                else
                {
                    Debug.LogError("[DungeonPressureController] Invalid or missing config during Spawned.", this);
                    RemainingTicks = 0;
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

        if (State == DungeonPressureState.NotStarted)
        {
            if (_matchController.Phase == NetworkMatchController.MatchPhase.InProgress)
            {
                State = DungeonPressureState.Running;
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
