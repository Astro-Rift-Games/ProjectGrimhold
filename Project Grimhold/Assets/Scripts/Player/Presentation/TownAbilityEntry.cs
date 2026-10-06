using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One unlocked ability as the Town Abilities tab shows it.</summary>
public readonly struct TownAbilityEntry : IEquatable<TownAbilityEntry>
{
    private static readonly IReadOnlyList<TownAbilityRequirementCheck> NoRequirements =
        Array.Empty<TownAbilityRequirementCheck>();

    private readonly IReadOnlyList<TownAbilityRequirementCheck> _requirements;
    private readonly UniversalAbilitySlot _equippedSlot;

    public AbilityId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public Sprite Icon { get; }
    public AbilityResourceType Resource { get; }
    public int Cost { get; }
    public float CooldownSeconds { get; }
    public TownAbilityEntryState State { get; }

    /// <summary>True when the ability is prepared in one of the two universal slots.</summary>
    public bool HasEquippedSlot { get; }

    /// <summary>The prepared slot. Only meaningful when <see cref="HasEquippedSlot"/> is true.</summary>
    public UniversalAbilitySlot EquippedSlot => _equippedSlot;

    public IReadOnlyList<TownAbilityRequirementCheck> Requirements => _requirements ?? NoRequirements;

    public TownAbilityEntry(
        AbilityId id,
        string displayName,
        string description,
        Sprite icon,
        AbilityResourceType resource,
        int cost,
        float cooldownSeconds,
        IReadOnlyList<TownAbilityRequirementCheck> requirements,
        TownAbilityEntryState state,
        bool hasEquippedSlot,
        UniversalAbilitySlot equippedSlot)
    {
        Id = id;
        DisplayName = displayName ?? string.Empty;
        Description = description ?? string.Empty;
        Icon = icon;
        Resource = resource;
        Cost = cost;
        CooldownSeconds = cooldownSeconds;
        _requirements = requirements;
        State = state;
        HasEquippedSlot = hasEquippedSlot;
        _equippedSlot = equippedSlot;
    }

    public bool Equals(TownAbilityEntry other) =>
        Id.Equals(other.Id) && State == other.State &&
        HasEquippedSlot == other.HasEquippedSlot && _equippedSlot == other._equippedSlot &&
        string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is TownAbilityEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, State, HasEquippedSlot, _equippedSlot);
}
