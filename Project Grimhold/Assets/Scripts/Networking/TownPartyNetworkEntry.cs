using Fusion;

public struct TownPartyNetworkEntry : INetworkStruct
{
    public int PartyId;
    public NetworkString<_32> HostProfileId;
    public NetworkString<_32> FirstMemberProfileId;
    public NetworkString<_32> SecondMemberProfileId;
    public int MemberCount;
    public int Revision;

    public bool IsOccupied => PartyId > 0;
}
