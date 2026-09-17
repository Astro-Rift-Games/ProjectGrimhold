using System;

/// <summary>
/// Tracks the progress of a single objective within a mission.
/// </summary>
public struct ObjectiveProgressState
{
    public int CurrentAmount;
    
    public ObjectiveProgressState(int initialAmount)
    {
        CurrentAmount = initialAmount;
    }
}
