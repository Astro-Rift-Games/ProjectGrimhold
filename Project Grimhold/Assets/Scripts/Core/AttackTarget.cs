using UnityEngine;

/// <summary>
/// Representa un objetivo detectado durante una consulta de ataque.
/// </summary>
public readonly struct AttackTarget
{
    public EntityId TargetId { get; }
    public Vector2 HitPoint { get; }
    /// <summary>The exact local orb overlapped by this query, or null for ordinary damage targets.</summary>
    public ProtectiveOrbCollider ProtectiveOrb { get; }

    public AttackTarget(EntityId targetId, Vector2 hitPoint) : this(targetId, hitPoint, null) { }

    public AttackTarget(EntityId targetId, Vector2 hitPoint, ProtectiveOrbCollider protectiveOrb)
    {
        TargetId = targetId;
        HitPoint = hitPoint;
        ProtectiveOrb = protectiveOrb;
    }
}
