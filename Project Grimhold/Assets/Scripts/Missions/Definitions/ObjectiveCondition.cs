using System;
using UnityEngine;

/// <summary>
/// Defines the criteria that must be satisfied for a progress event to apply to an objective.
/// </summary>
[Serializable]
public struct ObjectiveCondition
{
    [Tooltip("Optional. The specific target required (e.g., enemy prefab ID, chest ID, item ID).")]
    public string TargetId;

    [Tooltip("Optional. The zone identifier where this action must take place.")]
    public string ZoneId;

    [Min(1)]
    [Tooltip("The required amount to complete the objective.")]
    public int RequiredAmount;
}
