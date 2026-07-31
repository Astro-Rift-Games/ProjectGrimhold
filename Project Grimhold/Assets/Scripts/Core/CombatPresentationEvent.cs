using UnityEngine;

/// <summary>
/// Immutable authoritative combat result delivered only to local presentation.
/// </summary>
public readonly struct CombatPresentationEvent
{
    public int Sequence { get; }
    public CombatFeedbackKind Kind { get; }
    public EntityId TargetId { get; }
    public Vector2 HitPoint { get; }
    public float AppliedDamage { get; }
    public int SimulationTick { get; }
    public AttackFailureReason AttackFailureReason { get; }

    public CombatPresentationEvent(
        int sequence,
        CombatFeedbackKind kind,
        EntityId targetId,
        Vector2 hitPoint,
        float appliedDamage,
        int simulationTick,
        AttackFailureReason attackFailureReason)
    {
        Sequence = sequence;
        Kind = kind;
        TargetId = targetId;
        HitPoint = hitPoint;
        AppliedDamage = appliedDamage;
        SimulationTick = simulationTick;
        AttackFailureReason = attackFailureReason;
    }
}
