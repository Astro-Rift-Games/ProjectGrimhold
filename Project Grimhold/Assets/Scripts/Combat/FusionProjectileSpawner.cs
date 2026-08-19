using Fusion;
using UnityEngine;

/// <summary>
/// Unity/Fusion network adapter to spawn projectiles authoritatively.
/// Implements <see cref="IProjectileSpawner"/> as a <see cref="NetworkBehaviour"/>.
/// Entity-type agnostic: works for both Player and Enemy entities since it resolves
/// the IDamageResolver dependency through the GameObject hierarchy and spawns
/// projectiles that can damage any IDamageable target.
/// </summary>
[DisallowMultipleComponent]
public sealed class FusionProjectileSpawner : NetworkBehaviour, IProjectileSpawner
{
    [Header("Configuration")]
    [SerializeField]
    private RangedAttackConfig _config;

    [Header("Support Components")]
    [SerializeField]
    private MonoBehaviour _damageResolverSource;

    private IDamageResolver _damageResolver;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
    }

    private void CacheDependencies()
    {
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
            if (_damageResolver is MonoBehaviour spawnerMb)
            {
                _damageResolverSource = spawnerMb;
            }
        }
    }

    /// <summary>
    /// Applies the projectile settings owned by the currently equipped ranged weapon.
    /// </summary>
    public bool TryConfigure(RangedAttackConfig config)
    {
        _config = config;
        CacheDependencies();
        return _config != null && _config.TryValidate(out _) && _damageResolver != null;
    }

    /// <summary>
    /// Attempts to spawn a projectile on the network authoritatively.
    /// Validates network authority, configuration, and dependencies before spawning.
    /// </summary>
    public ProjectileSpawnResult Spawn(in ProjectileSpawnRequest request)
    {
        // 1. Validate network authority
        if (!HasStateAuthority)
        {
            Debug.LogWarning($"[CombatTrace] Projectile spawn rejected: Lack of State Authority on {gameObject.name}.", this);
            return new ProjectileSpawnResult(false);
        }

        // 2. Validate configuration and dependencies
        if (_config == null || !_config.ProjectilePrefab.IsValid)
        {
            Debug.LogError($"[CombatTrace] Projectile spawn rejected: Missing or invalid RangedAttackConfig/ProjectilePrefab.", this);
            return new ProjectileSpawnResult(false);
        }

        if (_damageResolver == null)
        {
            Debug.LogError($"[CombatTrace] Projectile spawn rejected: Missing IDamageResolver dependency.", this);
            return new ProjectileSpawnResult(false);
        }

        // 3. Validate request parameters
        if (request.Direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning($"[CombatTrace] Projectile spawn rejected: Invalid direction.", this);
            return new ProjectileSpawnResult(false);
        }

        bool projectileInitialized = false;
        ProjectileSpawnRequest localRequest = request;

        Debug.Log($"[CombatTrace] Projectile spawn requested. OwnerId: {request.OwnerId}, Origin: {request.Origin}, Direction: {request.Direction}", this);

        Quaternion spawnRotation = Quaternion.identity;
        if (request.Direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(request.Direction.y, request.Direction.x) * Mathf.Rad2Deg;
            spawnRotation = Quaternion.Euler(0, 0, angle);
        }

        // 4. Invoke authoritative network spawn with inline pre-initialization
        NetworkSpawnStatus spawnStatus = Runner.TrySpawn(
            _config.ProjectilePrefab,
            out NetworkObject spawnedObject,
            request.Origin,
            rotation: spawnRotation,
            inputAuthority: null,
            onBeforeSpawned: (_, networkObject) =>
            {
                if (networkObject.TryGetComponent(out NetworkProjectile projectile))
                {
                    projectile.InitializeNetworkState(in localRequest, _config.ImpactLayerMask.value);
                    projectile.SetRuntimeDependencies(_damageResolver);
                    projectileInitialized = true;
                    Debug.Log($"[CombatTrace] Projectile inlined onBeforeSpawned initialized.", networkObject);
                }
                else
                {
                    Debug.LogError($"{nameof(FusionProjectileSpawner)}: Spawned object does not contain a {nameof(NetworkProjectile)} component.", networkObject);
                }
            }
        );

        Debug.Log($"[CombatTrace] TrySpawn status: {spawnStatus}, spawnedObject: {(spawnedObject != null ? spawnedObject.name : "null")}, initialized: {projectileInitialized}", this);

        // 5. Handle initialization or spawn failures
        if (!projectileInitialized)
        {
            if (spawnedObject != null)
            {
                Runner.Despawn(spawnedObject);
            }
            return new ProjectileSpawnResult(false);
        }

        bool spawnSucceeded = spawnStatus == NetworkSpawnStatus.Spawned;
        return new ProjectileSpawnResult(spawnSucceeded);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
