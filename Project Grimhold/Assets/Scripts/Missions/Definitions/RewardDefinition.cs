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
        Equipment,
        Ability
    }

    [Tooltip("Type of reward.")]
    public RewardType Type;

    [Tooltip("Quantity of the reward (e.g., XP amount, gold amount).")]
    [Min(1)]
    public int Amount;

    [Tooltip("Loot identifier for Item or Equipment rewards; Ability identifier for Ability rewards.")]
    public string ReferenceId;
}
