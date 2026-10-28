using Fusion;
using UnityEngine;

public enum DungeonPressureState : byte
{
    NotStarted = 0,
    Running = 1,
    Stopped = 2
}

[DisallowMultipleComponent]
public sealed class DungeonPressureController : NetworkBehaviour, IDungeonInstabilitySink
{
    [SerializeField]
    private DungeonPressureConfig _config;

    [Networked]
    public int RemainingTicks { get; set; }

    [Networked]
    public int CollapseRemainingTicks { get; set; }

    [Networked]
    public DungeonPressurePhase Phase { get; set; }

    [Networked]
    public DungeonPressureState State { get; set; }

    [Networked]
    public int CurrentInstability { get; set; }

    [Networked]
    public int PhaseElapsedTicks { get; set; }

    [Networked]
    public int TotalElapsedTicks { get; set; }

    private NetworkMatchController _matchController;
    private NetworkSpawnManager _spawnManager;
    private EntityRegistry _registry;

    public bool IsRunning => State == DungeonPressureState.Running;
    
    public int RemainingSeconds => Runner != null 
        ? Mathf.CeilToInt((Phase == DungeonPressurePhase.Collapse ? CollapseRemainingTicks : RemainingTicks) * Runner.DeltaTime) 
        : 0;

    public int InstabilityThreshold => _config != null ? _config.GetInstabilityThreshold(Phase) : int.MaxValue;
    public int MinIntervalSeconds => _config != null ? _config.GetMinIntervalSeconds(Phase) : 0;
    public float InstabilityProgress => InstabilityThreshold > 0 && InstabilityThreshold < int.MaxValue
        ? Mathf.Clamp01((float)CurrentInstability / InstabilityThreshold)
        : 0f;

