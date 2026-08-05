/// <summary>
/// Razones por las que el uso de un consumible puede ser rechazado.
/// </summary>
public enum ConsumableFailureReason : byte
{
    None = 0,
    MissingAuthority = 1,
    InvalidLoot = 2,
    InsufficientAmount = 3,
    TargetDead = 4,
    TargetUnavailable = 5,
    HealthFull = 6,
    EffectFailed = 7
}
