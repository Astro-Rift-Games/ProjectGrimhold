using Fusion;

public struct TownPartyInvitationNetworkEntry : INetworkStruct
{
    public int InvitationId;
    public NetworkString<_32> InviterProfileId;
    public NetworkString<_32> RecipientProfileId;
    public NetworkString<_32> HostProfileId;
    public NetworkId PreparationNetworkId;
    public int MembershipRevision;
    public TickTimer ExpiresAt;

    public bool IsPending => InvitationId > 0;
}

public struct TownPartyInvitationCooldownEntry : INetworkStruct
{
    public NetworkString<_32> FirstProfileId;
    public NetworkString<_32> SecondProfileId;
    public TickTimer ExpiresAt;

    public bool IsActive => !string.IsNullOrWhiteSpace(FirstProfileId.ToString());
}
