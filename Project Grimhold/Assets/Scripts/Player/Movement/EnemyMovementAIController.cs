using Fusion;
using UnityEngine;

/// <summary>
/// Consumes Fusion input during network ticks and delegates movement
/// resolution to the kinematic movement motor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Kinematic2DMovementMotor))]
public sealed class EnemyMovementAIController : NetworkBehaviour, IMovementState
{
    [Min(0f)]
    private float _moveSpeed;
    [SerializeField, Min(0f)] private float _patrolSpeed = 3f;
    [SerializeField, Min(1f)] private float _pursuitSpeedMultiplier = 1.5f;
    private float PursuitSpeed => _patrolSpeed * _pursuitSpeedMultiplier;
    private Vector2 _moveDirection;
    [SerializeField] private float _decisionInterval = 1f; // Time interval between decisions in seconds

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
    private PlayerCharacter[] _targetCandidates;
    [SerializeField] private float _LOSDistance = 6f;
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private LayerMask _obstacleLayer;

    [SerializeField]
    private Kinematic2DMovementMotor _movementMotor;

    private bool _dependenciesValid;

    private const float ValidDirectionSqrThreshold = 0.0001f;
    private const float ValidMovementSqrThreshold = 0.000001f;

    [SerializeField]
    private Vector2 _defaultFacingDirection = Vector2.down;

    [Networked]
    public NetworkBool IsControlEnabled { get; private set; }

    [Networked]
    public Vector2 FacingDirection { get; private set; }

    [Networked]
    public NetworkBool IsMoving { get; private set; }

    [Networked]
    public NetworkBool IsAttacking { get; private set; }

    [Networked]
    public NetworkBool IsOnPursuit { get; private set; }

    [Networked]
    private TickTimer DecisionTimer { get; set; }

    private CharacterBase _characterBase;
    private EntityRegistry _entityRegistry;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _entityRegistry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _dependenciesValid = ValidateDependencies();

        if (HasStateAuthority)
        {
            IsControlEnabled = true;

            Vector2 initialFacing = _defaultFacingDirection.normalized;
            if (initialFacing.sqrMagnitude < 0.001f)
            {
                initialFacing = Vector2.down;
            }
            FacingDirection = initialFacing;
            IsMoving = false;
            CheckPotentialTargets();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_dependenciesValid || !HasStateAuthority)
        {
            return;
        }

        IsMoving = false;

        Vector2 moveDirection = ReadMoveDirection();

        bool canMove = IsControlEnabled && (_characterBase == null || _characterBase.IsAlive);

        if (canMove && moveDirection.sqrMagnitude > ValidDirectionSqrThreshold)
        {
            FacingDirection = moveDirection.normalized;
        }

        Vector2 displacement = canMove
            ? moveDirection * _moveSpeed * Runner.DeltaTime
            : Vector2.zero;

        Vector2 appliedDisplacement = _movementMotor.Move(displacement);

