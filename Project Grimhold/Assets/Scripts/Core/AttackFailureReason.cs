/// <summary>
/// Define las razones por las cuales un ataque no pudo ejecutarse.
/// </summary>
public enum AttackFailureReason
{
    /// <summary>
    /// Ningún fallo, el ataque se ejecutó con éxito.
    /// </summary>
    None,

    /// <summary>
    /// El personaje tiene el control de combate deshabilitado.
    /// </summary>
    ControlDisabled,

    /// <summary>
    /// El cooldown del ataque está activo en la simulación.
    /// </summary>
    CooldownActive,

    /// <summary>
    /// La dirección de apuntado es inválida (magnitud prácticamente cero).
    /// </summary>
    InvalidDirection,

    /// <summary>
    /// Falta la configuración requerida para realizar el ataque.
    /// </summary>
    MissingConfiguration,

    /// <summary>
    /// El par que intentó ejecutar el ataque no tiene State Authority.
    /// </summary>
    MissingAuthority,

    /// <summary>
    /// Falló la ejecución propia de la estrategia de ataque.
    /// </summary>
    ExecutionFailed,

    /// <summary>A basic attack cannot start while an ability is executing.</summary>
    AbilityInProgress
}
