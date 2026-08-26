using System;

/// <summary>One stable profile and its immutable initial team for a Raid launch.</summary>
public readonly struct RaidLaunchParticipant : IEquatable<RaidLaunchParticipant>
{
    public RaidLaunchParticipant(ProfileId profileId, RaidTeamId teamId)
    {
        ProfileId = profileId;
        TeamId = teamId;
    }

    public ProfileId ProfileId { get; }
    public RaidTeamId TeamId { get; }
    public bool IsValid => ProfileId.IsValid && TeamId.IsValid;

    public bool Equals(RaidLaunchParticipant other) =>
        ProfileId == other.ProfileId && TeamId == other.TeamId;
    public override bool Equals(object obj) => obj is RaidLaunchParticipant other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(ProfileId, TeamId);
}
