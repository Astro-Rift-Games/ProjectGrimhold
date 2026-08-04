/// <summary>
/// Contrato para entidades que pueden recibir curación (jugadores, NPCs aliados, etc.).
/// </summary>
public interface IHealable
{
    /// <summary>
    /// Aplica curación a la entidad respetando la autoridad de red.
    /// </summary>
    /// <param name="request">La solicitud de curación detallada.</param>
    /// <returns>El resultado de la curación procesada.</returns>
    HealResult ApplyHealing(in HealRequest request);
}
