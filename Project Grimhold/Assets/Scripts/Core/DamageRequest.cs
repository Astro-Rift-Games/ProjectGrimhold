using UnityEngine;

/// <summary>
/// Contiene la información de una solicitud para aplicar daño.
/// Es inmutable y no contiene estado mutable en tiempo de ejecución.
/// </summary>
public readonly struct DamageRequest
{
    public EntityId AttackerId { get; }
    public EntityId TargetId { get; }
    public float Amount { get; }
    /// <summary>Resolved initial-team direct damage; protects normal and Downed HP.</summary>
    public bool IsAllyDirectDamage { get; }
    public DamageType DamageType { get; }
    public Vector2 Direction { get; }
    public Vector2 HitPoint { get; }
    public int SimulationTick { get; }
    /// <summary>Supporter of an empowered basic attack. Abilities leave this unset.</summary>
    public RaidParticipantId EmpowerSourceId { get; }

    /// <summary>
    /// Knockback power (PE original, Doc 18), converted to displacement by KnockbackCurveConfig after RE mitigation.
    /// A value of 0 produces no knockback. Clamped to [0, ∞).
    /// </summary>
    public float KnockbackForce { get; }

    /// <summary>
    /// Environmental hazard applying this damage (Doc 19 §8.1); default when not hazard damage. The direct
    /// source stays the environment; the target resolves any indirect causal pusher from this id.
    /// </summary>
    public EntityId EnvironmentalHazardId { get; }

    public DamageRequest(
        EntityId attackerId,
        EntityId targetId,
        float amount,
        DamageType damageType,
        Vector2 direction,
        Vector2 hitPoint,
        int simulationTick,
        float knockbackForce = 0f,
        bool isAllyDirectDamage = false,
        RaidParticipantId empowerSourceId = default,
        EntityId environmentalHazardId = default)
    {
        AttackerId     = attackerId;
        TargetId       = targetId;
        Amount         = amount;
        IsAllyDirectDamage = isAllyDirectDamage;
        DamageType     = damageType;
        Direction      = direction.normalized;
        HitPoint       = hitPoint;
        SimulationTick = simulationTick;
        EmpowerSourceId = empowerSourceId;
        KnockbackForce = Mathf.Max(0f, knockbackForce);
        EnvironmentalHazardId = environmentalHazardId;
    }
}
