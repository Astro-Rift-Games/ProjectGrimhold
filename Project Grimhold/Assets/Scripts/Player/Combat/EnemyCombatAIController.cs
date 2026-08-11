using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Network component responsible for processing enemy AI combat decisions
/// and delegating attack execution to the active attack strategy.
///
/// Operates with any strategy implementing the <see cref="IAttack"/> contract,
/// driven by AI state authority rather than client player input.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyCombatAIController : NetworkBehaviour, ICombatController
{
    [Header("Dependencies")]
    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private Transform _attackOrigin;

    [SerializeField]
    private MonoBehaviour _activeAttackSource;

    [SerializeField]
    private EnemyMovementAIController _movementController;

    private ICharacter _character;
    private IAttack _activeAttack;
    private EntityRegistry _entityRegistry;
    private bool _dependenciesValid;
    private int _lastObservedSequence;

    // The request is queued during FixedUpdateNetwork and committed only after
    // ExecutePendingDamage revalidates the target on the animation hit frame.
    private AttackRequest _pendingDamageRequest;
    private EntityId _pendingTargetId;
    private bool _hasPendingDamage;

    [Networked]
    private TickTimer AttackCooldown { get; set; }

    [Networked]
    public NetworkBool IsAttackEnabled { get; private set; }

    // Replicated state for presentation layers
    [Networked]
    private int AttackSequence { get; set; }

    [Networked]
    private Vector2 LastAttackOrigin { get; set; }

    [Networked]
    private Vector2 LastAttackDirection { get; set; }

    [Networked]
    private int LastAttackTypeValue { get; set; }

    [Networked]
    private int LastAttackTick { get; set; }

    /// <summary>
    /// Local event raised during Render when a successful attack execution is detected in simulation.
    /// </summary>
    public event Action<AttackPerformedEvent> AttackPerformed;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _entityRegistry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        _dependenciesValid = ValidateDependencies();

        _lastObservedSequence = AttackSequence;

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            IsAttackEnabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!_dependenciesValid || !HasStateAuthority)
        {
            return;
        }

        if (_movementController == null || _character == null)
        {
            return;
        }

        bool attackRequested = _movementController.IsAttacking;

        if (!attackRequested || !IsAttackEnabled || !_character.IsAlive)
        {
            ClearPendingDamage();
            return;
        }

        bool hasTarget = _movementController.TryGetCurrentTarget(out EntityId targetId, out _);

        if (!hasTarget && targetId.Value != 0)
        {
            _movementController.TryInvalidateCurrentTarget(targetId);
            ClearPendingDamage();
            return;
        }

        if (!hasTarget)
        {
            ClearPendingDamage();
            return;
        }

        if (!TryResolveEligibleTarget(targetId))
        {
            _movementController.TryInvalidateCurrentTarget(targetId);
            ClearPendingDamage();
            return;
        }

        TryCommitAttack(_movementController.FacingDirection, targetId);
    }

    public override void Render()
    {
        if (!_dependenciesValid)
        {
            return;
        }

        if (AttackSequence != _lastObservedSequence)
        {
            AttackPerformedEvent performedEvent = new AttackPerformedEvent(
                _character.Id,
                (AttackType)LastAttackTypeValue,
                LastAttackOrigin,
                LastAttackDirection,
                LastAttackTick
            );

            AttackPerformed?.Invoke(performedEvent);
            _lastObservedSequence = AttackSequence;
        }
    }

    /// <summary>
    /// Validates cooldown and stores a pending request for the animation hit frame.
    /// Cooldown and replicated execution state are committed only after final target validation.
    /// </summary>
    /// <param name="aimDirection">The direction to execute the attack towards.</param>
    private void TryCommitAttack(Vector2 aimDirection, EntityId targetId)
    {
        if (_hasPendingDamage || !AttackCooldown.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            aimDirection = Vector2.down;
        }

        Vector2 normalizedDirection = aimDirection.normalized;
        Vector2 originPos = _attackOrigin != null ? (Vector2)_attackOrigin.position : (Vector2)transform.position;

        // Store the request so ExecutePendingDamage can resolve it on the animation hit frame.
        _pendingDamageRequest = new AttackRequest(
            _character.Id,
            originPos,
            normalizedDirection,
            (int)Runner.Tick
        );
        _pendingTargetId = targetId;
        _hasPendingDamage = true;
        {
            ExecutePendingDamage();
        }
    }

    /// <summary>
    /// Applies the stored pending damage request.
    /// Must be called from a trusted source (Animation Event via EnemyAttackAnimationListener)
    /// only on the State Authority peer.
    ///
    /// Safe to call multiple times per attack window: the pending flag is cleared on first execution.
    /// </summary>
    public void ExecutePendingDamage()
    {
        if (!HasStateAuthority || !_hasPendingDamage)
        {
            return;
        }

        if (!_character.IsAlive)
        {
            ClearPendingDamage();
            return;
        }

        EntityId currentTargetId = default;
        bool hasCurrentTarget = _movementController != null &&
            _movementController.TryGetCurrentTarget(out currentTargetId, out _);

        if (!hasCurrentTarget)
        {
            if (currentTargetId.Value != 0)
            {
                _movementController.TryInvalidateCurrentTarget(currentTargetId);
            }

            ClearPendingDamage();
            return;
        }

        if (currentTargetId != _pendingTargetId)
        {
            ClearPendingDamage();
            return;
        }

        if (!TryResolveEligibleTarget(_pendingTargetId))
        {
            _movementController.TryInvalidateCurrentTarget(_pendingTargetId);
            ClearPendingDamage();
            return;
        }

        AttackRequest request = _pendingDamageRequest;
        ClearPendingDamage();
        CommitAttack(in request);
        _activeAttack.Execute(in request);
    }

    /// <summary>
    /// Authoritatively changes the combat enabled state.
    /// </summary>
    public bool TrySetAttackEnabled(bool enabled)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        IsAttackEnabled = enabled;
        return true;
    }

    /// <summary>
    /// Authoritatively updates the active attack strategy. Requires State Authority.
    /// </summary>
    public bool TrySetActiveAttack(MonoBehaviour attackSource)
    {
        if (!HasStateAuthority)
        {
            return false;
        }

        if (attackSource == null)
        {
            Debug.LogError($"{nameof(EnemyCombatAIController)}: Cannot set active attack to null.", this);
            return false;
        }

        if (attackSource is IAttack newAttack)
        {
            _activeAttackSource = attackSource;
            _activeAttack = newAttack;
            return true;
        }

        Debug.LogError($"{nameof(EnemyCombatAIController)}: Component {attackSource.name} does not implement {nameof(IAttack)}.", this);
        return false;
    }

    private void CacheDependencies()
    {
        if (_characterSource != null)
        {
            _character = _characterSource as ICharacter;
        }
        
        if (_character == null)
        {
            _character = GetComponent<ICharacter>() ?? GetComponentInParent<ICharacter>();
        }

        if (_activeAttackSource != null)
        {
            _activeAttack = _activeAttackSource as IAttack;
        }

        if (_activeAttack == null)
        {
            _activeAttack = GetComponent<IAttack>() ?? GetComponentInChildren<IAttack>();
            if (_activeAttack is MonoBehaviour attackMb)
            {
                _activeAttackSource = attackMb;
            }
        }

        if (_attackOrigin == null)
        {
            _attackOrigin = transform;
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<EnemyMovementAIController>();
        }
    }

    private bool ValidateDependencies()
    {
        if (_character == null)
        {
            Debug.LogError($"{nameof(EnemyCombatAIController)} requires a component implementing {nameof(ICharacter)}.", this);
            return false;
        }

        if (_attackOrigin == null)
        {
            Debug.LogError($"{nameof(EnemyCombatAIController)} requires an assigned {nameof(_attackOrigin)} Transform.", this);
            return false;
        }

        if (_activeAttack == null)
        {
            Debug.LogError($"{nameof(EnemyCombatAIController)} requires a component implementing {nameof(IAttack)}.", this);
            return false;
        }

        if (_movementController == null)
        {
            Debug.LogError($"{nameof(EnemyCombatAIController)} requires an assigned {nameof(EnemyMovementAIController)}.", this);
            return false;
        }

        if (_entityRegistry == null)
        {
            Debug.LogError($"{nameof(EnemyCombatAIController)} requires a runner-scoped {nameof(EntityRegistry)}.", this);
            return false;
        }

        return true;
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

    private void ClearPendingDamage()
    {
        _hasPendingDamage = false;
        _pendingTargetId = default;
    }

    private void CommitAttack(in AttackRequest request)
    {
        float cooldownSeconds = _activeAttack.CooldownSeconds;
        AttackCooldown = cooldownSeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, cooldownSeconds)
            : TickTimer.None;

        LastAttackOrigin = request.Origin;
        LastAttackDirection = request.Direction;
        LastAttackTypeValue = (int)_activeAttack.Type;
        LastAttackTick = request.SimulationTick;
        AttackSequence++;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_characterSource == null)
        {
            _characterSource = GetComponent<MonoBehaviour>() as ICharacter != null ? GetComponent<MonoBehaviour>() : null;
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<EnemyMovementAIController>();
        }

        if (_activeAttackSource == null)
        {
            IAttack foundAttack = GetComponent<IAttack>() ?? GetComponentInChildren<IAttack>();
            if (foundAttack is MonoBehaviour attackMb)
            {
                _activeAttackSource = attackMb;
            }
        }

        CacheDependencies();
    }
#endif
}
