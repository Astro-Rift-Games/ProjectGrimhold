using Fusion;
using UnityEngine;

/// <summary>
/// Network component responsible for projectile simulation.
/// Handles kinematic movement, collision detection using its own collider volume,
/// and applies authoritative damage under State Authority in <see cref="FixedUpdateNetwork"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class NetworkProjectile : NetworkBehaviour
{
    [Header("Local Components")]
    [SerializeField]
    private Collider2D _projectileCollider;

    [SerializeField]
    private Rigidbody2D _rigidbody;

    [Networked]
    private int OwnerEntityIdValue { get; set; }

    [Networked]
    private float Damage { get; set; }

    [Networked]
    private DamageType DamageType { get; set; }

    [Networked]
    private Vector2 Direction { get; set; }

    [Networked]
    private float Speed { get; set; }

    [Networked]
    private Vector2 SpawnPosition { get; set; }

    [Networked]
    private float MaximumRange { get; set; }

    [Networked]
    private TickTimer LifetimeTimer { get; set; }

    [Networked]
    private int ImpactLayerMaskValue { get; set; }

    [Networked]
    private NetworkBool ImpactConsumed { get; set; }

    [Networked]
    private int SpawnSimulationTick { get; set; }

    private ContactFilter2D _contactFilter;
    private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];
    private IDamageResolver _damageResolver;
    private EntityRegistry _registry;

    private void Awake()
    {
        CacheComponents();
    }

    public override void Spawned()
    {
        CacheComponents();
        ConfigureContactFilter();

        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(NetworkProjectile)}: EntityRegistry was not found on the NetworkRunner GameObject.", this);
        }

        // Align physical starting state and force Unity physics synchronization
        if (_rigidbody != null)
        {
            _rigidbody.position = transform.position;
        }
        Physics2D.SyncTransforms();
    }

    private void CacheComponents()
    {
        if (_projectileCollider == null)
        {
            _projectileCollider = GetComponent<Collider2D>();
        }

        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }
    }

    private void ConfigureContactFilter()
    {
        _contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true // Allow interacting with triggers based on configured layer mask
        };
        _contactFilter.SetLayerMask(new LayerMask { value = ImpactLayerMaskValue });
    }

    private bool TryGetImpact(Collider2D collider, out EntityId targetId)
    {
        targetId = default;

        if (collider == null || collider == _projectileCollider)
        {
            return false;
        }

        // Static world geometry has no entity id but still consumes the projectile.
        if (_registry == null || !_registry.TryGetEntityId(collider, out targetId))
        {
            return true;
        }

        return targetId.Value != OwnerEntityIdValue;
    }

    /// <summary>
    /// Initializes network properties of the projectile prior to spawning completing.
    /// Can only be called during the initialization callback of the spawner on the State Authority.
    /// </summary>
    public void InitializeNetworkState(in ProjectileSpawnRequest request, int impactLayerMaskValue)
    {
        OwnerEntityIdValue = request.OwnerId.Value;
        Damage = request.Damage;
        DamageType = request.DamageType;
        Direction = request.Direction;
        Speed = request.Speed;
        SpawnPosition = request.Origin;
        MaximumRange = request.MaximumRange;
        LifetimeTimer = TickTimer.CreateFromSeconds(Runner, request.LifetimeSeconds);
        ImpactLayerMaskValue = impactLayerMaskValue;
        ImpactConsumed = false;
        SpawnSimulationTick = request.SimulationTick;

        Debug.Log($"[CombatTrace] Projectile initialized. OwnerId: {OwnerEntityIdValue}, Damage: {Damage}, Lifetime: {request.LifetimeSeconds}, Tick: {SpawnSimulationTick}", this);
    }

    /// <summary>
    /// Assigns required runtime dependencies.
    /// </summary>
    public void SetRuntimeDependencies(IDamageResolver damageResolver)
    {
        _damageResolver = damageResolver;
    }

    public override void FixedUpdateNetwork()
    {
        // Execute gameplay simulation exclusively under State Authority
        if (!HasStateAuthority)
        {
            return;
        }

        // 1. Validate expiration by lifetime
        if (LifetimeTimer.ExpiredOrNotRunning(Runner))
        {
            Debug.Log($"[CombatTrace] Projectile despawned: Lifetime expired.", this);
            Runner.Despawn(Object);
            return;
        }

        // 2. Calculate accumulated distance and remaining range
        Vector2 currentPosition = _rigidbody.position;
        float traveledDistance = Vector2.Distance(SpawnPosition, currentPosition);
        float remainingRange = MaximumRange - traveledDistance;

        if (remainingRange <= 0f)
        {
            Debug.Log($"[CombatTrace] Projectile despawned: Range limit exceeded (travelled: {traveledDistance}, max: {MaximumRange}).", this);
            Runner.Despawn(Object);
            return;
        }

        // 3. Determine distance to travel in this tick
        float requestedDistance = Speed * Runner.DeltaTime;
        bool reachesMaximumRange = requestedDistance >= remainingRange;
        float travelDistance = Mathf.Min(requestedDistance, remainingRange);

        // 4. Perform collider cast using the projectile's real collision volume
        Physics2D.SyncTransforms();

        int hitCount = _projectileCollider.Cast(
            Direction,
            _contactFilter,
            _hitBuffer,
            travelDistance
        );

        RaycastHit2D selectedHit = default;
        EntityId selectedTargetId = default;
        bool foundImpact = false;

        // 5. Select the nearest non-owner impact, including static world geometry.
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _hitBuffer[i];
            if (!TryGetImpact(hit.collider, out EntityId targetId))
            {
                continue;
            }

            if (!foundImpact || hit.distance < selectedHit.distance)
            {
                selectedHit = hit;
                selectedTargetId = targetId;
                foundImpact = true;
            }
        }

        // Clear the local hit buffer
        System.Array.Clear(_hitBuffer, 0, hitCount);

        // 6. Process impact or move the projectile forward
        if (foundImpact)
        {
            if (!ImpactConsumed)
            {
                ImpactConsumed = true;

                // Move projectile to exact collision point, respecting its volume
                Vector2 impactPosition = currentPosition + Direction * selectedHit.distance;
                _rigidbody.position = impactPosition;
                transform.position = impactPosition;

                Debug.Log($"[CombatTrace] Projectile despawned: Impact resolved on collider {selectedHit.collider.name}. Distance: {selectedHit.distance}, HitPoint: {selectedHit.point}", this);

                // Attempt to apply damage if target is a damageable entity
                if (_damageResolver != null && selectedTargetId.Value != 0)
                {
                    DamageRequest damageRequest = new DamageRequest(
                        new EntityId(OwnerEntityIdValue),
                        selectedTargetId,
                        Damage,
                        DamageType,
                        Direction,
                        selectedHit.point,
                        Runner.Tick
                    );

                    _damageResolver.Resolve(in damageRequest);
                }

                Runner.Despawn(Object);
            }
        }
        else
        {
            // Normal movement without collisions detected
            Vector2 nextPosition = currentPosition + Direction * travelDistance;
            _rigidbody.position = nextPosition;
            transform.position = nextPosition;

            // Despawn if maximum range is reached in this tick
            if (reachesMaximumRange)
            {
                Debug.Log($"[CombatTrace] Projectile despawned: Reached exact maximum range.", this);
                Runner.Despawn(Object);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheComponents();
    }
#endif
}
