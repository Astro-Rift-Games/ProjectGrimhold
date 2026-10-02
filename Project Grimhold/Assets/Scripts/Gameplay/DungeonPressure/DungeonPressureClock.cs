using UnityEngine;

public static class DungeonPressureClock
{
    public static void Step(
        ref int remainingTicks, 
        ref DungeonPressureState state, 
        ref DungeonPressurePhase phase, 
        bool isRaidInProgress, 
        bool isRaidClosingOrFinished, 
        int reinforcementsThresholdSecs,
        int criticalPressureThresholdSecs,
        float deltaTime)
    {
        if (state == DungeonPressureState.NotStarted)
        {
            if (isRaidInProgress)
            {
                state = DungeonPressureState.Running;
                // Debug logs should be handled by the caller.
            }
            return;
        }

        if (state == DungeonPressureState.Running)
        {
            if (isRaidClosingOrFinished)
            {
                state = DungeonPressureState.Stopped;
                return;
            }

            if (phase == DungeonPressurePhase.Collapse)
            {
                remainingTicks = 0;
                return;
            }

            remainingTicks--;

            if (remainingTicks <= 0)
            {
                remainingTicks = 0;
            }

            int remainingSecs = Mathf.CeilToInt(remainingTicks * deltaTime);
            
            DungeonPressurePhase newPhase = DungeonPhaseResolver.Resolve(
                remainingSecs, 
                reinforcementsThresholdSecs, 
                criticalPressureThresholdSecs);

            if (newPhase > phase)
            {
                phase = newPhase;
            }
        }
    }
}
