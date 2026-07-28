using Fusion;
using UnityEngine;

/// <summary>
/// Trampa de dardos que utiliza FusionProjectileSpawner y RangedAttackConfig
/// para disparar proyectiles de red de forma autoritativa.
///
/// Utiliza exactamente el mismo sistema de proyectiles que RangeEnemy y RangePlayer
/// (<see cref="NetworkProjectile"/>), desplazando el volumen físico del Collider2D
/// e infligiendo daño mediante <see cref="IDamageResolver"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class DartTrap : BaseTrap
{
    [Header("Configuración de Dirección y Disparo")]
    [SerializeField] private Vector2 _direction = Vector2.right;
    [SerializeField] private int _dartsAmount = 3;
    [SerializeField] private float _dartInterval = 0.5f;
    [SerializeField] private Transform _refPoint;

    [Header("Configuración de Ataque y Spawner")]
    [SerializeField] private RangedAttackConfig _attackConfig;
    [SerializeField] private FusionProjectileSpawner _projectileSpawner;

    [Networked] private int DartsShot { get; set; }
    [Networked] private TickTimer DartIntervalTimer { get; set; }

    public override void Spawned()
    {
        base.Spawned();

        // Buscar automáticamente el spawner en la jerarquía si no se asignó en el Inspector
        if (_projectileSpawner == null)
        {
            _projectileSpawner = GetComponent<FusionProjectileSpawner>() ?? GetComponentInChildren<FusionProjectileSpawner>();
        }
    }

    protected override void OnEnterActive()
    {
        DartsShot = 0;
        DartIntervalTimer = default;
    }

    protected override void UpdateActive()
    {
        if (DartsShot >= _dartsAmount) return;
        if (!DartIntervalTimer.ExpiredOrNotRunning(Runner)) return;

        ShootDart();
        DartsShot++;

        if (DartsShot < _dartsAmount)
        {
            DartIntervalTimer = TickTimer.CreateFromSeconds(Runner, _dartInterval);
        }
    }

    /// <summary>
    /// Emite la solicitud de spawn del proyectil al FusionProjectileSpawner de forma autoritativa.
    /// Utiliza la dirección Vector2 especificada en el Inspector.
    /// </summary>
    private void ShootDart()
    {
        if (_projectileSpawner == null)
        {
            Debug.LogWarning($"{nameof(DartTrap)}: Falta la referencia a FusionProjectileSpawner en {gameObject.name}.", this);
            return;
        }

        if (_attackConfig == null)
        {
            Debug.LogWarning($"{nameof(DartTrap)}: Falta la configuración RangedAttackConfig en {gameObject.name}.", this);
            return;
        }

        Transform originTransform = _refPoint != null ? _refPoint : transform;
        Vector2 dir = _direction.sqrMagnitude > 0f ? _direction.normalized : Vector2.right;

        Vector2 spawnOrigin = (Vector2)originTransform.position + dir * _attackConfig.ProjectileSpawnOffset;

        // EntityId(0) identifica a la trampa/escenario como el origen de daño ambiental
        ProjectileSpawnRequest request = new ProjectileSpawnRequest(
            new EntityId(0),
            spawnOrigin,
            dir,
            _attackConfig.Damage,
            _attackConfig.DamageType,
            _attackConfig.ProjectileSpeed,
            _attackConfig.LifetimeSeconds,
            _attackConfig.MaxRange,
            Runner.Tick
        );

        _projectileSpawner.Spawn(in request);
    }
}
