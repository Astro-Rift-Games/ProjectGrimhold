/// <summary>
/// Authoritative lifecycle of one admitted raid participant. The state describes the
/// participant, not the temporary combat avatar that represents it in the raid.
/// </summary>
public enum RaidParticipantState : byte
{
    Raiding = 0,
    Extracted = 1,
    Defeated = 2,
    Aborted = 3
}
