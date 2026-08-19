using UnityEngine;

/// <summary>
/// Clase base abstracta inmutable para configuraciones de ataque (melee, ranged, etc.).
/// </summary>
public abstract class AttackConfig : ScriptableObject
{
    [SerializeField, Min(0f)]
    private float _damage = 10f;

    [SerializeField]
    private DamageType _damageType = DamageType.Physical;

    [SerializeField, Min(0f)]
    private float _cooldownSeconds = 0.5f;

    [SerializeField]
    private AttackInputMode _inputMode = AttackInputMode.Press;

    [SerializeField, Min(0f)]
    [Tooltip("Knockback force in world units per second applied to the target on impact. Set to 0 to disable knockback.")]
    private float _knockbackForce = 5f;

    public float Damage => _damage;
    public DamageType DamageType => _damageType;
    public float CooldownSeconds => _cooldownSeconds;
    public AttackInputMode InputMode => _inputMode;

    /// <summary>
    /// Knockback force in world units per second applied to the target on a successful hit.
    /// A value of 0 produces no knockback.
    /// </summary>
    public float KnockbackForce => _knockbackForce;

    /// <summary>
    /// Intenta validar si la configuración actual es válida.
    /// </summary>
    /// <param name="error">Mensaje descriptivo del primer error encontrado.</param>
    /// <returns>True si la configuración es totalmente válida, de lo contrario False.</returns>
    public abstract bool TryValidate(out string error);

    /// <summary>
    /// Realiza validaciones comunes para todos los tipos de ataque.
    /// </summary>
    protected bool TryValidateCommon(out string error)
    {
        if (_damage <= 0f)
        {
            error = $"{nameof(Damage)} must be greater than zero (current: {_damage}).";
            return false;
        }

        if (_cooldownSeconds < 0f)
        {
            error = $"{nameof(CooldownSeconds)} must be greater than or equal to zero (current: {_cooldownSeconds}).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    protected virtual void OnValidate()
    {
        if (_damage < 0f)
        {
            _damage = 0f;
        }

        if (_cooldownSeconds < 0f)
        {
            _cooldownSeconds = 0f;
        }

        if (_knockbackForce < 0f)
        {
            _knockbackForce = 0f;
        }
    }
}
