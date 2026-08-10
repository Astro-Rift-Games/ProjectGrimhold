using Fusion;
using UnityEngine;

/// <summary>
/// Links a temporary raid avatar to its authoritative <see cref="NetworkRaidParticipant"/>.
/// The link is replicated so host migration and presentation can resolve the participant
/// without treating the avatar as the Fusion PlayerObject.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidAvatarParticipantLink : NetworkBehaviour
{
    [Networked]
    public NetworkId ParticipantId { get; private set; }

    internal void Initialize(NetworkObject participantObject)
    {
        ParticipantId = participantObject != null ? participantObject.Id : default;
    }

    /// <summary>
    /// Applies the dynamic-object remap produced by Host Migration restoration.
    /// </summary>
    internal void SetRestoredParticipant(NetworkId participantId)
    {
        if (HasStateAuthority)
        {
            ParticipantId = participantId;
        }
    }

    public bool TryResolveParticipant(out NetworkRaidParticipant participant)
    {
        participant = null;
        return Runner != null && ParticipantId.IsValid &&
            Runner.TryFindObject(ParticipantId, out NetworkObject participantObject) &&
            participantObject != null && participantObject.TryGetBehaviour(out participant);
    }

    internal void NotifyCorpseConversionCompleted()
    {
        if (!HasStateAuthority || !TryResolveParticipant(out NetworkRaidParticipant participant))
        {
            return;
        }

        if (participant.TryMarkDefeated(Object) && Object != null && !Object.InputAuthority.IsNone)
        {
            Object.AssignInputAuthority(PlayerRef.None);
        }
    }

    internal void NotifyExtractionCompleted()
    {
        if (HasStateAuthority && TryResolveParticipant(out NetworkRaidParticipant participant))
        {
            participant.TryMarkExtracted(Object);
        }
    }
}
