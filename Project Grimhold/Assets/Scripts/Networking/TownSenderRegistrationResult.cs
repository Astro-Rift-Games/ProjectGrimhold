/// <summary>Outcome of binding a TownSessionTicket to the PlayerRef that delivered it.</summary>
public enum TownSenderRegistrationResult
{
    Accepted,
    RejectedInvalidTicket,
    RejectedProfileBoundToAnotherSender,
    RejectedSenderBoundToAnotherProfile
}
