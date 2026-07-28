using UnityEngine;

public enum TrapState
{
    Ready,
    Telegraphing,
    Active,
    InCooldown,
}

/// <summary>
/// Configuración estática compartida para trampas de escenario.
/// Contiene únicamente datos de configuración (tiempos, daño).
/// El estado en runtime es administrado de forma síncrona en red por <see cref="BaseTrap"/>.
/// </summary>
[CreateAssetMenu(fileName = "NewTrapInfo", menuName = "Grimhold/TrapInfo")]
public class TrapInfo : ScriptableObject
{
    [SerializeField] private float _activationTime = 1f;
    public float activationTime => _activationTime;

    [SerializeField] private float _resetTime = 1f;
    public float resetTime => _resetTime;

    [SerializeField] private float _cooldown = 3f;
    public float cooldown => _cooldown;

    [SerializeField] private float _damage = 10f;
    public float damage => _damage;

    [SerializeField] private DamageType _damageType;
    public DamageType DamageType => _damageType;
}