        if (appliedDisplacement.sqrMagnitude > ValidMovementSqrThreshold)
        {
            IsMoving = true;
        }
    }

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
    /// Copies stored snapshot without mutating internal state.
    /// </summary>
    /// <param name="targetId">Outputs the stored target EntityId.</param>
    /// <param name="targetTransform">Outputs the cached Transform reference (may be null if destroyed).</param>
    /// <returns><see langword="true"/> if stored target ID is non-zero and Transform is not null; otherwise, <see langword="false"/>.</returns>
    public bool TryGetCurrentTarget(out EntityId targetId, out Transform targetTransform)
    {
        targetId = _currentTarget.Id;
        targetTransform = _currentTarget.Transform == null ? null : _currentTarget.Transform;

        return targetId.Value != 0 && targetTransform != null;
    }

    /// <summary>
    /// Attempts to invalidate the current target if the expected target ID matches the currently stored target ID.
    /// </summary>
    /// <param name="expectedTargetId">The entity ID expected to be invalidated.</param>
    /// <returns><see langword="true"/> if the stored target matched and was invalidated; otherwise, <see langword="false"/>.</returns>
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

        _currentTarget = default;
        if (Object != null && Object.IsValid)
        {
            IsOnPursuit = false;
            IsAttacking = false;
        }
        return true;
    }

    private void SetCurrentTarget(EntityId id, Transform targetTransform)
    {
        if (id.Value == 0 || targetTransform == null)
        {
            _currentTarget = default;
            if (Object != null && Object.IsValid)
            {
                IsOnPursuit = false;
                IsAttacking = false;
            }
            return;
        }

        _currentTarget = new EnemyTargetReference(id, targetTransform);
    }

    private Vector2 ReadMoveDirection()
    {
        if (!DecideDirection(out Vector2 decision))
        {
            return Vector2.zero;
        }

        _moveSpeed = IsOnPursuit ? PursuitSpeed : _patrolSpeed;

        return decision.normalized;
    }

    private bool DecideDirection(out Vector2 decision)
    {
        bool hasTarget = HasTarget(out Transform target, out float disToTarget);
        bool hasLOS = HasLOS(target, disToTarget);
        bool onRange = IsInAttackRange(target);

        if (onRange)
        {
            _moveDirection = Vector2.zero;
            if (target != null)
            {
                Vector2 aimDir = (target.position - transform.position).normalized;
                if (aimDir.sqrMagnitude > ValidDirectionSqrThreshold)
                {
                    FacingDirection = aimDir;
                }
            }
        }
        else if (hasLOS)
        {
            // Follow target
            _moveDirection = (target.position - transform.position).normalized;
        }
        else if (DecisionTimer.ExpiredOrNotRunning(Runner))
        {
            // Randomly choose a direction deterministically across ticks
            _moveDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            DecisionTimer = TickTimer.CreateFromSeconds(Runner, _decisionInterval);
        }

        decision = _moveDirection;
        return true;
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

    private void CheckPotentialTargets()
    {
        _targetCandidates = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude);
    }

    private bool ValidateDependencies()
    {
        if (_movementMotor != null && _entityRegistry != null)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(EnemyMovementAIController)} requires " +
            $"{nameof(Kinematic2DMovementMotor)} and a runner-scoped {nameof(EntityRegistry)}.",
            this);

        return false;
    }

    private bool HasTarget(out Transform target, out float disToTarget)
    {
        target = null;
        disToTarget = float.MaxValue;
        PlayerCharacter selectedCharacter = null;

        if (_targetCandidates == null || _targetCandidates.Length == 0)
        {
            CheckPotentialTargets();
        }

        if (_targetCandidates != null && _targetCandidates.Length > 0)
        {
            foreach (PlayerCharacter pc in _targetCandidates)
            {
                if (pc == null || !TryResolveEligibleTarget(pc.Id))
                {
                    continue;
                }

                float distance = Vector2.Distance(transform.position, pc.transform.position);
                if (distance < disToTarget)
                {
                    disToTarget = distance;
                    target = pc.transform;
                    selectedCharacter = pc;
                }
            }
        }

        if (selectedCharacter != null)
        {
            SetCurrentTarget(selectedCharacter.Id, selectedCharacter.transform);
        }
        else if (TryGetCurrentTarget(out EntityId currentTargetId, out Transform currentTargetTransform))
        {
            if (!TryResolveEligibleTarget(currentTargetId))
            {
                TryInvalidateCurrentTarget(currentTargetId);
                return false;
            }

            target = currentTargetTransform;
            disToTarget = Vector2.Distance(transform.position, target.position);
        }
        else
        {
            if (currentTargetId.Value != 0)
            {
                TryInvalidateCurrentTarget(currentTargetId);
            }
            else
            {
                IsOnPursuit = false;
                IsAttacking = false;
            }
        }

        return target != null;
    }

    private bool TryResolveEligibleTarget(EntityId targetId)
    {
        return targetId.Value != 0 &&
            _entityRegistry != null &&
            _entityRegistry.TryGetCharacter(targetId, out ICharacter character) &&
            character != null &&
            character.IsAlive &&
            _entityRegistry.TryGetDamageable(targetId, out IDamageable damageable) &&
            damageable != null &&
            damageable.CanReceiveDamage;
    }

    private bool HasLOS(Transform target, float disToTarget)
    {
        IsOnPursuit = false;

        if (target == null) return false;

        bool outOfRange = disToTarget > _LOSDistance;
        if (outOfRange) return false;

        bool blocked = Physics2D.Raycast(transform.position, target.position - transform.position, disToTarget, _obstacleLayer);
        if (blocked) return false;

        IsOnPursuit = true;
        return true;
    }

    private bool IsInAttackRange(Transform target)
    {
        IsAttacking = false;
        if (target == null) return false;

        bool isInRange = Vector2.Distance(transform.position, target.position) <= _attackRange;

        if (!isInRange) return false;

        IsAttacking = true;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _moveSpeed = Mathf.Max(0f, _moveSpeed);

        if (_movementMotor == null)
        {
            _movementMotor =
                GetComponent<Kinematic2DMovementMotor>();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _LOSDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }
#endif
}
