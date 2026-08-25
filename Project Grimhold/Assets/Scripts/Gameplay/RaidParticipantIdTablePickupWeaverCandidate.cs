using Fusion;

/// <summary>Isolated local RaidParticipantId table plus all single-stack origin quantities.</summary>
public sealed class RaidParticipantIdTablePickupWeaverCandidate : NetworkBehaviour
{
    [Networked, Capacity(RaidSessionRules.MaxParticipants)]
    private NetworkArray<RaidParticipantId> Participants => default;

    [Networked]
    private RaidLootPickupCompactOriginState Amounts { get; set; }
}
