using UnityEngine;

[CreateAssetMenu(fileName = "DungeonPressureConfig", menuName = "Grimhold/Gameplay/Dungeon Pressure Config")]
public sealed class DungeonPressureConfig : ScriptableObject
{
    [Tooltip("Total duration of the expedition in seconds. Provisory default.")]
    [SerializeField] private int _totalDurationSeconds = 900;
    
    [Tooltip("Remaining seconds when Reinforcements phase begins.")]
    [SerializeField] private int _reinforcementsThresholdSeconds = 300;
    
    [Tooltip("Remaining seconds when Critical Pressure phase begins.")]
    [SerializeField] private int _criticalPressureThresholdSeconds = 120;

    [Tooltip("Duration of the Collapse phase in seconds before absolute closure.")]
    [SerializeField] private int _collapseDurationSeconds = 120;

    [Tooltip("Global maximum number of active enemies allowed at any given time.")]
    [SerializeField] private int _maxGlobalEnemies = 40;

    public int TotalDurationSeconds => _totalDurationSeconds;
    public int ReinforcementsThresholdSeconds => _reinforcementsThresholdSeconds;
    public int CriticalPressureThresholdSeconds => _criticalPressureThresholdSeconds;
    public int CollapseDurationSeconds => _collapseDurationSeconds;
    public int MaxGlobalEnemies => _maxGlobalEnemies;

    [Header("Hybrid Instability Progression (Doc 20)")]
    [Tooltip("Instability threshold required to advance from Normal (E0) to Reinforcements (E1).")]
    [SerializeField] private int _e0InstabilityThreshold = 100;
    [Tooltip("Minimum seconds that must elapse in Normal (E0) before instability can trigger advance.")]
    [SerializeField] private int _e0MinIntervalSeconds = 180;

    [Tooltip("Instability threshold required to advance from Reinforcements (E1) to Critical Pressure (E2).")]
    [SerializeField] private int _e1InstabilityThreshold = 100;
    [Tooltip("Minimum seconds that must elapse in Reinforcements (E1) before instability can trigger advance.")]
    [SerializeField] private int _e1MinIntervalSeconds = 120;

    [Tooltip("Instability threshold required to advance from Critical Pressure (E2) to Collapse (E3).")]
    [SerializeField] private int _e2InstabilityThreshold = 100;
    [Tooltip("Minimum seconds that must elapse in Critical Pressure (E2) before instability can trigger advance.")]
    [SerializeField] private int _e2MinIntervalSeconds = 60;

    [Tooltip("Absolute minimum elapsed raid time (in seconds) before Collapse (E3) can start. Initial reference: 08:00 (480s).")]
    [SerializeField] private int _minCollapseStartElapsedSeconds = 480;

    [Header("PvE Enemy Instability Points")]
    [SerializeField] private int _ordinaryEnemyPoints = 5;
    [SerializeField] private int _eliteEnemyPoints = 15;
    [SerializeField] private int _bossEnemyPoints = 50;
    [SerializeField] private int _reinforcementEnemyPoints = 2;

    [Header("Treasure First Acquisition Instability Points")]
    [SerializeField] private int _conventionalTreasurePoints = 10;
    [SerializeField] private int _divineTreasurePoints = 20;
    [SerializeField] private int _cursedTreasurePoints = 25;
    [SerializeField] private int _chaosTreasurePoints = 30;

    public int E0InstabilityThreshold => _e0InstabilityThreshold;
    public int E0MinIntervalSeconds => _e0MinIntervalSeconds;
    public int E1InstabilityThreshold => _e1InstabilityThreshold;
    public int E1MinIntervalSeconds => _e1MinIntervalSeconds;
    public int E2InstabilityThreshold => _e2InstabilityThreshold;
    public int E2MinIntervalSeconds => _e2MinIntervalSeconds;
    public int MinCollapseStartElapsedSeconds => _minCollapseStartElapsedSeconds;

    public int OrdinaryEnemyPoints => _ordinaryEnemyPoints;
    public int EliteEnemyPoints => _eliteEnemyPoints;
    public int BossEnemyPoints => _bossEnemyPoints;
    public int ReinforcementEnemyPoints => _reinforcementEnemyPoints;

    public int ConventionalTreasurePoints => _conventionalTreasurePoints;
    public int DivineTreasurePoints => _divineTreasurePoints;
    public int CursedTreasurePoints => _cursedTreasurePoints;
    public int ChaosTreasurePoints => _chaosTreasurePoints;

    public int GetInstabilityThreshold(DungeonPressurePhase phase)
    {
        return phase switch
        {
            DungeonPressurePhase.Normal => _e0InstabilityThreshold,
            DungeonPressurePhase.Reinforcements => _e1InstabilityThreshold,
            DungeonPressurePhase.CriticalPressure => _e2InstabilityThreshold,
            _ => int.MaxValue
        };
    }

