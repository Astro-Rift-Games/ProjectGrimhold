using Fusion;

/// <summary>Isolated local RaidParticipantId table plus packed-index buckets.</summary>
public sealed class RaidParticipantIdTableEndpointWeaverCandidate : NetworkBehaviour
{
    [Networked, Capacity(RaidSessionRules.MaxParticipants)]
    private NetworkArray<RaidParticipantId> Participants => default;

    [Networked]
    private RaidLootOriginPackedState Buckets { get; set; }
}
