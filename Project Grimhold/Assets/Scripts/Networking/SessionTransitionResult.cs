public enum SessionTransitionResult
{
    Succeeded = 0,
    Busy,
    InvalidRequest,
    InvalidState,
    ShutdownFailed,
    ConnectionFailed,
    RecoveryFailed,
    LoadoutReservationFailed,
    LoadoutRollbackFailed,
    LoadoutConfirmationFailed
}
