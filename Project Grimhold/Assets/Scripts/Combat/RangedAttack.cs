using UnityEngine;

/// <summary>
/// Concrete strategy for ranged attacks.
/// Translates an <see cref="AttackRequest"/> into a <see cref="ProjectileSpawnRequest"/>
/// and delegates creation to the projectile spawner.
/// Entity-type agnostic: works for both Player and Enemy entities since it resolves
/// dependencies through the GameObject hierarchy and delegates to interface-based components.
/// </summary>
[DisallowMultipleComponent]
public sealed class RangedAttack : MonoBehaviour, IAttack
{
    [Header("Configuration")]
    [SerializeField]
    private RangedAttackConfig _config;

    [Header("Support Components")]
    [SerializeField]
    private MonoBehaviour _projectileSpawnerSource;

    private IProjectileSpawner _projectileSpawner;
    private float _runtimeDamage;
    private bool _isValid;

    public AttackType Type => AttackType.Ranged;
    public float CooldownSeconds => _config != null ? _config.CooldownSeconds : 0f;
    public AttackInputMode InputMode => _config != null ? _config.InputMode : AttackInputMode.Press;

    private void Awake()
    {
        _runtimeDamage = _config != null ? _config.Damage : 0f;
        CacheDependencies();
    }

    private void CacheDependencies()
    {
        if (_projectileSpawnerSource != null)
        {
            _projectileSpawner = _projectileSpawnerSource as IProjectileSpawner;
        }

        if (_projectileSpawner == null)
        {
            _projectileSpawner = GetComponent<IProjectileSpawner>() ?? GetComponentInChildren<IProjectileSpawner>() ?? GetComponentInParent<IProjectileSpawner>();
            if (_projectileSpawner == null)
            {
                _projectileSpawner = FindAnyObjectByType<FusionProjectileSpawner>(FindObjectsInactive.Exclude);
            }
            if (_projectileSpawner is MonoBehaviour spawnerMb)
            {
                _projectileSpawnerSource = spawnerMb;
            }
        }
    }

    private bool TryInitialize()
    {
        CacheDependencies();
        _isValid = ValidateDependencies();
        return _isValid;
    }

    /// <summary>
    /// Applies an equipped weapon's ranged configuration to the existing strategy.
    /// </summary>
    public bool TryConfigure(RangedAttackConfig config) =>
        TryConfigure(config, config != null ? config.Damage : 0f);

    /// <summary>Applies an equipped player weapon's configuration and resolved runtime damage.</summary>
    public bool TryConfigure(RangedAttackConfig config, float effectiveDamage)
    {
        _config = config;
        _runtimeDamage = effectiveDamage;
        CacheDependencies();
        _isValid = ValidateDependencies();
        return _isValid;
    }

    private bool ValidateDependencies()
    {
        if (_config == null)
        {
            Debug.LogError($"{nameof(RangedAttack)}: Missing RangedAttackConfig on GameObject {gameObject.name}.", this);
            return false;
        }

        if (!_config.TryValidate(out string error))
        {
            Debug.LogError($"{nameof(RangedAttack)}: Invalid configuration on GameObject {gameObject.name}. Error: {error}", this);
            return false;
        }

        if (_runtimeDamage <= 0f || float.IsNaN(_runtimeDamage) || float.IsInfinity(_runtimeDamage))
        {
            Debug.LogError($"{nameof(RangedAttack)}: Runtime damage must be finite and greater than zero on GameObject {gameObject.name}.", this);
            return false;
        }

        if (_projectileSpawner == null)
        {
            Debug.LogError($"{nameof(RangedAttack)}: Projectile spawner component does not implement {nameof(IProjectileSpawner)} on GameObject {gameObject.name}.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Executes the ranged attack strategy authoritatively on the State Authority.
    /// Builds the projectile spawn request and delegates it to the spawner.
    /// </summary>
    public AttackResult Execute(in AttackRequest request)
    {
        if (!_isValid && !TryInitialize())
        {
            return AttackResult.Rejected(
                AttackFailureReason.MissingConfiguration);
        }

        // Validate attack direction
        if (request.Direction.sqrMagnitude < 0.0001f)
        {
            return AttackResult.Rejected(
                AttackFailureReason.InvalidDirection);
        }

        Vector2 normalizedDirection =
            request.Direction.normalized;

        Vector2 projectileOrigin =
            request.Origin +
            normalizedDirection *
            _config.ProjectileSpawnOffset;

        ProjectileSpawnRequest spawnRequest =
            new ProjectileSpawnRequest(
                request.AttackerId,
                projectileOrigin,
                normalizedDirection,
                _runtimeDamage,
                _config.DamageType,
                _config.ProjectileSpeed,
                _config.LifetimeSeconds,
                _config.MaxRange,
                request.SimulationTick,
                _config.KnockbackForce);

        ProjectileSpawnResult spawnResult =
            _projectileSpawner.Spawn(in spawnRequest);

        return spawnResult.WasSpawned
            ? AttackResult.Executed()
            : AttackResult.Rejected(
                AttackFailureReason.ExecutionFailed);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
        _isValid = false;
    }
#endif
}
