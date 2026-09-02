using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Concrete strategy for melee attacks.
/// Uses IAttackTargetQuery to query targets spatially and IDamageResolver to apply damage.
/// Entity-type agnostic: works for both Player and Enemy entities since it resolves
/// dependencies through the GameObject hierarchy and delegates to interface-based components.
/// </summary>
[DisallowMultipleComponent]
public sealed class MeleeAttack : MonoBehaviour, IAttack
{
    [Header("Configuration")]
    [SerializeField]
    private MeleeAttackConfig _config;

    [SerializeField]
    private AttackExecutionParameters _defaultParameters;

    [Header("Support Components")]
    [SerializeField]
    private MonoBehaviour _targetQuerySource;

    [SerializeField]
    private MonoBehaviour _damageResolverSource;

    private IAttackTargetQuery _targetQuery;
    private IDamageResolver _damageResolver;
    private AttackExecutionParameters _runtimeParameters;
    private bool _isValid;

    private readonly HashSet<EntityId> _tempProcessedIds = new();

    public AttackType Type => AttackType.Melee;
    public float CooldownSeconds => _runtimeParameters.CooldownSeconds;
    public AttackInputMode InputMode => _config != null ? _config.InputMode : AttackInputMode.Press;
    public float EffectiveRange => _runtimeParameters.Range;
    public float DetectionCenterOffset => _config != null
        ? _runtimeParameters.Range - _config.Radius
        : 0f;
    public float Radius => _config != null ? _config.Radius : 0f;

#if UNITY_EDITOR
    public float EditorEffectiveRange => Application.isPlaying
        ? _runtimeParameters.Range
        : _defaultParameters.Range;
    public float EditorDetectionCenterOffset => _config != null
        ? EditorEffectiveRange - _config.Radius
        : 0f;
#endif

    private void Awake()
    {
        _runtimeParameters = _defaultParameters;
        if (_targetQuery == null || _damageResolver == null)
        {
            CacheDependencies();
        }
    }

    private void Start()
    {
        if (!_isValid && _config != null)
        {
            _isValid = ValidateDependencies();
        }
    }

    /// <summary>
    /// Explicitly initializes dependencies for testing or dynamic instantiation.
    /// </summary>
    public void Initialize(
        MeleeAttackConfig config,
        in AttackExecutionParameters parameters,
        IAttackTargetQuery targetQuery,
        IDamageResolver damageResolver)
    {
        _config = config;
        _runtimeParameters = parameters;
        _targetQuery = targetQuery;
        _damageResolver = damageResolver;
        _isValid = ValidateDependencies();
    }

    /// <summary>
    /// Applies an equipped weapon's melee configuration to the existing strategy.
    /// </summary>
    public bool TryConfigure(MeleeAttackConfig config, in AttackExecutionParameters parameters)
    {
        _config = config;
        _runtimeParameters = parameters;
        CacheDependencies();
        _isValid = ValidateDependencies();
        return _isValid;
    }

    private void CacheDependencies()
    {
        if (_targetQuerySource != null)
        {
            _targetQuery = _targetQuerySource as IAttackTargetQuery;
        }

        if (_targetQuery == null)
        {
            _targetQuery = GetComponent<IAttackTargetQuery>() ?? GetComponentInChildren<IAttackTargetQuery>() ?? GetComponentInParent<IAttackTargetQuery>();
            if (_targetQuery is MonoBehaviour queryMb)
            {
                _targetQuerySource = queryMb;
            }
        }

        if (_damageResolverSource != null)
        {
            _damageResolver = _damageResolverSource as IDamageResolver;
        }

        if (_damageResolver == null)
        {
            _damageResolver = GetComponent<IDamageResolver>() ?? GetComponentInChildren<IDamageResolver>() ?? GetComponentInParent<IDamageResolver>();
            if (_damageResolver == null)
            {
                _damageResolver = FindAnyObjectByType<DamageResolver>(FindObjectsInactive.Exclude);
            }
            if (_damageResolver is MonoBehaviour resolverMb)
            {
                _damageResolverSource = resolverMb;
            }
        }
    }

