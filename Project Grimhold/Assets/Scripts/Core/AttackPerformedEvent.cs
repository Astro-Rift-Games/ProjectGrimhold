using UnityEngine;

/// <summary>
/// Evento inmutable emitido al culminar la ejecución exitosa de un ataque.
/// Utilizado por los componentes de presentación para disparar efectos visuales, sonoros o animación.
/// </summary>
public readonly struct AttackPerformedEvent
{
    public EntityId AttackerId { get; }
    public AttackType AttackType { get; }
    public Vector2 Origin { get; }
    public Vector2 Direction { get; }
    public int SimulationTick { get; }
    public int WeaponCatalogIndexPlusOne { get; }

    public AttackPerformedEvent(
        EntityId attackerId,
        AttackType attackType,
        Vector2 origin,
        Vector2 direction,
        int simulationTick,
        int weaponCatalogIndexPlusOne = 0)
    {
        AttackerId = attackerId;
        AttackType = attackType;
        Origin = origin;
        Direction = direction.normalized;
        SimulationTick = simulationTick;
        WeaponCatalogIndexPlusOne = weaponCatalogIndexPlusOne;
    }
}
