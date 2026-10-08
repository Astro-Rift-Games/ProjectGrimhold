using Fusion;
using UnityEngine;

/// <summary>
/// Value-only accepted shot. Copying this network struct preserves the deadline and payload, not
/// an executor or asset reference. The owner is the containing avatar's current EntityId (remapped
/// by migration). Origin is deliberately sampled only at consumption.
/// </summary>
public struct RangedAttackRelease : INetworkStruct
{
    public NetworkBool Pending;
    public int ReleaseTick;
    public Vector2 Direction;
    public NetworkPrefabRef Prefab;
    public int ImpactMask;
    public float SpawnOffset;
    public float Speed;
    public float Lifetime;
    public float Damage;
    public DamageType DamageType;
    public float Range;
    public float Knockback;

    public bool TryAccept(in AttackRequest request, RangedAttackConfig config,
        in AttackExecutionParameters parameters, float deltaTime)
    {
        if (Pending || config == null || !config.TryValidate(out _) || !parameters.TryValidate(out _) ||
            !AttackTiming.IsFinite(request.Origin) ||
            !PlayerAimMath.TryNormalizeDirection(request.Direction, out Vector2 direction) ||
            !AttackTiming.TryGetReleaseTick(request.SimulationTick, parameters.ReleaseDelaySeconds,
                deltaTime, out int deadline))
            return false;

        this = new RangedAttackRelease
        {
            Pending = true,
            ReleaseTick = deadline,
            Direction = direction,
            Prefab = config.ProjectilePrefab,
            ImpactMask = config.ImpactLayerMask.value,
            // The weapon owns its spawn point when it sets one; the committed value then travels with the snapshot.
            SpawnOffset = parameters.HasProjectileSpawnDistance
                ? parameters.ProjectileSpawnDistance
                : config.ProjectileSpawnOffset,
            Speed = config.ProjectileSpeed,
            Lifetime = config.LifetimeSeconds,
            Damage = parameters.Damage,
            DamageType = parameters.DamageType,
            Range = parameters.Range,
            Knockback = parameters.KnockbackForce
        };
        return true;
    }

    /// <summary>Consumes before any external spawn call, including failed/invalid release attempts.</summary>
    public bool TryConsume(int tick, EntityId owner, Vector2 currentOrigin, out ProjectileSpawnRequest request)
    {
        request = default;
        if (!Pending || tick < ReleaseTick) return false;
        Pending = false;
        if (!AttackTiming.IsFinite(currentOrigin) || !Prefab.IsValid || ImpactMask == 0 ||
            !AttackTiming.IsFinite(SpawnOffset) || SpawnOffset < 0f) return false;
        request = new ProjectileSpawnRequest(owner, currentOrigin + Direction * SpawnOffset, Direction,
            Damage, DamageType, Speed, Lifetime, Range, tick, Knockback, Prefab, ImpactMask);
        return request.TryValidate();
    }

    public void Cancel() => Pending = false;
}
