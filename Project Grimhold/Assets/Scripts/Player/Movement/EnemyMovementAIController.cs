using Fusion;
using UnityEngine;

/// <summary>
/// Drives enemy AI movement during network simulation ticks and evaluates
/// sensor state (target detection, line-of-sight, attack range) on behalf
/// of the State Authority.
///
/// Responsibilities:
/// - Detect player targets via periodic Physics2D.OverlapCircle scans (Patrol/Idle).
/// - Evaluate distance and line-of-sight against the active target every tick (Chase/Attack).
/// - Compute movement direction for pursuit.
/// - Replicate locomotion state (FacingDirection, IsMoving, IsOnPursuit, IsAttacking)
///   so the FSM and presentation layers can read authoritative output.
///
/// This component does NOT decide FSM transitions. It reports sensor results;
/// EnemyFSM states observe those results and call TransitionTo.
///
/// See Docs/Architecture/EnemyFSMArchitecture.md for the complete flow.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Kinematic2DMovementMotor))]
public sealed class EnemyMovementAIController : NetworkBehaviour, IMovementState
{
    // ─────────────────────────────────────────────────────────────────────────
    // Serialized configuration
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Movement")]
    [SerializeField, Min(0f)] private float _patrolSpeed = 3f;
    [SerializeField, Min(1f)] private float _pursuitSpeedMultiplier = 1.5f;
    [SerializeField] private Vector2 _defaultFacingDirection = Vector2.down;

    [Header("Patrol")]
    [SerializeField] private EnemyPatrolRoute _patrolRoute;
    [SerializeField, Min(0f)] private float _waypointReachRadius = 0.3f;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float _detectionRange = 6f;

    [Tooltip("Range at which the enemy stops pursuing. Must be >= Detection Range.")]
    [SerializeField, Min(0f)] private float _disengageRange = 8f;

    [Tooltip("Ticks of continuous LOS loss before pursuit is abandoned (within disengage range).")]
    [SerializeField, Min(0)] private int _pursuitLostGraceTicks = 10;

