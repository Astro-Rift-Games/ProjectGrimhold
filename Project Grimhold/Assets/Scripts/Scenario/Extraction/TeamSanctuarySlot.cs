using System;
using Fusion;

/// <summary>
/// Replicated network struct representing a single team's ritual and reservation state on an extraction sanctuary.
/// At most one slot is assigned at a time, because a Sanctuary is reserved for its owning team.
/// </summary>
[System.Serializable]
public struct TeamSanctuarySlot : INetworkStruct, IEquatable<TeamSanctuarySlot>
{
    public int TeamIdRaw;
    public ExtractionRitualState RitualState;
    public TickTimer RitualTimer;

    public bool IsEmpty => TeamIdRaw == 0;
    public bool IsAssigned => TeamIdRaw != 0;
    public RaidTeamId TeamId => RaidTeamId.TryCreate(TeamIdRaw, out RaidTeamId id) ? id : default;

    public bool Equals(TeamSanctuarySlot other)
    {
        return TeamIdRaw == other.TeamIdRaw &&
               RitualState == other.RitualState &&
               RitualTimer.TargetTick == other.RitualTimer.TargetTick;
    }

    public override bool Equals(object obj) => obj is TeamSanctuarySlot other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = TeamIdRaw;
            hash = (hash * 397) ^ (int)RitualState;
            hash = (hash * 397) ^ (RitualTimer.TargetTick ?? 0);
            return hash;
        }
    }

    public static bool operator ==(TeamSanctuarySlot left, TeamSanctuarySlot right) => left.Equals(right);
    public static bool operator !=(TeamSanctuarySlot left, TeamSanctuarySlot right) => !left.Equals(right);
}
