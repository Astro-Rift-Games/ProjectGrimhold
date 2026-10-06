using Fusion;
using UnityEngine;

/// <summary>
/// Representa una solicitud inmutable para generar un proyectil.
/// Contiene únicamente datos pertenecientes al core de simulación.
/// </summary>
public readonly struct ProjectileSpawnRequest
{
    public EntityId OwnerId { get; }
    public Vector2 Origin { get; }
    public Vector2 Direction { get; }
    public float Damage { get; }
    public DamageType DamageType { get; }
    public float Speed { get; }
    public float LifetimeSeconds { get; }
    public float MaximumRange { get; }
    public int SimulationTick { get; }
    // Optional committed projectile configuration. Legacy consumers still use the spawner's config.
    public NetworkPrefabRef ProjectilePrefab { get; }
    public int ImpactLayerMask { get; }

    /// <summary>
    /// Knockback force in world units per second applied to the target on impact.
    /// Stored in the projectile's networked state and forwarded to the
    /// <see cref="DamageRequest"/> when the projectile hits a damageable entity.
    /// </summary>
    public float KnockbackForce { get; }

    public ProjectileSpawnRequest(
        EntityId ownerId,
        Vector2 origin,
        Vector2 direction,
        float damage,
        DamageType damageType,
        float speed,
        float lifetimeSeconds,
        float maximumRange,
        int simulationTick,
        float knockbackForce = 0f,
        NetworkPrefabRef projectilePrefab = default,
        int impactLayerMask = 0)
    {
        OwnerId        = ownerId;
        Origin         = origin;
        Direction      = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
        Damage         = damage;
        DamageType     = damageType;
        Speed          = speed;
        LifetimeSeconds = lifetimeSeconds;
        MaximumRange   = maximumRange;
        SimulationTick = simulationTick;
        KnockbackForce = Mathf.Max(0f, knockbackForce);
        ProjectilePrefab = projectilePrefab;
        ImpactLayerMask = impactLayerMask;
    }

    public bool TryValidate() =>
        AttackTiming.IsFinite(Origin) && AttackTiming.IsFinite(Direction) && Direction.sqrMagnitude > 0.0001f &&
        AttackTiming.IsFinite(Damage) && Damage > 0f && System.Enum.IsDefined(typeof(DamageType), DamageType) &&
        AttackTiming.IsFinite(Speed) && Speed > 0f && AttackTiming.IsFinite(LifetimeSeconds) && LifetimeSeconds > 0f &&
        AttackTiming.IsFinite(MaximumRange) && MaximumRange > 0f &&
        AttackTiming.IsFinite(KnockbackForce) && KnockbackForce >= 0f;
}
