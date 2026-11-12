/// <summary>
/// Transient cause behind a <see cref="RaidLaunchPreparationResult.NotReady"/> local launch
/// preparation, so a launch that expires while waiting can report why it never acknowledged.
/// </summary>
public enum RaidLaunchNotReadyReason
{
    /// <summary>The last preparation was not transient-blocked.</summary>
    None,

    /// <summary>The coordinator is inactive, quitting, not in Town, or received no launch context.</summary>
    CoordinatorUnavailable,

    /// <summary>Another session operation is still active.</summary>
    OperationActive,

    /// <summary>The previous Raid participation outcome has not completed yet.</summary>
    ParticipationOutcomePending,

    /// <summary>The local profile has no Town party continuation that contains it.</summary>
    PartyContinuationMissing,

    /// <summary>The frozen launch context does not name the local profile as a participant.</summary>
    LaunchProfileMismatch,

    /// <summary>A launch ticket exists for another Raid code, launch revision or profile.</summary>
    TicketMismatch,

    /// <summary>The loadout reservation save for this launch has not completed yet.</summary>
    ReservationSavePending
}
