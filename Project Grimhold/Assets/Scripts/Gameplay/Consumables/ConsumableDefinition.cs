using UnityEngine;

/// <summary>
/// Define los datos y configuraciones base para un consumible.
/// Referencia a un efecto que implementa la lógica específica.
/// </summary>
[CreateAssetMenu(fileName = "ConsumableDefinition", menuName = "Grimhold/Consumables/Consumable Definition")]
public sealed class ConsumableDefinition : ScriptableObject
{
    [SerializeField]
    [Tooltip("El efecto que se aplicará cuando se use el consumible.")]
    private ConsumableEffectBase _effect;

    [SerializeField]
    [Tooltip("Sonido a reproducir de forma local cuando el consumo tiene éxito.")]
    private AudioClip _consumeSound;

    [SerializeField]
    [Tooltip("Prefab de ParticleSystem instanciado en la posición del jugador al consumir con éxito.")]
    private ParticleSystem _consumeParticles;

    [SerializeField, Min(0.01f)]
    private float _consumptionDurationSeconds = 2f;

    public ConsumableEffectBase Effect => _effect;
    public AudioClip ConsumeSound => _consumeSound;
    public ParticleSystem ConsumeParticles => _consumeParticles;
    public float ConsumptionDurationSeconds => _consumptionDurationSeconds;

    /// <summary>
    /// Valida que la definición tenga las dependencias necesarias.
    /// </summary>
    public bool TryValidate(out string error)
    {
        error = null;

        if (float.IsNaN(_consumptionDurationSeconds) || float.IsInfinity(_consumptionDurationSeconds) ||
            _consumptionDurationSeconds <= 0f)
        {
            error = $"ConsumableDefinition '{name}' must have a positive finite consumption duration.";
            return false;
        }

        if (_effect == null)
        {
            error = $"ConsumableDefinition '{name}' does not have an effect assigned.";
            return false;
        }

        return true;
    }
}
