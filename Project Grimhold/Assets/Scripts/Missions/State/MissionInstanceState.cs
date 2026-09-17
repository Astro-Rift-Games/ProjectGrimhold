using System;
using System.Collections.Generic;

/// <summary>
/// Mutable state of a specific mission accepted by the player.
/// </summary>
public class MissionInstanceState
{
    public MissionId MissionId;
    public MissionState State;
    public int CurrentPhaseIndex;
    
    /// <summary>
    /// Objective progress mapped by the objective's index within the current phase.
    /// </summary>
    public Dictionary<int, ObjectiveProgressState> ObjectiveProgress = new();

    public MissionInstanceState(MissionId missionId, MissionState initialState = MissionState.Activa)
    {
        MissionId = missionId;
        State = initialState;
        CurrentPhaseIndex = 0;
    }
}
