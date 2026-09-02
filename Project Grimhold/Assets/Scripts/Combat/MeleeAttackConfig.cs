using UnityEngine;

/// <summary>
/// Configuración inmutable de datos compartidos para ataques melee.
/// </summary>
[CreateAssetMenu(fileName = "MeleeAttackConfig", menuName = "Grimhold/Combat/MeleeAttackConfig")]
public sealed class MeleeAttackConfig : AttackConfig
{
    [SerializeField, Min(0.1f)]
    private float _radius = 0.5f;

    [SerializeField, Min(1)]
    private int _maximumTargets = 1;

    [SerializeField]
    private LayerMask _targetLayerMask;

    public float Radius => _radius;
    public int MaximumTargets => _maximumTargets;
    public LayerMask TargetLayerMask => _targetLayerMask;

    public override bool TryValidate(out string error)
    {
        if (!TryValidateCommon(out error))
        {
            return false;
        }

        if (_radius <= 0f)
        {
            error = $"{nameof(Radius)} must be greater than zero (current: {_radius}).";
            return false;
        }

        if (_maximumTargets < 1)
        {
            error = $"{nameof(MaximumTargets)} must be at least one (current: {_maximumTargets}).";
            return false;
        }

        if (_targetLayerMask.value == 0)
        {
            error = $"{nameof(TargetLayerMask)} must not be empty.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        if (_radius < 0.1f)
        {
            _radius = 0.1f;
        }

        if (_maximumTargets < 1)
        {
            _maximumTargets = 1;
        }
    }
}
