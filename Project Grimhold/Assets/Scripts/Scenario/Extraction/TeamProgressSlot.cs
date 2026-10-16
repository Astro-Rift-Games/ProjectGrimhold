using System;
using Fusion;

/// <summary>
/// Replicated network struct representing a single team's extraction progress state.
/// Quota is derived at runtime from <see cref="InitialTeamSize"/> and <see cref="ExtractionConfig"/>,
/// never replicated directly.
/// </summary>
[System.Serializable]
public struct TeamProgressSlot : INetworkStruct, IEquatable<TeamProgressSlot>
{
    public int TeamIdRaw;
    public int CurrentProgress;
    public NetworkBool AssignmentRequested;
    public int InitialTeamSize;

    public bool IsEmpty => TeamIdRaw == 0;
    public bool IsAssigned => TeamIdRaw != 0;
    public RaidTeamId TeamId => RaidTeamId.TryCreate(TeamIdRaw, out RaidTeamId id) ? id : default;

    public bool Equals(TeamProgressSlot other)
    {
        return TeamIdRaw == other.TeamIdRaw &&
               CurrentProgress == other.CurrentProgress &&
               (bool)AssignmentRequested == (bool)other.AssignmentRequested &&
               InitialTeamSize == other.InitialTeamSize;
    }

    public override bool Equals(object obj) => obj is TeamProgressSlot other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = TeamIdRaw;
            hash = (hash * 397) ^ CurrentProgress;
            hash = (hash * 397) ^ AssignmentRequested.GetHashCode();
            hash = (hash * 397) ^ InitialTeamSize;
            return hash;
        }
    }
}
