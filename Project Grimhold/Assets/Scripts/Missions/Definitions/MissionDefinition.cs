using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static configuration for a mission.
/// </summary>
[CreateAssetMenu(fileName = "NewMissionDefinition", menuName = "Grimhold/Missions/Mission Definition")]
public class MissionDefinition : ScriptableObject
{
    [SerializeField, Tooltip("The unique string identifier for this mission.")]
    private string _id;

    [Tooltip("The human-readable title of the mission.")]
    public string Title;

    [Tooltip("The human-readable description of the mission.")]
    [TextArea(3, 6)]
    public string Description;

    [Tooltip("The type of mission, determining slot usage and recurrence.")]
    public MissionType Type;

    [Tooltip("The rank required to accept this mission.")]
    public MissionRank RankRequired;

    [Tooltip("The sequential phases of the mission.")]
    public List<PhaseDefinition> Phases = new();

    [Tooltip("The rewards granted upon claiming this mission.")]
    public List<RewardDefinition> Rewards = new();

    /// <summary>
    /// Exposes the immutable mission identifier derived from the configured string.
    /// </summary>
    public MissionId MissionId => new MissionId(_id);

    public string Id => _id;

    /// <summary>
    /// Validates the mission definition for correctness.
    /// </summary>
    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            error = "Mission ID cannot be empty.";
            return false;
        }

        if (Phases == null || Phases.Count == 0)
        {
            error = $"Mission '{_id}' must have at least one phase.";
            return false;
        }

        for (int i = 0; i < Phases.Count; i++)
        {
            var phase = Phases[i];
            if (phase == null)
            {
                error = $"Mission '{_id}' has a null phase at index {i}.";
                return false;
            }
            if (!phase.TryValidate(out string phaseError))
            {
                error = $"Mission '{_id}' invalid phase at index {i}: {phaseError}";
                return false;
            }
        }

        if (Rewards != null)
        {
            for (int i = 0; i < Rewards.Count; i++)
            {
                var reward = Rewards[i];
                if (reward.Amount <= 0)
                {
                    error = $"Mission '{_id}' reward at index {i} requires a positive amount.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }
}
