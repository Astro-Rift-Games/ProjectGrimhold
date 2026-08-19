using System;
using UnityEngine;

/// <summary>
/// Observa el evento de despawn de un NetworkProjectile y emite partículas
/// si el proyectil impactó contra geometría estática (escenario).
/// </summary>
[DisallowMultipleComponent]
public sealed class ProjectileImpactParticlePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private NetworkProjectile _projectile;

    [Header("Configuration")]
    [SerializeField]
    [Tooltip("Prefab de partículas a instanciar en el punto de impacto. Debe tener Stop Action = Destroy.")]
    private ParticleSystem _impactParticles;

    private void Awake()
    {
        if (_projectile == null)
        {
            _projectile = GetComponent<NetworkProjectile>();
        }
    }

    private void OnEnable()
    {
        if (_projectile != null)
        {
            _projectile.ImpactResolved += OnImpactResolved;
        }
    }

    private void OnDisable()
    {
        if (_projectile != null)
        {
            _projectile.ImpactResolved -= OnImpactResolved;
        }
    }

    private void OnImpactResolved(Vector2 position, bool isGeometryImpact)
    {
        // Solo emitimos partículas de impacto de proyectil si chocó contra el escenario (geometría estática).
        // Si golpeó un enemy/player o un breakable, esos sistemas emitirán sus propias partículas.
        if (isGeometryImpact && _impactParticles != null)
        {
            ParticleEffectPlayer.InstantiateAndPlay(_impactParticles, position, transform.rotation);
        }
    }
}
