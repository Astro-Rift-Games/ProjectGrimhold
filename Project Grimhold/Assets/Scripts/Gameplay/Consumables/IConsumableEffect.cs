using UnityEngine;

/// <summary>
/// Contrato para cualquier efecto de un consumible.
/// Permite validar e intentar aplicar el efecto sobre un objetivo.
/// </summary>
public interface IConsumableEffect
{
    /// <summary>
    /// Intenta aplicar el efecto al objetivo especificado.
    /// </summary>
    /// <param name="target">El personaje que consume el objeto.</param>
    /// <param name="failureReason">Mensaje descriptivo en caso de que la validación o aplicación falle.</param>
    /// <returns><see langword="true"/> si el efecto se aplicó exitosamente; de lo contrario, <see langword="false"/>.</returns>
    bool TryApplyEffect(ICharacter target, out string failureReason);
}
