public static class DungeonPhaseResolver
{
    public static DungeonPressurePhase Resolve(int remainingSeconds, int reinforcementsThreshold, int criticalThreshold)
    {
        if (remainingSeconds > reinforcementsThreshold)
        {
            return DungeonPressurePhase.Normal;
        }
        
        if (remainingSeconds > criticalThreshold)
        {
            return DungeonPressurePhase.Reinforcements;
        }
        
        if (remainingSeconds > 0)
        {
            return DungeonPressurePhase.CriticalPressure;
        }

        return DungeonPressurePhase.Collapse;
    }
}