    public int GetMinIntervalSeconds(DungeonPressurePhase phase)
    {
        return phase switch
        {
            DungeonPressurePhase.Normal => _e0MinIntervalSeconds,
            DungeonPressurePhase.Reinforcements => _e1MinIntervalSeconds,
            DungeonPressurePhase.CriticalPressure => _e2MinIntervalSeconds,
            _ => 0
        };
    }

    public int GetEnemyPoints(CreatureTier tier, Spawning.EnemyPopulationOrigin origin)
    {
        if (origin == Spawning.EnemyPopulationOrigin.Reinforcement)
        {
            return _reinforcementEnemyPoints;
        }

        return tier switch
        {
            CreatureTier.Ordinary => _ordinaryEnemyPoints,
            CreatureTier.Elite => _eliteEnemyPoints,
            CreatureTier.Boss => _bossEnemyPoints,
            _ => _ordinaryEnemyPoints
        };
    }

    public int GetTreasurePoints(string family)
    {
        if (string.IsNullOrEmpty(family))
        {
            return _conventionalTreasurePoints;
        }

        return family switch
        {
            "ConventionalTreasure" => _conventionalTreasurePoints,
            "DivineTreasure" => _divineTreasurePoints,
            "CursedTreasure" => _cursedTreasurePoints,
            "ChaosTreasure" => _chaosTreasurePoints,
            _ => _conventionalTreasurePoints
        };
    }

    [Header("Phase Policies")]
    [SerializeField] private Spawning.ReinforcementPolicy _normalPolicy = new Spawning.ReinforcementPolicy { PopulationBudget = 0, EvaluationIntervalSeconds = 10f, MinSecondsBetweenSpawns = 0f, MaxSpawnsPerAttempt = 1, MinDistanceToPlayer = 20f };
    [SerializeField] private Spawning.ReinforcementPolicy _reinforcementsPolicy = new Spawning.ReinforcementPolicy { PopulationBudget = 2, EvaluationIntervalSeconds = 15f, MinSecondsBetweenSpawns = 0f, MaxSpawnsPerAttempt = 1, MinDistanceToPlayer = 15f };
    [SerializeField] private Spawning.ReinforcementPolicy _criticalPressurePolicy = new Spawning.ReinforcementPolicy { PopulationBudget = 5, EvaluationIntervalSeconds = 5f, MinSecondsBetweenSpawns = 0f, MaxSpawnsPerAttempt = 1, MinDistanceToPlayer = 10f };
    [SerializeField] private Spawning.ReinforcementPolicy _collapsePolicy = new Spawning.ReinforcementPolicy { PopulationBudget = 8, EvaluationIntervalSeconds = 3f, MinSecondsBetweenSpawns = 0f, MaxSpawnsPerAttempt = 2, MinDistanceToPlayer = 10f };

    public Spawning.ReinforcementPolicy GetPolicy(DungeonPressurePhase phase)
    {
        return phase switch
        {
            DungeonPressurePhase.Normal => _normalPolicy,
            DungeonPressurePhase.Reinforcements => _reinforcementsPolicy,
            DungeonPressurePhase.CriticalPressure => _criticalPressurePolicy,
            DungeonPressurePhase.Collapse => _collapsePolicy,
            _ => Spawning.ReinforcementPolicy.None
        };
    }

