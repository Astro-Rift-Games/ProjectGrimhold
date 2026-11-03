using System;

/// <summary>
/// Tracks the progress of a single objective within a mission.
/// </summary>
public struct ObjectiveProgressState
{
    public int CurrentAmount;
    public string ExpeditionId;
    
    public ObjectiveProgressState(int initialAmount, string expeditionId = null)
    {
        CurrentAmount = initialAmount;
        ExpeditionId = expeditionId;
    }
}
