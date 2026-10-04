/// <summary>Why an assisted recovery session was interrupted (one value per GD 13 matrix row).</summary>
public enum DownedRecoveryInterruptReason : byte
{
    None = 0,
    InteractReleased,
    ReviverMoved,
    DownedMoved,
    OutOfRange,
    ReviverDamaged,
    DownedDamaged,
    IncompatibleReviverAction,
    ReviverDowned,
    ReviverDisconnected,
    DownedDefeated
}
