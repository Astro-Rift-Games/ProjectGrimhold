/// <summary>
/// Decides whether a Quick Slot may unassign itself because its last unit left the Raid
/// inventory. Releasing is allowed only while the participant is actively raiding; every way of
/// emptying the inventory that ends or suspends the expedition (extraction, defeat, voluntary
/// abandon, definitive disconnect, Downed) must keep the player's bindings.
/// </summary>
public static class QuickSlotReleaseGate
{
    public static bool CanRelease(
        bool isAlive,
        bool isDowned,
        bool isExtractionIdle,
        bool isMatchInProgress,
        RaidParticipantState participantState,
        ExpeditionProgressionFinalizationCause finalizationCause) =>
        isAlive &&
        !isDowned &&
        isExtractionIdle &&
        isMatchInProgress &&
        participantState == RaidParticipantState.Raiding &&
        finalizationCause == ExpeditionProgressionFinalizationCause.None;
}