    public bool Validate(out string error)
    {
        if (_totalDurationSeconds <= 0)
        {
            error = "TotalDurationSeconds must be greater than 0.";
            return false;
        }
        if (_criticalPressureThresholdSeconds <= 0)
        {
            error = "CriticalPressureThresholdSeconds must be greater than 0.";
            return false;
        }
        if (_reinforcementsThresholdSeconds <= _criticalPressureThresholdSeconds)
        {
            error = "ReinforcementsThresholdSeconds must be strictly greater than CriticalPressureThresholdSeconds.";
            return false;
        }
        if (_totalDurationSeconds <= _reinforcementsThresholdSeconds)
        {
            error = "TotalDurationSeconds must be strictly greater than ReinforcementsThresholdSeconds.";
            return false;
        }
        if (_collapseDurationSeconds <= 0)
        {
            error = "CollapseDurationSeconds must be greater than 0.";
            return false;
        }
        if (_maxGlobalEnemies <= 0)
        {
            error = "MaxGlobalEnemies must be greater than 0.";
            return false;
        }
        if (_minCollapseStartElapsedSeconds <= 0)
        {
            error = "MinCollapseStartElapsedSeconds must be greater than 0.";
            return false;
        }
        if (_e0InstabilityThreshold < 0 || _e1InstabilityThreshold < 0 || _e2InstabilityThreshold < 0)
        {
            error = "Instability thresholds cannot be negative.";
            return false;
        }
        if (_e0MinIntervalSeconds < 0 || _e1MinIntervalSeconds < 0 || _e2MinIntervalSeconds < 0)
        {
            error = "Minimum interval seconds cannot be negative.";
            return false;
        }

        var phases = new[] { DungeonPressurePhase.Normal, DungeonPressurePhase.Reinforcements, DungeonPressurePhase.CriticalPressure, DungeonPressurePhase.Collapse };
        foreach (var phase in phases)
        {
            var policy = GetPolicy(phase);
            
            if (phase == DungeonPressurePhase.Normal && policy.PopulationBudget > 0)
            {
                error = $"Policy for phase {phase} must have PopulationBudget = 0 (reinforcements disabled).";
                return false;
            }

            if (phase == DungeonPressurePhase.Collapse && policy.PopulationBudget <= 0)
            {
                error = $"Policy for phase {phase} must have PopulationBudget > 0 (collapse applies maximum PvE pressure).";
                return false;
            }

            if (policy.PopulationBudget > _maxGlobalEnemies)
            {
                error = $"Policy for phase {phase} has PopulationBudget ({policy.PopulationBudget}) exceeding MaxGlobalEnemies ({_maxGlobalEnemies}).";
                return false;
            }

            if (policy.MaxSpawnsPerAttempt > policy.PopulationBudget && policy.PopulationBudget > 0)
            {
                error = $"Policy for phase {phase} has MaxSpawnsPerAttempt ({policy.MaxSpawnsPerAttempt}) exceeding PopulationBudget ({policy.PopulationBudget}).";
                return false;
            }

            if (policy.MinSecondsBetweenSpawns < 0f)
            {
                error = $"Policy for phase {phase} has MinSecondsBetweenSpawns < 0.";
                return false;
            }

            if (policy.MinDistanceToPlayer < 0f)
            {
                error = $"Policy for phase {phase} has MinDistanceToPlayer < 0.";
                return false;
            }

            if (policy.PopulationBudget > 0)
            {
                if (policy.EvaluationIntervalSeconds <= 0f)
                {
                    error = $"Policy for phase {phase} has PopulationBudget > 0 but EvaluationIntervalSeconds is <= 0.";
                    return false;
                }
                if (policy.MaxSpawnsPerAttempt < 1)
                {
                    error = $"Policy for phase {phase} has PopulationBudget > 0 but MaxSpawnsPerAttempt is < 1.";
                    return false;
                }
            }
        }

        var reinPolicy = GetPolicy(DungeonPressurePhase.Reinforcements);
        var critPolicy = GetPolicy(DungeonPressurePhase.CriticalPressure);

        if (critPolicy.PopulationBudget < reinPolicy.PopulationBudget)
        {
            error = "CriticalPressure PopulationBudget cannot be less than Reinforcements PopulationBudget.";
            return false;
        }

        if (critPolicy.PopulationBudget > 0 && reinPolicy.PopulationBudget > 0)
        {
            if (critPolicy.EvaluationIntervalSeconds > reinPolicy.EvaluationIntervalSeconds)
            {
                error = "CriticalPressure EvaluationIntervalSeconds cannot be greater than Reinforcements EvaluationIntervalSeconds (Critical should be more frequent).";
                return false;
            }
            if (critPolicy.MaxSpawnsPerAttempt < reinPolicy.MaxSpawnsPerAttempt)
            {
                error = "CriticalPressure MaxSpawnsPerAttempt cannot be less than Reinforcements MaxSpawnsPerAttempt.";
                return false;
            }
        }

        var collapsePolicy = GetPolicy(DungeonPressurePhase.Collapse);

        if (collapsePolicy.PopulationBudget < critPolicy.PopulationBudget)
        {
            error = "Collapse PopulationBudget cannot be less than CriticalPressure PopulationBudget.";
            return false;
        }

        if (critPolicy.PopulationBudget > 0)
        {
            if (collapsePolicy.EvaluationIntervalSeconds > critPolicy.EvaluationIntervalSeconds)
            {
                error = "Collapse EvaluationIntervalSeconds cannot be greater than CriticalPressure EvaluationIntervalSeconds (Collapse should be more frequent).";
                return false;
            }
            if (collapsePolicy.MaxSpawnsPerAttempt < critPolicy.MaxSpawnsPerAttempt)
            {
                error = "Collapse MaxSpawnsPerAttempt cannot be less than CriticalPressure MaxSpawnsPerAttempt.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_totalDurationSeconds <= 0) _totalDurationSeconds = 1;
        if (_reinforcementsThresholdSeconds >= _totalDurationSeconds) _reinforcementsThresholdSeconds = _totalDurationSeconds - 1;
        if (_criticalPressureThresholdSeconds >= _reinforcementsThresholdSeconds) _criticalPressureThresholdSeconds = _reinforcementsThresholdSeconds - 1;
        if (_criticalPressureThresholdSeconds <= 0) _criticalPressureThresholdSeconds = 1;
        if (_collapseDurationSeconds <= 0) _collapseDurationSeconds = 1;
        if (_minCollapseStartElapsedSeconds <= 0) _minCollapseStartElapsedSeconds = 1;
    }
#endif
}
