using System;
using System.Collections.Generic;

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
                progress = new ObjectiveProgressState(0, contribution.ZoneId);
            }

            // Single-expedition extraction validation:
            // ExtraccionDeObjetos requires all items to be extracted in the same expedition.
            // If the incoming event is from a different expedition, reset counter for the new expedition.
            if (objective.Family == ObjectiveFamily.ExtraccionDeObjetos)
            {
                if (!string.IsNullOrEmpty(contribution.ZoneId) &&
                    !string.IsNullOrEmpty(progress.ExpeditionId) &&
                    !string.Equals(progress.ExpeditionId, contribution.ZoneId, StringComparison.OrdinalIgnoreCase))
                {
                    progress = new ObjectiveProgressState(0, contribution.ZoneId);
                }
                else if (string.IsNullOrEmpty(progress.ExpeditionId) && !string.IsNullOrEmpty(contribution.ZoneId))
                {
                    progress.ExpeditionId = contribution.ZoneId;
                }
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

    /// <summary>
    /// Synchronizes dynamic inventory possession objectives (Family == Obtencion)
    /// against the player's personal inventory (Loadout).
    /// Updates objective progress up or down, and transitions between Activa and
    /// PendienteDeReclamar accordingly.
    /// </summary>
    public static bool EvaluateDynamicPossession(
        MissionInstanceState state,
        MissionDefinition definition,
        IReadOnlyList<StashItem> personalInventory)
    {
        if (state == null || definition == null) return false;
        if (state.State != MissionState.Activa &&
            state.State != MissionState.PendienteDeReclamar &&
            state.State != MissionState.Completada)
        {
            return false;
        }

        if (state.CurrentPhaseIndex < 0 || state.CurrentPhaseIndex >= definition.Phases.Count)
        {
            return false;
        }

        PhaseDefinition phase = definition.Phases[state.CurrentPhaseIndex];
        bool hasDynamicObjectives = false;
        for (int i = 0; i < phase.Objectives.Count; i++)
        {
            if (phase.Objectives[i].Family == ObjectiveFamily.Obtencion)
            {
                hasDynamicObjectives = true;
                break;
            }
        }

        if (!hasDynamicObjectives) return false;

        bool changed = false;

        for (int i = 0; i < phase.Objectives.Count; i++)
        {
            var objective = phase.Objectives[i];
            if (objective.Family != ObjectiveFamily.Obtencion) continue;

            int count = 0;
            if (personalInventory != null)
            {
                for (int j = 0; j < personalInventory.Count; j++)
                {
                    var item = personalInventory[j];
                    if (string.Equals(item.LootId.Value, objective.Condition.TargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        count += item.Amount;
                    }
                }
            }

            int targetAmount = Math.Min(count, objective.Condition.RequiredAmount);
            if (!state.ObjectiveProgress.TryGetValue(i, out var progress) || progress.CurrentAmount != targetAmount)
            {
                state.ObjectiveProgress[i] = new ObjectiveProgressState(targetAmount);
                changed = true;
            }
        }

        bool allCompleted = true;
        for (int i = 0; i < phase.Objectives.Count; i++)
        {
            var obj = phase.Objectives[i];
            if (!state.ObjectiveProgress.TryGetValue(i, out var p) || p.CurrentAmount < obj.Condition.RequiredAmount)
            {
                allCompleted = false;
                break;
            }
        }

        if (allCompleted)
        {
            if (state.State == MissionState.Activa)
            {
                if (state.CurrentPhaseIndex + 1 < definition.Phases.Count)
                {
                    state.CurrentPhaseIndex++;
                    state.ObjectiveProgress.Clear();
                }
                else
                {
                    if (state.TryTransitionTo(MissionState.Completada))
                    {
                        state.TryTransitionTo(MissionState.PendienteDeReclamar);
                    }
                }
                changed = true;
            }
        }
        else
        {
            if (state.State == MissionState.PendienteDeReclamar || state.State == MissionState.Completada)
            {
                state.State = MissionState.Activa;
                changed = true;
            }
        }

        return changed;
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

            // Phase completed!
            if (state.CurrentPhaseIndex + 1 < definition.Phases.Count)
            {
                state.CurrentPhaseIndex++;
                state.ObjectiveProgress.Clear(); // Progress is tracked per-phase
            }
            else
            {
                // All phases completed
                if (state.TryTransitionTo(MissionState.Completada))
                {
                    // Automatically transition to PendingClaim to allow the player to claim it
                    state.TryTransitionTo(MissionState.PendienteDeReclamar);
                }
                break;
            }
        }
    }
}
