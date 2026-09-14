using Fusion;

public readonly struct TownPartyInvitationSnapshot
{
    public int InvitationId { get; }
    public ProfileId InviterProfileId { get; }
    public ProfileId RecipientProfileId { get; }
    public int InviterPartyId { get; }
    public int InviterPartyRevision { get; }
    public int RecipientPartyId { get; }
    public int RecipientPartyRevision { get; }
    public TickTimer ExpiresAt { get; }

    public TownPartyInvitationSnapshot(in TownPartyInvitationNetworkEntry entry)
    {
        InvitationId = entry.InvitationId;
        InviterProfileId = new ProfileId(entry.InviterProfileId.ToString());
        RecipientProfileId = new ProfileId(entry.RecipientProfileId.ToString());
        InviterPartyId = entry.InviterPartyId;
        InviterPartyRevision = entry.InviterPartyRevision;
        RecipientPartyId = entry.RecipientPartyId;
        RecipientPartyRevision = entry.RecipientPartyRevision;
        ExpiresAt = entry.ExpiresAt;
    }
}

public readonly struct TownPartyInvitationResultEvent
{
    public TownPartyInvitationResult Result { get; }

    public TownPartyInvitationResultEvent(TownPartyInvitationResult result)
    {
        Result = result;
    }
}
