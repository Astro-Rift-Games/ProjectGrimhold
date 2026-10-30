using Fusion;
using UnityEngine;

/// <summary>Value-only attack snapshot for a single Equipment hand slot. No asset or executor references cross the network.</summary>
public struct WeaponHandAttackState : INetworkStruct
{
    public WeaponAttackTiming Timing;
    public WeaponReloadState Reload;
    public MeleeAttackRelease Melee;
    public RangedAttackRelease Ranged;
    public int Sequence;
    public int StartTick;
    public int ReleaseTick;
    public int CancellationTick;
    public int WeaponCatalogIndexPlusOne;
    public int TypeValue;
    public Vector2 Origin;
    public Vector2 Direction;
    public NetworkBool Aimed;
    public bool HasPendingRelease => Melee.Pending || Ranged.Pending;

    public AttackPerformedEvent ToEvent(EntityId owner, EquipmentSlot slot, float deltaTime) => new(
        owner, (AttackType)TypeValue, Origin, Direction, StartTick, WeaponCatalogIndexPlusOne,
        Sequence, ReleaseTick, ReleaseTick >= StartTick ? (ReleaseTick - StartTick) * deltaTime : 0f, Aimed, slot);
}
