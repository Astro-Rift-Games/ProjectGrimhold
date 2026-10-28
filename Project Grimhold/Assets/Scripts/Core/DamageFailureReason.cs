/// <summary>
/// Razones por las cuales una solicitud de daño puede ser rechazada o fallar.
/// </summary>
public enum DamageFailureReason
{
    None,
    InvalidAmount,
    InvalidTarget,
    TargetUnavailable,
    TargetDead,
    SelfDamageRejected,
    MissingAuthority,
    RejectedByTarget,
    FriendlyFireBlocked
}
