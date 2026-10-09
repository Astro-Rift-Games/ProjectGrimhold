using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Maintains the set of disconnected participants that are retained in the Raid.
/// </summary>
public sealed class DisconnectedParticipantRegistry
{
    private readonly List<NetworkObject> _retainedParticipants = new();

    public IReadOnlyList<NetworkObject> RetainedParticipants => _retainedParticipants;

    public void Clear()
    {
        _retainedParticipants.Clear();
    }

    public void Retain(NetworkObject participantObject)
    {
        if (participantObject != null && !_retainedParticipants.Contains(participantObject))
        {
            _retainedParticipants.Add(participantObject);
        }
    }

    /// <summary>
    /// Checks if there is any retained participant still unresolved (Raiding).
    /// As a side effect, removes from tracking any participant that has been resolved.
    /// </summary>
    public bool HasUnresolvedRetainedParticipant()
    {
        bool unresolved = false;
        for (int index = _retainedParticipants.Count - 1; index >= 0; index--)
        {
            NetworkObject participantObject = _retainedParticipants[index];
            if (participantObject == null ||
                !participantObject.IsValid ||
                !participantObject.TryGetBehaviour(out NetworkRaidParticipant participant) ||
                participant.State != RaidParticipantState.Raiding)
            {
                _retainedParticipants.RemoveAt(index);
                continue;
            }

            unresolved = true;
        }

        return unresolved;
    }
}