    private bool ValidateDependencies()
    {
        if (_config == null)
        {
            Debug.LogError($"{nameof(MeleeAttack)}: Missing MeleeAttackConfig on GameObject {gameObject.name}.", this);
            return false;
        }

        if (!_config.TryValidate(out string error))
        {
            Debug.LogError($"{nameof(MeleeAttack)}: Invalid configuration on GameObject {gameObject.name}. Error: {error}", this);
            return false;
        }

        if (!_runtimeParameters.TryValidate(out string parameterError))
        {
            Debug.LogError($"{nameof(MeleeAttack)}: Invalid runtime parameters on GameObject {gameObject.name}. Error: {parameterError}", this);
            return false;
        }

        if (_runtimeParameters.Range < _config.Radius)
        {
            Debug.LogError($"{nameof(MeleeAttack)}: Effective range must be at least the detection radius on GameObject {gameObject.name}.", this);
            return false;
        }

        if (_config.MaximumTargets <= 0)
        {
            Debug.LogError($"{nameof(MeleeAttack)}: MaximumTargets must be greater than zero on GameObject {gameObject.name}.", this);
            return false;
        }

        if (_targetQuery == null)
        {
            Debug.LogError($"{nameof(MeleeAttack)}: Target query component does not implement {nameof(IAttackTargetQuery)} on GameObject {gameObject.name}.", this);
            return false;
        }

        if (_damageResolver == null)
        {
            Debug.LogError($"{nameof(MeleeAttack)}: Damage resolver component does not implement {nameof(IDamageResolver)} on GameObject {gameObject.name}.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Executes the melee attack strategy authoritatively on the State Authority.
    /// </summary>
    public AttackResult Execute(in AttackRequest request)
    {
        if (!_isValid)
        {
            _isValid = ValidateDependencies();
            if (!_isValid)
            {
                return AttackResult.Rejected(AttackFailureReason.MissingConfiguration);
            }
        }

        if (_config.MaximumTargets <= 0)
        {
            return AttackResult.Rejected(AttackFailureReason.MissingConfiguration);
        }

        // Validate attack direction
        if (request.Direction.sqrMagnitude < 0.0001f)
        {
            return AttackResult.Rejected(AttackFailureReason.InvalidDirection);
        }

        // Clear the buffer at the start of each execution
        _tempProcessedIds.Clear();

        // 1. Build the target query (with normalized direction)
        AttackTargetQuery targetQuery = new AttackTargetQuery(
            request.AttackerId,
            request.Origin,
            request.Direction.normalized,
            DetectionCenterOffset,
            _config.Radius,
            _config.MaximumTargets,
            _config.TargetLayerMask.value
        );

        // 2. Perform spatial query
        var targets = _targetQuery.FindTargets(in targetQuery);

        // 3. Generate and delegate damage requests for each unique deduplicated target
        int targetsCount = 0;
        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];

                // Exclude the attacker by ID
                if (target.TargetId == request.AttackerId)
                {
                    continue;
                }

                // Deduplicate before applying the limit and before applying damage
                if (!_tempProcessedIds.Add(target.TargetId))
                {
                    continue;
                }

                // Generate damage request
                DamageRequest damageRequest = new DamageRequest(
                    request.AttackerId,
                    target.TargetId,
                    _runtimeParameters.Damage,
                    _runtimeParameters.DamageType,
                    request.Direction,
                    target.HitPoint,
                    request.SimulationTick,
                    _runtimeParameters.KnockbackForce
                );

                // We do not depend on the Resolve result to decide if the attack was executed
                _damageResolver.Resolve(in damageRequest);

                targetsCount++;
                if (targetsCount >= _config.MaximumTargets)
                {
                    break;
                }
            }
        }

        // Clear to avoid holding targets between executions
        _tempProcessedIds.Clear();

        // A melee attack without targets, or whose targets reject the damage, is still considered successfully executed.
        return AttackResult.Executed();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }

    private void OnDrawGizmosSelected()
    {
        if (_config == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        MeleeAttackGizmoDrawer drawer = GetComponentInParent<MeleeAttackGizmoDrawer>();
        if (drawer != null)
        {
            Gizmos.DrawSphere(drawer.AttackOrigin.position, _config.Radius);
        }
    }
#endif
}
