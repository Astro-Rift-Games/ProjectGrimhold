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

    public ConsumableEffectBase Effect => _effect;
    public AudioClip ConsumeSound => _consumeSound;
    public ParticleSystem ConsumeParticles => _consumeParticles;

    /// <summary>
    /// Valida que la definición tenga las dependencias necesarias.
    /// </summary>
    public bool TryValidate(out string error)
    {
        error = null;

        if (_effect == null)
        {
            error = $"ConsumableDefinition '{name}' does not have an effect assigned.";
            return false;
        }

        return true;
    }
}
