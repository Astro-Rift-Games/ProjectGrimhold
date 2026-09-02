using UnityEngine;
using Fusion;

/// <summary>
/// Configuración inmutable de datos compartidos para ataques ranged (a distancia).
/// </summary>
[CreateAssetMenu(fileName = "RangedAttackConfig", menuName = "Grimhold/Combat/RangedAttackConfig")]
public sealed class RangedAttackConfig : AttackConfig
{
    [SerializeField, Min(0.1f)]
    private float _projectileSpeed = 10f;

    [SerializeField, Min(0.1f)]
    private float _lifetimeSeconds = 5f;

    [SerializeField, Min(0f)]
    private float _projectileSpawnOffset = 0.7f;

    [SerializeField]
    private NetworkPrefabRef _projectilePrefab;

    [SerializeField]
    private LayerMask _impactLayerMask;

    public float ProjectileSpeed => _projectileSpeed;
    public float LifetimeSeconds => _lifetimeSeconds;
    public float ProjectileSpawnOffset => _projectileSpawnOffset;
    public NetworkPrefabRef ProjectilePrefab => _projectilePrefab;
    public LayerMask ImpactLayerMask => _impactLayerMask;

    public override bool TryValidate(out string error)
    {
        if (!TryValidateCommon(out error))
        {
            return false;
        }

        if (_projectileSpeed <= 0f)
        {
            error = $"{nameof(ProjectileSpeed)} must be greater than zero (current: {_projectileSpeed}).";
            return false;
        }

        if (_lifetimeSeconds <= 0f)
        {
            error = $"{nameof(LifetimeSeconds)} must be greater than zero (current: {_lifetimeSeconds}).";
            return false;
        }

        if (_projectileSpawnOffset < 0f)
        {
            error = $"{nameof(ProjectileSpawnOffset)} must be greater than or equal to zero (current: {_projectileSpawnOffset}).";
            return false;
        }

        if (!_projectilePrefab.IsValid)
        {
            error = $"{nameof(ProjectilePrefab)} must refer to a valid network object.";
            return false;
        }

        if (_impactLayerMask.value == 0)
        {
            error = $"{nameof(ImpactLayerMask)} must not be empty.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        if (_projectileSpeed < 0.1f)
        {
            _projectileSpeed = 0.1f;
        }

        if (_lifetimeSeconds < 0.1f)
        {
            _lifetimeSeconds = 0.1f;
        }

        if (_projectileSpawnOffset < 0f)
        {
            _projectileSpawnOffset = 0f;
        }
    }
}
