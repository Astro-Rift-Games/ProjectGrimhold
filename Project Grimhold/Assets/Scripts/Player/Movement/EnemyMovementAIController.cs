using Fusion;
using Unity.VisualScripting;
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

    private Transform[] _targets;
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

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
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
        if (!_dependenciesValid)
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
        PlayerCharacter[] playerCharacters = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude);
        if (playerCharacters == null || playerCharacters.Length == 0)
        {
            return;
        }

        _targets = new Transform[playerCharacters.Length];
        for (int i = 0; i < playerCharacters.Length; i++)
        {
            _targets[i] = playerCharacters[i].transform;
        }
    }

    private bool ValidateDependencies()
    {
        if (_movementMotor != null)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(EnemyMovementAIController)} requires " +
            $"{nameof(Kinematic2DMovementMotor)}.",
            this);

        return false;
    }

    private bool HasTarget(out Transform target, out float disToTarget)
    {
        target = null;
        disToTarget = float.MaxValue;
        if (_targets == null || _targets.Length == 0)
        {
            IsOnPursuit = false;
            return false;
        }
        else
        {
            foreach (var t in _targets)
            {
                if (t == null) continue;
                float distance = Vector2.Distance(transform.position, t.position);
                if (distance < disToTarget)
                {
                    disToTarget = distance;
                    target = t;
                }
            }
        }
        return target != null;
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