using Fusion;
using UnityEngine;

/// <summary>
/// Value-only accepted player melee swing. Copying this network struct preserves the deadline and the
/// accepted direction, not an executor or asset reference. Origin is deliberately sampled only at
/// consumption, and the weapon identity is committed so a weapon change discards the swing.
/// </summary>
public struct MeleeAttackRelease : INetworkStruct
{
    public NetworkBool Pending;
    public int ReleaseTick;
    public Vector2 Direction;
    public int WeaponCatalogIndexPlusOne;

    public bool TryAccept(in AttackRequest request, in AttackExecutionParameters parameters,
        int weaponCatalogIndexPlusOne, float deltaTime)
    {
        if (Pending || !parameters.TryValidate(out _) ||
            !AttackTiming.IsFinite(request.Origin) ||
            !PlayerAimMath.TryNormalizeDirection(request.Direction, out Vector2 direction) ||
            !AttackTiming.TryGetReleaseTick(request.SimulationTick, parameters.ReleaseDelaySeconds,
                deltaTime, out int deadline))
            return false;

        this = new MeleeAttackRelease
        {
            Pending = true,
            ReleaseTick = deadline,
            Direction = direction,
            WeaponCatalogIndexPlusOne = weaponCatalogIndexPlusOne
        };
        return true;
    }

    /// <summary>
    /// Consumes the swing before any external damage call. A swing whose weapon changed is discarded.
    /// </summary>
    public bool TryConsume(int tick, int currentWeaponCatalogIndexPlusOne, out Vector2 direction)
    {
        direction = default;
        if (!Pending || tick < ReleaseTick) return false;
        Pending = false;
        if (currentWeaponCatalogIndexPlusOne != WeaponCatalogIndexPlusOne) return false;
        direction = Direction;
        return true;
    }

    /// <summary>Discards a pending swing as soon as the committed weapon is no longer the active one.</summary>
    public bool CancelIfWeaponChanged(int currentWeaponCatalogIndexPlusOne)
    {
        if (!Pending || currentWeaponCatalogIndexPlusOne == WeaponCatalogIndexPlusOne) return false;
        Pending = false;
        return true;
    }

    public void Cancel() => Pending = false;
}
