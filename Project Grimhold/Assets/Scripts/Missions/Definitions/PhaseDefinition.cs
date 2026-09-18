using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a phase containing one or more parallel objectives.
/// </summary>
[Serializable]
public class PhaseDefinition
{
    [Tooltip("The objectives that must be completed to finish this phase.")]
    public List<ObjectiveDefinition> Objectives = new();

    /// <summary>
    /// Validates this phase definition.
    /// </summary>
    public bool TryValidate(out string error)
    {
        if (Objectives == null || Objectives.Count == 0)
        {
            error = "Phase must have at least one objective.";
            return false;
        }

        for (int i = 0; i < Objectives.Count; i++)
        {
            var obj = Objectives[i];
            if (obj == null)
            {
                error = $"Phase contains a null objective at index {i}.";
                return false;
            }

            if (!obj.TryValidate(out string objError))
            {
                error = $"Invalid objective at index {i}: {objError}";
                return false;
            }
        }

        error = null;
        return true;
    }
}
