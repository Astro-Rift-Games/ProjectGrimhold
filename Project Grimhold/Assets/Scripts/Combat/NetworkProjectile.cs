using Fusion;
using UnityEngine;

/// <summary>
/// Network component responsible for projectile simulation.
/// Handles kinematic movement, collision detection using its own collider volume,
/// and applies authoritative damage under State Authority in <see cref="FixedUpdateNetwork"/>.
/// Contacts resolve in deterministic spatial order through the prefab's Doc 18 §5.2 impact contract.
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

    [Header("Impact Contract (Doc 18 §5.2)")]
    [Tooltip("Re-impact policy of this projectile type. Every policy deduplicates a target per event.")]
    [SerializeField]
    private ProjectileReimpactPolicy _reimpactPolicy = ProjectileReimpactPolicy.OncePerTarget;

    [Tooltip("Applied impacts that terminate the projectile. 1 stops on the first valid target.")]
    [SerializeField, Range(1, ProjectileImpactSettings.MaxImpactCountLimit)]
    private int _maxImpactCount = 1;

    [Tooltip("Seconds between pulses; used only by the PeriodicPulse policy.")]
    [SerializeField, Min(0.01f)]
    private float _pulseIntervalSeconds = 0.25f;

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
    private NetworkBool IsUnbounded { get; set; }

    [Networked]
    private NetworkBool EnemyOnly { get; set; }

    // Committed source affiliation, independent of the shooter's later eligibility or registry lifetime.
    // Spawned/rebind reconstruct local dependencies only; Fusion copies this with the flight snapshot.
    [Networked]
    private NetworkBool SourceWasPvEEnemy { get; set; }

    [Networked]
    private int ImpactLayerMaskValue { get; set; }

    [Networked]
    private NetworkBool ImpactConsumed { get; set; }

    [Networked]
    private NetworkBool ImpactWasGeometryOnly { get; set; }

    [Networked]
    private int SpawnSimulationTick { get; set; }

    [Networked]
    private float KnockbackForce { get; set; }

    [Networked]
    private int EmpowerSourceParticipantValue { get; set; }

    // Re-impact progress is flight state: Fusion copies it with the projectile snapshot.
    [Networked]
    private int AppliedImpactCount { get; set; }

    [Networked]
    private int NextPulseTick { get; set; }

    [Networked, Capacity(ProjectileImpactLedger.Capacity)]
    private NetworkArray<int> LedgerTargetIds { get; }

    [Networked, Capacity(ProjectileImpactLedger.Capacity)]
    private NetworkArray<int> LedgerContactTicks { get; }

    public bool IsImpactConsumed => ImpactConsumed;
    public bool IsImpactGeometryOnly => ImpactWasGeometryOnly;

    public event System.Action<Vector2, bool> ImpactResolved;

    private const int HitBufferCapacity = 16;

    private ContactFilter2D _contactFilter;
    private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[HitBufferCapacity];
    private readonly Collider2D[] _overlapBuffer = new Collider2D[HitBufferCapacity];
    private readonly ProjectileContact[] _contacts = new ProjectileContact[HitBufferCapacity * 2];
    private readonly Collider2D[] _contactColliders = new Collider2D[HitBufferCapacity * 2];
    private readonly ProjectileImpactLedger _ledger = new ProjectileImpactLedger();
    private ProjectileImpactSettings _impactSettings = ProjectileImpactSettings.SingleImpact;
    private bool _hasValidImpactSettings;
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

        _hasValidImpactSettings = ProjectileImpactSettings.TryCreate(_reimpactPolicy, _maxImpactCount,
            _pulseIntervalSeconds, Runner.DeltaTime, out _impactSettings, out string impactError);
        if (!_hasValidImpactSettings && HasStateAuthority)
        {
            Debug.LogError($"{nameof(NetworkProjectile)} impact contract is invalid: {impactError} The projectile is despawned without impacts.", this);
        }

        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(NetworkProjectile)}: EntityRegistry was not found on the NetworkRunner GameObject.", this);
        }

        // Independent flight uses the projectile's own authoritative resolver, including after restore.
        // Its Spawned() reconstructs runner-local registry/manager references; no caster reference survives.
        if (IsUnbounded)
        {
            _damageResolver = GetComponent<DamageResolver>();
            if (_damageResolver == null)
                Debug.LogError($"{nameof(NetworkProjectile)} requires a projectile-local {nameof(DamageResolver)} for unbounded flight.", this);
        }

        // Align physical starting state and force Unity physics synchronization
        if (Direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (_rigidbody != null)
            {
                _rigidbody.rotation = angle;
            }
        }

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
        bool hasState = Object != null && Object.IsValid;
        return TryGetImpact(collider, new EntityId(hasState ? OwnerEntityIdValue : 0),
            hasState && EnemyOnly, out targetId);
    }

    private bool TryGetImpact(Collider2D collider, EntityId ownerId, bool enemyOnly, out EntityId targetId)
    {
        targetId = default;

        if (collider == null || collider == _projectileCollider)
        {
            return false;
        }

        // Orb children are local interception geometry, never registry entities or ordinary world blockers.
        // PvE player projectiles (EnemyOnly), own/friendly and consumed orbs must pass through them.
        if (collider.TryGetComponent(out ProtectiveOrbCollider orb))
            return !enemyOnly && Object != null && Object.IsValid &&
                orb.CanInterceptEnemyProjectileImpact(ownerId, SourceWasPvEEnemy);

        // Static world geometry has no entity id but still consumes the projectile.
        if (_registry == null || !_registry.TryGetEntityId(collider, out targetId))
        {
            return true;
        }

        if (targetId == ownerId) return false;
        if (!enemyOnly) return _registry.IsDamageCollider(targetId, collider);
        // Collision and effect eligibility are separate (Doc 18 §5.1): excluded allies are passed through.
        if (_registry.TryGetDamageable(targetId, out IDamageable candidate) &&
            (candidate is ICharacter || candidate is IAbilityEnemyTarget))
            return _registry.IsDamageCollider(targetId, collider) &&
                AbilityTargetPredicate.IsValidEnemy(ownerId, candidate);

        // Registered non-character world objects are geometry in the PvE ability mode, not damage targets.
        targetId = default;
        return true;
    }

    /// <summary>
    /// Initializes network properties of the projectile prior to spawning completing.
    /// Can only be called during the initialization callback of the spawner on the State Authority.
    /// </summary>
    public void InitializeNetworkState(in ProjectileSpawnRequest request, int impactLayerMaskValue,
        RaidParticipantId empowerSourceId = default)
    {
        if (!HasStateAuthority) return;

        EntityRegistry sourceRegistry = Runner.GetComponent<EntityRegistry>();
        SourceWasPvEEnemy = sourceRegistry != null &&
            sourceRegistry.TryGetDamageable(request.OwnerId, out IDamageable source) &&
            source is IAbilityEnemyTarget;
        OwnerEntityIdValue = request.OwnerId.Value;
        EmpowerSourceParticipantValue = empowerSourceId.IsValid ? empowerSourceId.Value : 0;
        Damage = request.Damage;
        DamageType = request.DamageType;
        Direction = request.Direction;
        Speed = request.Speed;
        SpawnPosition = request.Origin;
        MaximumRange = request.MaximumRange;
        IsUnbounded = request.IsUnbounded;
        EnemyOnly = request.EnemyOnly;
        LifetimeTimer = request.IsUnbounded ? TickTimer.None : TickTimer.CreateFromSeconds(Runner, request.LifetimeSeconds);
        ImpactLayerMaskValue = impactLayerMaskValue;
        ImpactConsumed = false;
        ImpactWasGeometryOnly = false;
        SpawnSimulationTick = request.SimulationTick;
        KnockbackForce = request.KnockbackForce;
        AppliedImpactCount = 0;
        NextPulseTick = Runner.Tick.Raw;
        for (int i = 0; i < ProjectileImpactLedger.Capacity; i++)
        {
            LedgerTargetIds.Set(i, 0);
            LedgerContactTicks.Set(i, 0);
        }

        Debug.Log($"[CombatTrace] Projectile initialized. OwnerId: {OwnerEntityIdValue}, Damage: {Damage}, Lifetime: {request.LifetimeSeconds}, Tick: {SpawnSimulationTick}", this);
    }

    /// <summary>
    /// Assigns required runtime dependencies.
    /// </summary>
    public void SetRuntimeDependencies(IDamageResolver damageResolver)
    {
        _damageResolver = damageResolver;
    }

    public override void Render()
    {
        if (Direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Execute gameplay simulation exclusively under State Authority
        if (!HasStateAuthority || !Runner.IsForward || ImpactConsumed)
        {
            return;
        }

        // Every projectile must have a valid end condition; an invalid contract never flies.
        if (!_hasValidImpactSettings)
        {
            Runner.Despawn(Object);
            return;
        }

        // 1. Validate expiration by lifetime
        if (!IsUnbounded && LifetimeTimer.ExpiredOrNotRunning(Runner))
        {
            Debug.Log($"[CombatTrace] Projectile despawned: Lifetime expired.", this);
            Runner.Despawn(Object);
            return;
        }

        // 2. Calculate accumulated distance and remaining range
        Vector2 currentPosition = _rigidbody.position;
        float remainingRange = 0f;
        if (!IsUnbounded)
        {
            float traveledDistance = Vector2.Distance(SpawnPosition, currentPosition);
            remainingRange = MaximumRange - traveledDistance;
            if (remainingRange <= 0f)
            {
                Debug.Log($"[CombatTrace] Projectile despawned: Range limit exceeded (travelled: {traveledDistance}, max: {MaximumRange}).", this);
                Runner.Despawn(Object);
                return;
            }
        }

        // 3. Unbounded means no distance/lifetime policy, not an invented huge limit.
        float requestedDistance = Speed * Runner.DeltaTime;
        bool reachesMaximumRange = !IsUnbounded && requestedDistance >= remainingRange;
        float travelDistance = IsUnbounded ? requestedDistance : Mathf.Min(requestedDistance, remainingRange);

        // 4. Gather contacts along the tick's sweep using the projectile's real collision volume.
        Physics2D.SyncTransforms();
        int contactCount = GatherContacts(currentPosition, travelDistance);

        // 5. Resolve contacts in authoritative spatial order through the declared impact contract.
        //    Contacts after the terminating one are discarded (Doc 18 §5.2).
        contactCount = ProjectileContactResolver.SortAndDeduplicate(_contacts, contactCount);
        int eventTick = Runner.Tick.Raw;
        bool pulseDue = _impactSettings.Policy == ProjectileReimpactPolicy.PeriodicPulse && eventTick >= NextPulseTick;
        if (pulseDue) NextPulseTick = eventTick + _impactSettings.PulseIntervalTicks;

        LoadLedger();
        var sink = new ContactSink(this);
        ProjectileContactResolution resolution = ProjectileContactResolver.Resolve(_contacts, contactCount,
            _impactSettings, _ledger, eventTick, pulseDue, AppliedImpactCount, ref sink);
        AppliedImpactCount = resolution.ImpactsApplied;
        StoreLedger();

        ProjectileContact terminal = resolution.IsTerminated ? _contacts[resolution.TerminalIndex] : default;
        System.Array.Clear(_contacts, 0, _contacts.Length);
        System.Array.Clear(_contactColliders, 0, _contactColliders.Length);

        // 6. Terminate at the deciding contact or move the projectile forward.
        if (resolution.IsTerminated)
        {
            // Functional disappearance is immediate; VFX trails observe Despawned independently.
            ImpactConsumed = true;
            ImpactWasGeometryOnly = terminal.Kind != ProjectileContactKind.Target;

            Vector2 impactPosition = currentPosition + Direction * terminal.Distance;
            _rigidbody.position = impactPosition;
            transform.position = impactPosition;

            Debug.Log($"[CombatTrace] Projectile despawned: {terminal.Kind} contact terminated it. Distance: {terminal.Distance}, Impacts: {AppliedImpactCount}", this);
            Runner.Despawn(Object);
            return;
        }

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

    private int GatherContacts(Vector2 currentPosition, float travelDistance)
    {
        int count = 0;
        int hitCount = _projectileCollider.Cast(Direction, _contactFilter, _hitBuffer, travelDistance);
        for (int i = 0; i < hitCount; i++)
            TryAddContact(_hitBuffer[i].collider, _hitBuffer[i].distance, _hitBuffer[i].point, ref count);
        System.Array.Clear(_hitBuffer, 0, hitCount);

        // Re-impact policies need contacts the projectile is already inside, not only those ahead of it.
        if (_impactSettings.TracksContinuousContact)
        {
            int overlapCount = _projectileCollider.Overlap(_contactFilter, _overlapBuffer);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D collider = _overlapBuffer[i];
                if (collider != null)
                    TryAddContact(collider, 0f, collider.ClosestPoint(currentPosition), ref count);
            }

            System.Array.Clear(_overlapBuffer, 0, overlapCount);
        }

        return count;
    }

    private void TryAddContact(Collider2D collider, float distance, Vector2 point, ref int count)
    {
        if (count >= _contacts.Length || !TryGetImpact(collider, out EntityId targetId)) return;

        ProjectileContact contact;
        if (collider.TryGetComponent(out ProtectiveOrbCollider orb))
            contact = new ProjectileContact(ProjectileContactKind.Interceptor, orb.OwnerId.Value, orb.SlotIndex,
                distance, point, count);
        else if (targetId.Value == 0)
            contact = new ProjectileContact(ProjectileContactKind.Blocker, 0, 0, distance, point, count);
        else
            contact = new ProjectileContact(ProjectileContactKind.Target, targetId.Value, 0, distance, point, count);

        _contacts[count] = contact;
        _contactColliders[count] = collider;
        count++;
    }

    private void LoadLedger()
    {
        for (int i = 0; i < ProjectileImpactLedger.Capacity; i++)
            _ledger.SetEntry(i, LedgerTargetIds.Get(i), LedgerContactTicks.Get(i));
    }

    private void StoreLedger()
    {
        for (int i = 0; i < ProjectileImpactLedger.Capacity; i++)
        {
            LedgerTargetIds.Set(i, _ledger.GetTargetId(i));
            LedgerContactTicks.Set(i, _ledger.GetTick(i));
        }
    }

    private bool TryInterceptContact(in ProjectileContact contact)
    {
        // Commit only the resolved orb. An inactive slot is passed through, including when a previous
        // projectile consumed it in this same tick, so it can never hide the contacts behind it.
        Collider2D collider = _contactColliders[contact.SourceIndex];
        return !EnemyOnly && collider != null && collider.TryGetComponent(out ProtectiveOrbCollider orb) &&
            orb.TryConsumeEnemyProjectileImpact(new EntityId(OwnerEntityIdValue), SourceWasPvEEnemy);
    }

    private void ApplyContactImpact(in ProjectileContact contact)
    {
        if (_damageResolver == null) return;

        RaidParticipantId.TryCreate(EmpowerSourceParticipantValue, out RaidParticipantId empowerSourceId);
        DamageRequest damageRequest = new DamageRequest(
            new EntityId(OwnerEntityIdValue),
            new EntityId(contact.EntityIdValue),
            Damage,
            DamageType,
            Direction,
            contact.Point,
            Runner.Tick,
            KnockbackForce,
            empowerSourceId: empowerSourceId
        );

        _damageResolver.Resolve(in damageRequest);
    }

    private readonly struct ContactSink : IProjectileContactSink
    {
        private readonly NetworkProjectile _projectile;

        public ContactSink(NetworkProjectile projectile) => _projectile = projectile;

        public bool TryIntercept(in ProjectileContact contact) => _projectile.TryInterceptContact(in contact);

        public void ApplyImpact(in ProjectileContact contact) => _projectile.ApplyContactImpact(in contact);
    }

    internal int GetRestoredOwnerEntityIdValue() => OwnerEntityIdValue;
    internal void SetRestoredOwnerEntityId(EntityId newOwnerId)
    {
        OwnerEntityIdValue = newOwnerId.Value;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (hasState && ImpactConsumed)
        {
            ImpactResolved?.Invoke(transform.position, ImpactWasGeometryOnly);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheComponents();
    }
#endif
}
