using System;

/// <summary>
/// Pure domain engine that evaluates mission events against an active mission state.
/// </summary>
public static class MissionProgressEngine
{
    /// <summary>
    /// Attempts to apply a contribution event to an active mission.
    /// Mutates the state if progress is made, and handles phase/mission completion.
    /// </summary>
    public static bool TryApplyProgress(
        MissionInstanceState state,
        MissionDefinition definition,
        in MissionContributionEvent contribution)
    {
        if (state.State != MissionState.Activa)
        {
            return false;
        }

        if (state.CurrentPhaseIndex < 0 || state.CurrentPhaseIndex >= definition.Phases.Count)
        {
            return false;
        }

        PhaseDefinition phase = definition.Phases[state.CurrentPhaseIndex];
        bool anyProgressMade = false;

        for (int i = 0; i < phase.Objectives.Count; i++)
        {
            var objective = phase.Objectives[i];

            if (objective.Family != contribution.Family)
            {
                continue;
            }

            // Target Filter
            if (!string.IsNullOrEmpty(objective.Condition.TargetId) &&
                !string.Equals(objective.Condition.TargetId, contribution.TargetId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Zone Filter
            if (!string.IsNullOrEmpty(objective.Condition.ZoneId) &&
                !string.Equals(objective.Condition.ZoneId, contribution.ZoneId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!state.ObjectiveProgress.TryGetValue(i, out var progress))
            {
                progress = new ObjectiveProgressState(0);
            }

            if (progress.CurrentAmount >= objective.Condition.RequiredAmount)
            {
                continue; // Objective already complete
            }

            int newAmount = Math.Min(progress.CurrentAmount + contribution.Amount, objective.Condition.RequiredAmount);
            if (newAmount > progress.CurrentAmount)
            {
                progress.CurrentAmount = newAmount;
                state.ObjectiveProgress[i] = progress;
                anyProgressMade = true;
            }
        }

        if (anyProgressMade)
        {
            TryAdvancePhaseOrComplete(state, definition);
        }

        return anyProgressMade;
    }

    private static void TryAdvancePhaseOrComplete(MissionInstanceState state, MissionDefinition definition)
    {
        while (state.State == MissionState.Activa && state.CurrentPhaseIndex < definition.Phases.Count)
        {
            var phase = definition.Phases[state.CurrentPhaseIndex];
            bool phaseCompleted = true;

            for (int i = 0; i < phase.Objectives.Count; i++)
            {
                if (!state.ObjectiveProgress.TryGetValue(i, out var progress) ||
                    progress.CurrentAmount < phase.Objectives[i].Condition.RequiredAmount)
                {
                    phaseCompleted = false;
                    break;
                }
            }

            if (!phaseCompleted)
            {
                break; // Still working on this phase
            }

            // Phase completed! Advance.
            state.CurrentPhaseIndex++;
            state.ObjectiveProgress.Clear(); // Progress is tracked per-phase

            if (state.CurrentPhaseIndex >= definition.Phases.Count)
            {
                // All phases completed
                if (state.TryTransitionTo(MissionState.Completada))
                {
                    // Automatically transition to PendingClaim to allow the player to claim it
                    state.TryTransitionTo(MissionState.PendienteDeReclamar);
                }
            }
        }
    }
}
