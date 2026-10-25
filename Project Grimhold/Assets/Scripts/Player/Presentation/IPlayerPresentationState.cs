using System;
using UnityEngine;

/// <summary>
/// Read-only player presentation boundary. Implementations own network guards and clocks;
/// presenters never inspect Fusion objects. Locomotion remains on IMovementState and armor
/// remains on IEquipmentVisualSource.
/// </summary>
public interface IPlayerPresentationState
{
    event Action<AttackPerformedEvent> AttackPerformed;
    event Action<AttackPerformedEvent> AttackPresentationResumed;

    bool TryGetAttackPresentationSeconds(in AttackPerformedEvent attack, out float seconds);
    bool IsAlive { get; }
    bool IsDowned { get; }
    bool IsConsuming { get; }

    /// <summary>Whether the equipment controller's Object exists and is valid.</summary>
    bool IsEquipmentReadable { get; }
    bool TryGetWeaponByCatalogIndexPlusOne(int indexPlusOne, out LootDefinition definition);
    bool TryGetSlotDefinition(EquipmentSlot slot, out LootDefinition definition);
    bool TryGetEquippedDefinition(out LootDefinition definition);
    WeaponSetSlot ActiveWeaponSetSlot { get; }
    EquipmentSlot GetActiveHandSlot(bool offHand);

    /// <summary>Whether the aim controller's Object exists and is valid.</summary>
    bool IsAimReadable { get; }
    Vector2 AimDirection { get; }
    Vector2 FacingDirection { get; }
    bool IsAimStance { get; }
    bool HasInputAuthority { get; }
    bool TryGetAimStanceElapsedSeconds(out float seconds);

    /// <summary>Guarded shield defense state; false before a valid network object exists.</summary>
    bool IsDefending { get; }
    bool TryGetConsumingDefinition(out LootDefinition definition);
}
