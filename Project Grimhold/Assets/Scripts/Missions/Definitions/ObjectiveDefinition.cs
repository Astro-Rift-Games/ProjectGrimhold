using System;
using UnityEngine;

/// <summary>
/// Defines a single objective within a mission phase.
/// </summary>
[Serializable]
public class ObjectiveDefinition
{
    [Tooltip("The fundamental ruleset for evaluating this objective.")]
    public ObjectiveFamily Family;

    [Tooltip("The conditions that must be met to progress this objective.")]
    public ObjectiveCondition Condition;

    /// <summary>
    /// Validates this objective definition.
    /// </summary>
    public bool TryValidate(out string error)
    {
        if (Condition.RequiredAmount <= 0)
        {
            error = "Objective requires a positive amount.";
            return false;
        }

        error = null;
        return true;
    }
}