    public override void Spawned()
    {
        _matchController = GetComponent<NetworkMatchController>();
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _registry = Runner.GetComponent<EntityRegistry>();
        _registry?.TryRegisterDungeonInstabilitySink(this);

        if (HasStateAuthority)
        {
            if (_spawnManager != null && _spawnManager.ShouldInitializeMatchPhase)
            {
                State = DungeonPressureState.NotStarted;
                Phase = DungeonPressurePhase.Normal;
                CurrentInstability = 0;
                PhaseElapsedTicks = 0;
                TotalElapsedTicks = 0;
                
                if (_config == null)
                {
                    Debug.LogError("[DungeonPressureController] _config is NULL! Timer will not start.");
                    State = DungeonPressureState.Stopped;
                    RemainingTicks = 0;
                    CollapseRemainingTicks = 0;
                }
                else if (!_config.Validate(out string error))
                {
                    Debug.LogError($"[DungeonPressureController] Config is invalid! Timer will not start. Reason: {error}");
                    State = DungeonPressureState.Stopped;
                    RemainingTicks = 0;
                    CollapseRemainingTicks = 0;
                }
                else
                {
                    RemainingTicks = Mathf.CeilToInt(_config.TotalDurationSeconds / Runner.DeltaTime);
                    CollapseRemainingTicks = Mathf.CeilToInt(_config.CollapseDurationSeconds / Runner.DeltaTime);
                    Debug.Log($"[DungeonPressureController] Initialized RemainingTicks to {RemainingTicks} and CollapseRemainingTicks to {CollapseRemainingTicks}");
                }
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_registry != null)
        {
            _registry.TryUnregisterDungeonInstabilitySink(this);
            _registry = null;
        }
    }

    public bool TryRecordEnemyDefeat(CreatureTier tier, Spawning.EnemyPopulationOrigin origin)
    {
        if (!HasStateAuthority || State != DungeonPressureState.Running || Phase == DungeonPressurePhase.Collapse || _config == null)
        {
            return false;
        }

        int points = _config.GetEnemyPoints(tier, origin);
        if (points <= 0) return false;

        AddInstability(points);
        return true;
    }

    public bool TryRecordTreasureFirstAcquisition(string treasureFamily)
    {
        if (!HasStateAuthority || State != DungeonPressureState.Running || Phase == DungeonPressurePhase.Collapse || _config == null)
        {
            return false;
        }

        int points = _config.GetTreasurePoints(treasureFamily);
        if (points <= 0) return false;

        AddInstability(points);
        return true;
    }

    private void AddInstability(int points)
    {
        int threshold = _config.GetInstabilityThreshold(Phase);
        int newInstability = CurrentInstability + points;
        CurrentInstability = Mathf.Min(newInstability, threshold);
        Debug.Log($"[DungeonPressureController] Added {points} instability points. CurrentInstability: {CurrentInstability}/{threshold} in phase {Phase}.");
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
        var collapseRemainingTicks = CollapseRemainingTicks;
        var currentInstability = CurrentInstability;
        var phaseElapsedTicks = PhaseElapsedTicks;
        var totalElapsedTicks = TotalElapsedTicks;
        
        bool isInProgress = _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;
        bool isClosingOrFinished = _matchController.Phase == NetworkMatchController.MatchPhase.Closing || 
                                   _matchController.Phase == NetworkMatchController.MatchPhase.Finished;

#if UNITY_EDITOR
        if (_forceStartRequested)
        {
            _forceStartRequested = false;
            if (state == DungeonPressureState.NotStarted)
            {
                state = DungeonPressureState.Running;
                Debug.Log("[DungeonPressureController] Force started RUNNING via debug request.");
            }
        }

        if (_advancePhaseRequested)
        {
            _advancePhaseRequested = false;
            AdvancePhaseInternal();
            phase = Phase;
            state = State;
            remainingTicks = RemainingTicks;
            collapseRemainingTicks = CollapseRemainingTicks;
            currentInstability = CurrentInstability;
            phaseElapsedTicks = PhaseElapsedTicks;
            totalElapsedTicks = TotalElapsedTicks;
        }

        if (EditorTimeMultiplier > 1f && state == DungeonPressureState.Running)
        {
            _editorAccumulatedExtraTime += (EditorTimeMultiplier - 1f) * Runner.DeltaTime;
            int ticksToDeduct = Mathf.FloorToInt(_editorAccumulatedExtraTime / Runner.DeltaTime);
            if (ticksToDeduct > 0)
            {
                if (phase == DungeonPressurePhase.Collapse)
                {
                    collapseRemainingTicks -= ticksToDeduct;
                    if (collapseRemainingTicks < 0) collapseRemainingTicks = 0;
                }
                else
                {
                    remainingTicks -= ticksToDeduct;
                    if (remainingTicks < 0) remainingTicks = 0;
                }
                _editorAccumulatedExtraTime -= ticksToDeduct * Runner.DeltaTime;
            }
        }
#endif

        DungeonPressureClock.Step(
            ref remainingTicks,
            ref collapseRemainingTicks,
            ref state,
            ref phase,
            ref currentInstability,
            ref phaseElapsedTicks,
            ref totalElapsedTicks,
            isInProgress,
            isClosingOrFinished,
            _config.ReinforcementsThresholdSeconds,
            _config.CriticalPressureThresholdSeconds,
            _config.CollapseDurationSeconds,
            _config.TotalDurationSeconds,
            _config.GetInstabilityThreshold(phase),
            _config.GetMinIntervalSeconds(phase),
            _config.MinCollapseStartElapsedSeconds,
            Runner.DeltaTime);

        bool shouldTriggerAbsoluteClosure = State == DungeonPressureState.Running &&
                                             state == DungeonPressureState.Stopped &&
                                             phase == DungeonPressurePhase.Collapse;

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
        CollapseRemainingTicks = collapseRemainingTicks;
        CurrentInstability = currentInstability;
        PhaseElapsedTicks = phaseElapsedTicks;
        TotalElapsedTicks = totalElapsedTicks;

        if (shouldTriggerAbsoluteClosure)
        {
            Debug.Log("[DungeonPressureController] Absolute collapse deadline reached. Defeating remaining raiding participants.");
            _spawnManager.DefeatRemainingRaidingParticipantsForAbsoluteClosure();
        }
    }

#if UNITY_EDITOR
    [HideInInspector]
    public float EditorTimeMultiplier = 1f;
    private float _editorAccumulatedExtraTime;
    private bool _advancePhaseRequested;
    private bool _forceStartRequested;

    public void AdvancePhase()
    {
        _advancePhaseRequested = true;
    }

    public void ForceStartTimer()
    {
        _forceStartRequested = true;
    }

    public void ForceAddInstability(int points)
    {
        if (HasStateAuthority && State == DungeonPressureState.Running)
        {
            AddInstability(points);
        }
    }

    private void AdvancePhaseInternal()
    {
        if (!HasStateAuthority || State == DungeonPressureState.Stopped || _config == null) return;
        
        if (State == DungeonPressureState.NotStarted)
        {
            State = DungeonPressureState.Running;
            Debug.Log("[DungeonPressureController] Force started RUNNING state via AdvancePhase.");
        }

        CurrentInstability = 0;
        PhaseElapsedTicks = 0;

        if (Phase == DungeonPressurePhase.Normal)
        {
            Phase = DungeonPressurePhase.Reinforcements;
            RemainingTicks = Mathf.CeilToInt(_config.ReinforcementsThresholdSeconds / Runner.DeltaTime);
            Debug.Log($"[DungeonPressureController] Advanced phase to {Phase}. RemainingTicks: {RemainingTicks}");
        }
        else if (Phase == DungeonPressurePhase.Reinforcements)
        {
            Phase = DungeonPressurePhase.CriticalPressure;
            RemainingTicks = Mathf.CeilToInt(_config.CriticalPressureThresholdSeconds / Runner.DeltaTime);
            Debug.Log($"[DungeonPressureController] Advanced phase to {Phase}. RemainingTicks: {RemainingTicks}");
        }
        else if (Phase == DungeonPressurePhase.CriticalPressure)
        {
            Phase = DungeonPressurePhase.Collapse;
            RemainingTicks = 0;
            int totalClosureTicks = Mathf.CeilToInt((_config.TotalDurationSeconds + _config.CollapseDurationSeconds) / Runner.DeltaTime);
            CollapseRemainingTicks = Mathf.Max(0, totalClosureTicks - TotalElapsedTicks);
            Debug.Log($"[DungeonPressureController] Advanced phase to {Phase}. CollapseRemainingTicks: {CollapseRemainingTicks}");
        }
        else if (Phase == DungeonPressurePhase.Collapse)
        {
            CollapseRemainingTicks = 0;
            State = DungeonPressureState.Stopped;
            _spawnManager?.DefeatRemainingRaidingParticipantsForAbsoluteClosure();
            Debug.Log("[DungeonPressureController] Advanced phase past Collapse -> Absolute Closure executed.");
        }
    }
#endif
}
