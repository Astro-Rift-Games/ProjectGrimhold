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
        int dummyInstability = 0;
        int dummyPhaseElapsedTicks = 0;
        int dummyTotalElapsedTicks = 0;
        Step(
            ref remainingTicks,
            ref dummyCollapseTicks,
            ref state,
            ref phase,
            ref dummyInstability,
            ref dummyPhaseElapsedTicks,
            ref dummyTotalElapsedTicks,
            isRaidInProgress,
            isRaidClosingOrFinished,
            reinforcementsThresholdSecs,
            criticalPressureThresholdSecs,
            0,
            0,
            0,
            0,
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
        int dummyInstability = 0;
        int dummyPhaseElapsedTicks = 0;
        int dummyTotalElapsedTicks = 0;
        Step(
            ref remainingTicks,
            ref collapseRemainingTicks,
            ref state,
            ref phase,
            ref dummyInstability,
            ref dummyPhaseElapsedTicks,
            ref dummyTotalElapsedTicks,
            isRaidInProgress,
            isRaidClosingOrFinished,
            reinforcementsThresholdSecs,
            criticalPressureThresholdSecs,
            collapseDurationSecs,
            0,
            0,
            0,
            0,
            deltaTime);
    }

    /// <summary>
    /// Pure simulation step for authoritative hybrid Dungeon Pressure progression (Doc 20 §7-§8).
    /// </summary>
    public static void Step(
        ref int remainingTicks,
        ref int collapseRemainingTicks,
        ref DungeonPressureState state,
        ref DungeonPressurePhase phase,
        ref int currentInstability,
        ref int phaseElapsedTicks,
        ref int totalElapsedTicks,
        bool isRaidInProgress,
        bool isRaidClosingOrFinished,
        int reinforcementsThresholdSecs,
        int criticalPressureThresholdSecs,
        int collapseDurationSecs,
        int totalDurationSecs,
        int instabilityThreshold,
        int minIntervalSecs,
        int minCollapseStartElapsedSecs,
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

        if (state != DungeonPressureState.Running)
        {
            return;
        }

        if (isRaidClosingOrFinished)
        {
            state = DungeonPressureState.Stopped;
            return;
        }

        totalElapsedTicks++;
        phaseElapsedTicks++;

        float totalElapsedSecs = totalElapsedTicks * deltaTime;
        float phaseElapsedSecs = phaseElapsedTicks * deltaTime;

        if (phase != DungeonPressurePhase.Collapse)
        {
            bool advanceViaInstability = false;

            if (currentInstability >= instabilityThreshold && instabilityThreshold > 0)
            {
                bool minIntervalMet = phaseElapsedSecs >= minIntervalSecs;

                bool collapseAllowed = true;
                if (phase == DungeonPressurePhase.CriticalPressure)
                {
                    collapseAllowed = totalElapsedSecs >= minCollapseStartElapsedSecs;
                }

                if (minIntervalMet && collapseAllowed)
                {
                    advanceViaInstability = true;
                }
                else
                {
                    currentInstability = instabilityThreshold;
                }
            }

            if (advanceViaInstability)
            {
                if (phase == DungeonPressurePhase.Normal)
                {
                    phase = DungeonPressurePhase.Reinforcements;
                    remainingTicks = Mathf.CeilToInt(reinforcementsThresholdSecs / deltaTime);
                }
                else if (phase == DungeonPressurePhase.Reinforcements)
                {
                    phase = DungeonPressurePhase.CriticalPressure;
                    remainingTicks = Mathf.CeilToInt(criticalPressureThresholdSecs / deltaTime);
                }
                else if (phase == DungeonPressurePhase.CriticalPressure)
                {
                    phase = DungeonPressurePhase.Collapse;
                    remainingTicks = 0;
                    if (collapseDurationSecs > 0)
                    {
                        if (totalDurationSecs > 0 && totalElapsedTicks > 0)
                        {
                            int totalClosureTicks = Mathf.CeilToInt((totalDurationSecs + collapseDurationSecs) / deltaTime);
                            collapseRemainingTicks = Mathf.Max(0, totalClosureTicks - totalElapsedTicks);
                        }
                        else
                        {
                            collapseRemainingTicks = Mathf.CeilToInt(collapseDurationSecs / deltaTime);
                        }
                    }
                }

                currentInstability = 0;
                phaseElapsedTicks = 0;
            }
            else
            {
                remainingTicks--;

                if (remainingTicks <= 0)
                {
                    remainingTicks = 0;
                    phase = DungeonPressurePhase.Collapse;
                    if (collapseDurationSecs > 0)
                    {
                        if (totalDurationSecs > 0 && totalElapsedTicks > 0)
                        {
                            int totalClosureTicks = Mathf.CeilToInt((totalDurationSecs + collapseDurationSecs) / deltaTime);
                            collapseRemainingTicks = Mathf.Max(0, totalClosureTicks - totalElapsedTicks);
                        }
                        else if (collapseRemainingTicks <= 0)
                        {
                            collapseRemainingTicks = Mathf.CeilToInt(collapseDurationSecs / deltaTime);
                        }
                    }
                    currentInstability = 0;
                    phaseElapsedTicks = 0;
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
                        currentInstability = 0;
                        phaseElapsedTicks = 0;

                        if (phase == DungeonPressurePhase.Collapse && collapseDurationSecs > 0 && collapseRemainingTicks <= 0)
                        {
                            if (totalDurationSecs > 0 && totalElapsedTicks > 0)
                            {
                                int totalClosureTicks = Mathf.CeilToInt((totalDurationSecs + collapseDurationSecs) / deltaTime);
                                collapseRemainingTicks = Mathf.Max(0, totalClosureTicks - totalElapsedTicks);
                            }
                            else
                            {
                                collapseRemainingTicks = Mathf.CeilToInt(collapseDurationSecs / deltaTime);
                            }
                        }
                    }
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