    [SerializeField] private float _attackRange = 1.5f;

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("Scan")]
    [Tooltip("Interval in seconds between target scans when no target is active.")]
    [SerializeField, Min(0.05f)] private float _scanInterval = 0.1f;

    [Tooltip("Layer mask used to find player colliders during OverlapCircle scans.")]
    [SerializeField] private LayerMask _playerLayer;

    [Header("Dependencies")]
    [SerializeField] private Kinematic2DMovementMotor _movementMotor;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal constants
    // ─────────────────────────────────────────────────────────────────────────

    private const float ValidDirectionSqrThreshold = 0.0001f;
    private const float ValidMovementSqrThreshold = 0.000001f;
    private const int MaxScanCandidates = 8;

    // ─────────────────────────────────────────────────────────────────────────
    // Private runtime state (non-networked; lives only on State Authority)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Immutable snapshot of the current target identity and its Unity Transform.
    /// Replaced atomically; never mutated in place.
    /// </summary>
    private readonly struct EnemyTargetReference
    {
        public EntityId Id { get; }
        public Transform Transform { get; }

        public EnemyTargetReference(EntityId id, Transform transform)
        {
            Id = id;
            Transform = transform;
        }
    }

    private EnemyTargetReference _currentTarget;

    /// <summary>
    /// Consecutive ticks the active target has been out of LOS while still within
    /// _disengageRange. Pursuit is dropped when this reaches _pursuitLostGraceTicks.
    /// Not networked: lives on State Authority only.
    /// </summary>
    private int _pursuitLostTickCount;

    /// <summary>
    /// Pre-allocated buffer for OverlapCircleNonAlloc. Avoids per-scan allocation.
    /// </summary>
    private readonly Collider2D[] _overlapBuffer = new Collider2D[MaxScanCandidates];

    private bool _dependenciesValid;
    private CharacterBase _characterBase;
    private EntityRegistry _entityRegistry;

    // ─────────────────────────────────────────────────────────────────────────
    // Networked state
    // ─────────────────────────────────────────────────────────────────────────

    [Networked] public NetworkBool IsControlEnabled { get; private set; }
    [Networked] public Vector2 FacingDirection { get; private set; }
    [Networked] public NetworkBool IsMoving { get; private set; }

    /// <summary>
    /// True when the enemy has an active target within attack range.
    /// Written exclusively by EvaluateActiveTarget; read by EnemyFSM states and EnemyCombatAIController.
    /// </summary>
    [Networked] public NetworkBool IsAttacking { get; private set; }

    /// <summary>
    /// True when the enemy is actively pursuing a detected target.
    /// Written exclusively by EvaluateActiveTarget; read by EnemyFSM states.
    /// </summary>
    [Networked] public NetworkBool IsOnPursuit { get; private set; }

    /// <summary>
    /// Throttle timer for periodic OverlapCircle scans when no target is active.
    /// Used only in Idle/Patrol context (no active target).
    /// </summary>
    [Networked] private TickTimer ScanTimer { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // IMovementState — derived values (no extra Networked properties needed)
    // ─────────────────────────────────────────────────────────────────────────

    private float PursuitSpeed => _patrolSpeed * _pursuitSpeedMultiplier;

    /// <summary>
    /// True when a patrol route component is present and has at least one waypoint.
    /// </summary>
    public bool HasPatrolRoute => _patrolRoute != null && _patrolRoute.HasWaypoints;

    /// <summary>
    /// Current index in the patrol route. Modified only by the State Authority during patrol.
    /// </summary>
    [Networked] public int PatrolWaypointIndex { get; private set; }

    /// <summary>
    /// Set to true by EnemyPatrolState to indicate that the movement controller should execute patrol movement.
    /// </summary>
    public bool IsPatrolActive { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _entityRegistry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _dependenciesValid = ValidateDependencies();

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            IsControlEnabled = true;

            Vector2 initialFacing = _defaultFacingDirection.sqrMagnitude > 0.001f
                ? _defaultFacingDirection.normalized
                : Vector2.down;

            FacingDirection = initialFacing;
            IsMoving = false;
            IsOnPursuit = false;
            IsAttacking = false;
            _pursuitLostTickCount = 0;
            PatrolWaypointIndex = 0;
            IsPatrolActive = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fusion simulation
    // ─────────────────────────────────────────────────────────────────────────

    public override void FixedUpdateNetwork()
    {
        if (!_dependenciesValid || !HasStateAuthority)
        {
            return;
        }

        IsMoving = false;

        // Update sensor flags exactly once per tick; FSM states read the results.
        EvaluateSensors();

        Vector2 moveDirection = ComputeMoveDirection();

        bool canMove = IsControlEnabled && (_characterBase == null || _characterBase.IsAlive);

        if (canMove && moveDirection.sqrMagnitude > ValidDirectionSqrThreshold)
        {
            FacingDirection = moveDirection.normalized;
        }

        float speed = IsOnPursuit ? PursuitSpeed : _patrolSpeed;
        Vector2 displacement = canMove
            ? moveDirection * speed * Runner.DeltaTime
            : Vector2.zero;

        Vector2 appliedDisplacement = _movementMotor.Move(displacement);

        if (appliedDisplacement.sqrMagnitude > ValidMovementSqrThreshold)
        {
            IsMoving = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API (called by FSM states and EnemyCombatAIController)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Authoritatively enables or disables movement control.
    /// Requires State Authority.
    /// </summary>
    public bool TrySetControlEnabled(bool enabled)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        IsControlEnabled = enabled;
        return true;
    }

    /// <summary>
    /// Attempts to retrieve the current canonical target identity and transform reference.
    /// Returns a snapshot without mutating internal state.
    /// </summary>
    /// <param name="targetId">Outputs the stored target EntityId.</param>
    /// <param name="targetTransform">Outputs the cached Transform (may be null if destroyed).</param>
    /// <returns>
    /// <see langword="true"/> when the stored ID is non-zero and Transform is not null.
    /// </returns>
    public bool TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform)
    {
        targetId = _currentTarget.Id;
        targetTransform = _currentTarget.Transform == null ? null : _currentTarget.Transform;
        return targetId.Value != 0 && targetTransform != null;
    }

    /// <summary>
    /// Clears the current target only when the expected ID matches the stored one.
    /// Protects newly acquired targets from late invalidations triggered by a previous target.
    /// Requires State Authority when the object is valid and in a live session.
    /// </summary>
    /// <param name="expectedTargetId">The entity ID expected to be invalidated.</param>
    /// <returns>
    /// <see langword="true"/> when the stored target matched and was cleared.
    /// </returns>
    public bool TryInvalidateCurrentTarget(EntityId expectedTargetId)
    {
        if (Object != null && Object.IsValid && !HasStateAuthority)
        {
            return false;
        }

        if (expectedTargetId.Value == 0)
        {
            return false;
        }

        if (_currentTarget.Id != expectedTargetId)
        {
            return false;
        }

        ClearCurrentTarget();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sensor evaluation (State Authority only, called once per tick)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates IsOnPursuit and IsAttacking based on the current target state.
    /// Exactly one write to each flag per tick.
    ///
    /// When a target is active, evaluates distance and LOS directly (O(1)).
    /// When no target is active, runs a periodic OverlapCircle scan.
    /// </summary>
    private void EvaluateSensors()
    {
        bool hasActiveTarget = TryGetCurrentTarget(out EntityId currentTargetId, out Transform currentTargetTransform);

        if (hasActiveTarget)
        {
            EvaluateActiveTarget(currentTargetId, currentTargetTransform);
        }
        else
        {
            // currentTargetId may be non-zero with a destroyed Transform (stale reference).
            if (currentTargetId.Value != 0)
            {
                ClearCurrentTarget();
            }

            IsOnPursuit = false;
            IsAttacking = false;

            // Scan for new targets on a throttled interval to avoid OverlapCircle every tick.
            if (ScanTimer.ExpiredOrNotRunning(Runner))
            {
                ScanForTargets();
                ScanTimer = TickTimer.CreateFromSeconds(Runner, _scanInterval);
            }
        }
    }

    /// <summary>
    /// Validates the active target and updates IsOnPursuit / IsAttacking.
    /// Runs every tick when a target is already acquired (no OverlapCircle needed).
    /// Handles grace period for LOS loss within disengage range.
    /// </summary>
    private void EvaluateActiveTarget(EntityId targetId, Transform targetTransform)
    {
        // Validate target is still eligible (alive, damageable).
        if (!TryResolveEligibleTarget(targetId))
        {
            ClearCurrentTarget();
            IsOnPursuit = false;
            IsAttacking = false;
            return;
        }

        Vector2 enemyPos = transform.position;
        Vector2 targetPos = targetTransform.position;
        float distance = Vector2.Distance(enemyPos, targetPos);

        // Target moved beyond disengage range: drop immediately.
        if (distance > _disengageRange)
        {
            ClearCurrentTarget();
            IsOnPursuit = false;
            IsAttacking = false;
            _pursuitLostTickCount = 0;
            return;
        }

        // Check attack range first (does not require LOS).
        if (distance <= _attackRange)
        {
            _pursuitLostTickCount = 0;
            IsAttacking = true;
            IsOnPursuit = true;
            return;
        }

        // Check LOS for pursuit.
        bool hasLOS = !Physics2D.Linecast(enemyPos, targetPos, _obstacleLayer);

        if (hasLOS)
        {
            _pursuitLostTickCount = 0;
            IsOnPursuit = true;
            IsAttacking = false;
            return;
        }

        // LOS lost within disengage range: apply grace period before dropping target.
        _pursuitLostTickCount++;
        if (_pursuitLostTickCount >= _pursuitLostGraceTicks)
        {
            ClearCurrentTarget();
            IsOnPursuit = false;
            IsAttacking = false;
            _pursuitLostTickCount = 0;
        }
        else
        {
            // Within grace: maintain pursuit flag so the enemy continues moving toward last known position.
            IsOnPursuit = true;
            IsAttacking = false;
        }
    }

    /// <summary>
    /// Scans for player targets using a non-allocating OverlapCircle.
    /// Selects the best candidate deterministically: closest distance, with EntityId.Value
    /// as a stable tiebreaker. Does not depend on the order OverlapCircleNonAlloc returns colliders.
    /// </summary>
    private void ScanForTargets()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            _detectionRange,
            _overlapBuffer,
            _playerLayer);

        EntityId bestId = default;
        Transform bestTransform = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = _overlapBuffer[i];
            if (col == null)
            {
                continue;
            }

            if (!_entityRegistry.TryGetEntityId(col, out EntityId candidateId))
            {
                continue;
            }

            // Skip additional colliders belonging to the same entity already selected.
            // Entities register multiple colliders (body + hitboxes); only process each entity once.
            if (candidateId == bestId)
            {
                continue;
            }

            if (!TryResolveEligibleTarget(candidateId))
            {
                continue;
            }

            // Use the collider's root transform to measure distance from the entity's origin,
            // not from a potentially offset hitbox child.
            Transform candidateTransform = col.attachedRigidbody != null
                ? col.attachedRigidbody.transform
                : col.transform;

            Vector2 candidatePos = candidateTransform.position;
            Vector2 enemyPos = transform.position;

            // LOS check: Linecast is sufficient for detection (CircleCast is reserved for wall-steering).
            bool hasLOS = !Physics2D.Linecast(enemyPos, candidatePos, _obstacleLayer);
            if (!hasLOS)
            {
                continue;
            }

            float distance = Vector2.Distance(enemyPos, candidatePos);

            // Deterministic selection: prefer closer target; break ties by EntityId.Value.
            bool betterCandidate = distance < bestDistance
                || (Mathf.Approximately(distance, bestDistance) && candidateId.Value < bestId.Value);

            if (betterCandidate)
            {
                bestId = candidateId;
                bestTransform = candidateTransform;
                bestDistance = distance;
            }
        }

        if (bestId.Value != 0 && bestTransform != null)
        {
            _currentTarget = new EnemyTargetReference(bestId, bestTransform);
            // ScanTimer will restart on the next EvaluateSensors call with no target;
            // since we now have a target, EvaluateActiveTarget takes over next tick.
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Movement direction computation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the normalized movement direction for this tick.
    /// Pursues the active target when IsOnPursuit is true.
    /// Patrols when IsPatrolActive is true.
    /// Returns zero when neither pursuit nor patrol direction is applicable.
    /// </summary>
    private Vector2 ComputeMoveDirection()
    {
        if (IsOnPursuit && TryGetCurrentTarget(out _, out Transform targetTransform))
        {
            return ComputePursuitDirection(targetTransform);
        }

        if (IsPatrolActive)
        {
            return ComputePatrolDirection();
        }

        return Vector2.zero;
    }

    /// <summary>
    /// Computes the normalized direction toward the current waypoint.
    /// Advances the waypoint index if the current waypoint is reached.
    /// </summary>
    private Vector2 ComputePatrolDirection()
    {
        if (!HasPatrolRoute)
        {
            return Vector2.zero;
        }

        if (_patrolRoute.TryGetWaypoint(PatrolWaypointIndex, out Transform waypoint))
        {
            Vector2 toWaypoint = (Vector2)waypoint.position - (Vector2)transform.position;
            if (toWaypoint.sqrMagnitude < _waypointReachRadius * _waypointReachRadius)
            {
                PatrolWaypointIndex = (PatrolWaypointIndex + 1) % _patrolRoute.Count;
                
                // Recalculate direction towards the new waypoint immediately to avoid a 1-tick stop.
                if (_patrolRoute.TryGetWaypoint(PatrolWaypointIndex, out Transform nextWaypoint))
                {
                    toWaypoint = (Vector2)nextWaypoint.position - (Vector2)transform.position;
                    return toWaypoint.normalized;
                }
            }
            
            return toWaypoint.normalized;
        }

        return Vector2.zero;
    }

    /// <summary>
    /// Returns the normalized direction from the enemy toward the active target.
    /// Etapa 3: EnemyObstacleAvoidance.Steer will be applied here.
    /// </summary>
    private Vector2 ComputePursuitDirection(Transform target)
    {
        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < ValidDirectionSqrThreshold)
        {
            return Vector2.zero;
        }

        return toTarget.normalized;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears the stored target reference and resets pursuit/attack flags.
    /// Also resets the grace tick counter.
    /// </summary>
    private void ClearCurrentTarget()
    {
        _currentTarget = default;
        _pursuitLostTickCount = 0;

        if (Object != null && Object.IsValid)
        {
            IsOnPursuit = false;
            IsAttacking = false;
        }
    }

    /// <summary>
    /// Returns true when the entity identified by targetId is alive and can receive damage.
    /// </summary>
    private bool TryResolveEligibleTarget(EntityId targetId)
    {
        return targetId.Value != 0
            && _entityRegistry != null
            && _entityRegistry.TryGetCharacter(targetId, out ICharacter character)
            && character != null
            && character.IsAlive
            && _entityRegistry.TryGetDamageable(targetId, out IDamageable damageable)
            && damageable != null
            && damageable.CanReceiveDamage;
    }

    private void CacheDependencies()
    {
        if (_movementMotor == null)
        {
            _movementMotor = GetComponent<Kinematic2DMovementMotor>();
        }

        if (_characterBase == null)
        {
            _characterBase = GetComponent<CharacterBase>();
        }
    }

    private bool ValidateDependencies()
    {
        if (_movementMotor == null)
        {
            Debug.LogError(
                $"{nameof(EnemyMovementAIController)} requires {nameof(Kinematic2DMovementMotor)}.",
                this);
            return false;
        }

        if (_entityRegistry == null)
        {
            Debug.LogError(
                $"{nameof(EnemyMovementAIController)} requires a runner-scoped {nameof(EntityRegistry)}.",
                this);
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_disengageRange < _detectionRange)
        {
            _disengageRange = _detectionRange;
        }

        if (_movementMotor == null)
        {
            _movementMotor = GetComponent<Kinematic2DMovementMotor>();
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 pos = transform.position;

        // Detection range (green): initiates pursuit.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos, _detectionRange);

        // Disengage range (yellow): pursuit dropped only beyond this.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, _disengageRange);

        // Attack range (red): triggers attack state.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, _attackRange);
    }
#endif
}
