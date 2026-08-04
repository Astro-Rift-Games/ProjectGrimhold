/// <summary>
/// Razones por las que una solicitud de curación puede ser rechazada.
/// </summary>
public enum HealFailureReason : byte
{
    None = 0,
    MissingAuthority = 1,
    TargetDead = 2,
    TargetUnavailable = 3,
    InvalidAmount = 4,
    HealthFull = 5
}
