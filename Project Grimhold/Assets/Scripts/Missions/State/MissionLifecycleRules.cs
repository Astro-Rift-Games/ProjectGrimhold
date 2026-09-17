using System.Collections.Generic;

/// <summary>
/// Pure domain rules for mission transitions and constraints.
/// </summary>
public static class MissionLifecycleRules
{
    public const int MaxNormalMissions = 3;

    /// <summary>
    /// Validates if a transition from the current state to the target state is allowed.
    /// </summary>
    public static bool CanTransitionTo(MissionState currentState, MissionState targetState)
    {
        switch (currentState)
        {
            case MissionState.Disponible:
                return targetState == MissionState.Activa;

            case MissionState.Activa:
                return targetState == MissionState.Completada || targetState == MissionState.Abandonada;

            case MissionState.Completada:
                return targetState == MissionState.PendienteDeReclamar;

            case MissionState.PendienteDeReclamar:
                return targetState == MissionState.Reclamada;

            case MissionState.Reclamada:
            case MissionState.Abandonada:
                return false; // Terminal states

            default:
                return false;
        }
    }

    /// <summary>
    /// Validates if the player can accept a new mission based on current slot limits.
    /// Normal and Unica missions share the same limit. Semanal missions are excluded.
    /// </summary>
    public static bool CanAcceptMission(MissionType typeToAccept, IEnumerable<MissionType> currentActiveTypes)
    {
        if (typeToAccept == MissionType.Semanal)
        {
            return true;
        }

        int count = 0;
        foreach (var type in currentActiveTypes)
        {
            if (type == MissionType.Normal || type == MissionType.Unica)
            {
                count++;
            }
        }

        return count < MaxNormalMissions;
    }
}
