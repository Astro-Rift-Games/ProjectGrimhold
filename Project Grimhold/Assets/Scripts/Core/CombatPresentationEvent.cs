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
    public Vector2 HitDirection { get; }
    public float AppliedDamage { get; }
    public int SimulationTick { get; }
    public AttackFailureReason AttackFailureReason { get; }
    public EquipmentSlot MeleeSourceSlot { get; }
    public int MeleeAttackSequence { get; }
    public int MeleeWeaponCatalogIndexPlusOne { get; }
    public bool IsMeleeImpact => Kind == CombatFeedbackKind.ConfirmedImpact && MeleeAttackSequence > 0;

    public CombatPresentationEvent(
        int sequence,
        CombatFeedbackKind kind,
        EntityId targetId,
        Vector2 hitPoint,
        float appliedDamage,
        int simulationTick,
        AttackFailureReason attackFailureReason,
        EquipmentSlot meleeSourceSlot = EquipmentSlot.None,
        int meleeAttackSequence = 0,
        int meleeWeaponCatalogIndexPlusOne = 0,
        Vector2 hitDirection = default)
    {
        Sequence = sequence;
        Kind = kind;
        TargetId = targetId;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        AppliedDamage = appliedDamage;
        SimulationTick = simulationTick;
        AttackFailureReason = attackFailureReason;
        MeleeSourceSlot = meleeSourceSlot;
        MeleeAttackSequence = meleeAttackSequence;
        MeleeWeaponCatalogIndexPlusOne = meleeWeaponCatalogIndexPlusOne;
    }
}
