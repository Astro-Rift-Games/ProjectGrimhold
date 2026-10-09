using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Current simulation context, not a second owner of mutable execution state.</summary>
public readonly struct AbilityExecutionContext
{
    private readonly AbilityAreaTargetFinder _areaTargets;

    public NetworkRunner Runner { get; }
    public PlayerCharacter Character { get; }
    public UniversalAbilitySlot Slot { get; }
    public AbilityDefinition Definition { get; }
    public CharacterAttributeState Attributes { get; }

    /// <summary>
    /// Direction the execution captures (or already captured). Resolved by the runtime from the same source it
    /// writes to the snapshot, so planning and execution never disagree. Read-only copy; the player's aim is untouched.
    /// </summary>
    public Vector2 AimDirection { get; }

    internal AbilityExecutionContext(NetworkRunner runner, PlayerCharacter character,
        UniversalAbilitySlot slot, AbilityDefinition definition, in CharacterAttributeState attributes,
        AbilityAreaTargetFinder areaTargets, Vector2 aimDirection = default)
    {
        Runner = runner;
        Character = character;
        Slot = slot;
        Definition = definition;
        Attributes = attributes;
        _areaTargets = areaTargets;
        AimDirection = aimDirection;
    }

    internal AbilityExecutionContext WithAim(Vector2 aimDirection) =>
        new AbilityExecutionContext(Runner, Character, Slot, Definition, Attributes, _areaTargets, aimDirection);

    /// <summary>
    /// Single entry for caster-centered area abilities. Call it from <c>TryPlanStart</c> (reject when the list is
    /// empty) and again from resolution: each call rebuilds the targets around the caster's current position.
    /// The radius belongs to the concrete behavior. Returns false when targeting is unavailable.
    /// </summary>
    public bool TryFindEnemiesInArea(float radius, List<AttackTarget> results) =>
        _areaTargets != null && Character != null &&
        _areaTargets.TryFindEnemies(Runner, Character.Id, radius, results);
}
