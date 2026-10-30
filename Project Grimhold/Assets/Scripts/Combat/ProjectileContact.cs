using UnityEngine;

/// <summary>
/// One classified contact of a projectile resolution event, ordered by authoritative spatial distance.
/// </summary>
public readonly struct ProjectileContact
{
    public ProjectileContactKind Kind { get; }

    /// <summary>Target EntityId value, or the interceptor owner's EntityId value; zero for blockers.</summary>
    public int EntityIdValue { get; }

    /// <summary>Interceptor slot index; zero for other kinds.</summary>
    public int Slot { get; }

    /// <summary>Distance along the projectile direction from the event's start position.</summary>
    public float Distance { get; }

    public Vector2 Point { get; }

    /// <summary>Caller-owned index of the physical source (collider buffer slot).</summary>
    public int SourceIndex { get; }

    public ProjectileContact(ProjectileContactKind kind, int entityIdValue, int slot, float distance,
        Vector2 point, int sourceIndex)
    {
        Kind = kind;
        EntityIdValue = entityIdValue;
        Slot = slot;
        Distance = distance;
        Point = point;
        SourceIndex = sourceIndex;
    }

    /// <summary>True when both contacts describe the same target or the same interceptor slot.</summary>
    public bool IsSameSubject(in ProjectileContact other) =>
        Kind == other.Kind && Kind != ProjectileContactKind.Blocker &&
        EntityIdValue == other.EntityIdValue && Slot == other.Slot;

    /// <summary>Total deterministic order: distance, kind, entity, slot, then source index.</summary>
    public int CompareTo(in ProjectileContact other)
    {
        int result = Distance.CompareTo(other.Distance);
        if (result != 0) return result;
        result = Kind.CompareTo(other.Kind);
        if (result != 0) return result;
        result = EntityIdValue.CompareTo(other.EntityIdValue);
        if (result != 0) return result;
        result = Slot.CompareTo(other.Slot);
        return result != 0 ? result : SourceIndex.CompareTo(other.SourceIndex);
    }
}
