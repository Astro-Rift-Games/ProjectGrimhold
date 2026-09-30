using UnityEngine;

[CreateAssetMenu(fileName = "DungeonPressureConfig", menuName = "Grimhold/Gameplay/Dungeon Pressure Config")]
public sealed class DungeonPressureConfig : ScriptableObject
{
    [Tooltip("Total duration of the expedition in seconds. Provisory default.")]
    [SerializeField] private int _totalDurationSeconds = 600;
    
    [Tooltip("Remaining seconds when Reinforcements phase begins.")]
    [SerializeField] private int _reinforcementsThresholdSeconds = 300;
    
    [Tooltip("Remaining seconds when Critical Pressure phase begins.")]
    [SerializeField] private int _criticalPressureThresholdSeconds = 120;

    public int TotalDurationSeconds => _totalDurationSeconds;
    public int ReinforcementsThresholdSeconds => _reinforcementsThresholdSeconds;
    public int CriticalPressureThresholdSeconds => _criticalPressureThresholdSeconds;

    public bool IsValid()
    {
        return _totalDurationSeconds > _reinforcementsThresholdSeconds &&
               _reinforcementsThresholdSeconds > _criticalPressureThresholdSeconds &&
               _criticalPressureThresholdSeconds > 0;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_totalDurationSeconds <= 0) _totalDurationSeconds = 1;
        if (_reinforcementsThresholdSeconds >= _totalDurationSeconds) _reinforcementsThresholdSeconds = _totalDurationSeconds - 1;
        if (_criticalPressureThresholdSeconds >= _reinforcementsThresholdSeconds) _criticalPressureThresholdSeconds = _reinforcementsThresholdSeconds - 1;
        if (_criticalPressureThresholdSeconds <= 0) _criticalPressureThresholdSeconds = 1;
    }
#endif
}
