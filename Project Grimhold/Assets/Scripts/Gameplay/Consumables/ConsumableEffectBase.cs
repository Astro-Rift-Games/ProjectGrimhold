using UnityEngine;

/// <summary>
/// Base abstracta para todos los efectos de consumibles que permite 
/// serialización nativa en Unity como ScriptableObject.
/// </summary>
public abstract class ConsumableEffectBase : ScriptableObject, IConsumableEffect
{
    /// <summary>
    /// Intenta aplicar el efecto al objetivo especificado.
    /// </summary>
    /// <param name="target">El personaje que consume el objeto.</param>
    /// <param name="failureReason">Mensaje descriptivo en caso de que la validación o aplicación falle.</param>
    /// <returns><see langword="true"/> si el efecto se aplicó exitosamente; de lo contrario, <see langword="false"/>.</returns>
    public abstract bool TryApplyEffect(ICharacter target, out string failureReason);
}
