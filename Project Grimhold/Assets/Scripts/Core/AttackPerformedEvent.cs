using UnityEngine;

/// <summary>
/// Confirmed attack-start notification: successful melee execution or accepted ranged wind-up.
/// Presentation only; a ranged notification does not guarantee a later successful projectile spawn.
/// </summary>
public readonly struct AttackPerformedEvent
{
    public EntityId AttackerId { get; }
    public AttackType AttackType { get; }
    public Vector2 Origin { get; }
    public Vector2 Direction { get; }
    public int SimulationTick { get; }
    public int WeaponCatalogIndexPlusOne { get; }
    public int Sequence { get; }
    public int ReleaseTick { get; }
    public float ScheduledWindupSeconds { get; }
    public bool HasReleaseTimeline => ReleaseTick >= SimulationTick;

    /// <summary>The shot was released from a fully drawn aim stance, with the short aimed release delay.</summary>
    public bool IsAimed { get; }

    public AttackPerformedEvent(
        EntityId attackerId,
        AttackType attackType,
        Vector2 origin,
        Vector2 direction,
        int simulationTick,
        int weaponCatalogIndexPlusOne = 0,
        int sequence = 0,
        int releaseTick = -1,
        float scheduledWindupSeconds = 0f,
        bool isAimed = false)
    {
        AttackerId = attackerId;
        AttackType = attackType;
        Origin = origin;
        Direction = direction.normalized;
        SimulationTick = simulationTick;
        WeaponCatalogIndexPlusOne = weaponCatalogIndexPlusOne;
        Sequence = sequence;
        ReleaseTick = releaseTick;
        ScheduledWindupSeconds = scheduledWindupSeconds;
        IsAimed = isAimed;
    }
}
