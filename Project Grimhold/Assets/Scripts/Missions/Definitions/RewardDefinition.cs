using System;
using UnityEngine;

/// <summary>
/// Describes a specific reward to be granted upon claiming a mission.
/// </summary>
[Serializable]
public struct RewardDefinition
{
    public enum RewardType
    {
        Experience,
        GuildReputation,
        Gold,
        Item,
        Equipment
    }

    [Tooltip("Type of reward.")]
    public RewardType Type;

    [Tooltip("Quantity of the reward (e.g., XP amount, gold amount).")]
    [Min(1)]
    public int Amount;

    [Tooltip("Optional identifier for Item or Equipment rewards.")]
    public string ReferenceId;
}
