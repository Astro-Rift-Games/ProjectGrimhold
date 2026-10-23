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
        int dummyCollapseTicks = 0;
        Step(
            ref remainingTicks,
            ref dummyCollapseTicks,
            ref state,
            ref phase,
            isRaidInProgress,
            isRaidClosingOrFinished,
            reinforcementsThresholdSecs,
            criticalPressureThresholdSecs,
            0,
            deltaTime);
    }

    public static void Step(
        ref int remainingTicks,
        ref int collapseRemainingTicks,
        ref DungeonPressureState state, 
        ref DungeonPressurePhase phase, 
        bool isRaidInProgress, 
        bool isRaidClosingOrFinished, 
        int reinforcementsThresholdSecs,
        int criticalPressureThresholdSecs,
        int collapseDurationSecs,
        float deltaTime)
    {
        if (state == DungeonPressureState.NotStarted)
        {
            if (isRaidInProgress)
            {
                state = DungeonPressureState.Running;
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

            if (phase != DungeonPressurePhase.Collapse)
            {
                remainingTicks--;

                if (remainingTicks <= 0)
                {
                    remainingTicks = 0;
                    phase = DungeonPressurePhase.Collapse;
                    if (collapseRemainingTicks <= 0 && collapseDurationSecs > 0)
                    {
                        collapseRemainingTicks = Mathf.CeilToInt(collapseDurationSecs / deltaTime);
                    }
                }
                else
                {
                    int remainingSecs = Mathf.CeilToInt(remainingTicks * deltaTime);
                    
                    DungeonPressurePhase newPhase = DungeonPhaseResolver.Resolve(
                        remainingSecs, 
                        reinforcementsThresholdSecs, 
                        criticalPressureThresholdSecs);

                    if (newPhase > phase)
                    {
                        phase = newPhase;
                    }

                    if (phase == DungeonPressurePhase.Collapse && collapseRemainingTicks <= 0 && collapseDurationSecs > 0)
                    {
                        collapseRemainingTicks = Mathf.CeilToInt(collapseDurationSecs / deltaTime);
                    }
                }
            }
            else
            {
                remainingTicks = 0;
                if (collapseRemainingTicks > 0)
                {
                    collapseRemainingTicks--;
                    if (collapseRemainingTicks <= 0)
                    {
                        collapseRemainingTicks = 0;
                        state = DungeonPressureState.Stopped;
                    }
                }
                else if (collapseDurationSecs > 0)
                {
                    state = DungeonPressureState.Stopped;
                }
            }
        }
    }
}
