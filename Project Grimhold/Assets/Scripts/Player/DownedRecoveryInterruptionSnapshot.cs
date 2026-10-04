/// <summary>
/// Immutable inputs for one interruption evaluation of an assisted recovery session.
/// The Downed player's own disconnect is intentionally not an input: it never interrupts.
/// </summary>
internal readonly struct DownedRecoveryInterruptionSnapshot
{
    internal DownedRecoveryInterruptionSnapshot(
        bool reviverInteractHeld,
        bool reviverMoving,
        bool downedMoving,
        float distance,
        float range,
        float previousReviverHealth,
        float currentReviverHealth,
        bool reviverDowned,
        bool reviverConnected,
        bool downedDamaged,
        bool incompatibleReviverAction,
        bool downedIsDowned,
        int sessionCycle,
        int downedCycle)
    {
        ReviverInteractHeld = reviverInteractHeld;
        ReviverMoving = reviverMoving;
        DownedMoving = downedMoving;
        Distance = distance;
        Range = range;
        PreviousReviverHealth = previousReviverHealth;
        CurrentReviverHealth = currentReviverHealth;
        ReviverDowned = reviverDowned;
        ReviverConnected = reviverConnected;
        DownedDamaged = downedDamaged;
        IncompatibleReviverAction = incompatibleReviverAction;
        DownedIsDowned = downedIsDowned;
        SessionCycle = sessionCycle;
        DownedCycle = downedCycle;
    }

    internal bool ReviverInteractHeld { get; }
    internal bool ReviverMoving { get; }
    internal bool DownedMoving { get; }
    internal float Distance { get; }
    internal float Range { get; }
    internal float PreviousReviverHealth { get; }
    internal float CurrentReviverHealth { get; }
    internal bool ReviverDowned { get; }
    internal bool ReviverConnected { get; }
    internal bool DownedDamaged { get; }
    internal bool IncompatibleReviverAction { get; }
    internal bool DownedIsDowned { get; }
    internal int SessionCycle { get; }
    internal int DownedCycle { get; }
}
