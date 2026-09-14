using Fusion;

public readonly struct TownPartyInvitationSnapshot
{
    public int InvitationId { get; }
    public ProfileId InviterProfileId { get; }
    public ProfileId RecipientProfileId { get; }
    public NetworkId PreparationNetworkId { get; }
    public int MembershipRevision { get; }
    public TickTimer ExpiresAt { get; }

    public TownPartyInvitationSnapshot(in TownPartyInvitationNetworkEntry entry)
    {
        InvitationId = entry.InvitationId;
        InviterProfileId = new ProfileId(entry.InviterProfileId.ToString());
        RecipientProfileId = new ProfileId(entry.RecipientProfileId.ToString());
        PreparationNetworkId = entry.PreparationNetworkId;
        MembershipRevision = entry.MembershipRevision;
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
