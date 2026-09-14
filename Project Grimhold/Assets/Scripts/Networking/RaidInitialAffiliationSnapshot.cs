using System;
using System.Collections.Generic;

/// <summary>
/// Immutable initial Raid affiliation projected from the frozen launch roster.
/// </summary>
public sealed class RaidInitialAffiliationSnapshot
{
    private readonly ProfileId[] _profileIds;
    private readonly RaidParticipantId[] _participantIds;
    private readonly RaidTeamId[] _teamIds;

    private RaidInitialAffiliationSnapshot(
        ProfileId[] profileIds,
        RaidParticipantId[] participantIds,
        RaidTeamId[] teamIds)
    {
        _profileIds = profileIds;
        _participantIds = participantIds;
        _teamIds = teamIds;
    }

    public int ParticipantCount => _participantIds.Length;

    public static bool TryCreate(
        IReadOnlyList<RaidLaunchParticipant> participants,
        out RaidInitialAffiliationSnapshot snapshot)
    {
        snapshot = null;
        if (participants == null || participants.Count < 1 ||
            participants.Count > RaidSessionRules.MaxParticipants)
        {
            return false;
        }

        var profiles = new ProfileId[participants.Count];
        for (int index = 0; index < participants.Count; index++)
        {
            RaidLaunchParticipant participant = participants[index];
            if (!participant.IsValid)
            {
                return false;
            }

            profiles[index] = participant.ProfileId;
        }

        var participantIds = new RaidParticipantId[participants.Count];
        var teamIds = new RaidTeamId[participants.Count];
        var seenIds = new HashSet<RaidParticipantId>();
        for (int index = 0; index < participants.Count; index++)
        {
            RaidLaunchParticipant participant = participants[index];
            if (!RaidParticipantIdAssignment.TryResolve(
                    profiles,
                    participant.ProfileId,
                    out RaidParticipantId participantId) ||
                !seenIds.Add(participantId))
            {
                return false;
            }

            participantIds[index] = participantId;
            teamIds[index] = participant.TeamId;
        }

        snapshot = new RaidInitialAffiliationSnapshot(profiles, participantIds, teamIds);
        return true;
    }

    /// <summary>
    /// Resolves the only other frozen profile that shares the local profile's initial team.
    /// Team identifiers are compared only for equality and are never treated as indexes.
    /// </summary>
    public bool TryGetTeammateProfileId(ProfileId localProfileId, out ProfileId teammateProfileId)
    {
        teammateProfileId = default;
        if (!localProfileId.IsValid)
        {
            return false;
        }

        int localIndex = -1;
        for (int index = 0; index < _profileIds.Length; index++)
        {
            if (_profileIds[index] != localProfileId)
            {
                continue;
            }

            if (localIndex >= 0)
            {
                return false;
            }

            localIndex = index;
        }

        if (localIndex < 0)
        {
            return false;
        }

        RaidTeamId localTeamId = _teamIds[localIndex];
        int teammateIndex = -1;
        for (int index = 0; index < _profileIds.Length; index++)
        {
            if (index == localIndex || _teamIds[index] != localTeamId)
            {
                continue;
            }

            if (teammateIndex >= 0)
            {
                return false;
            }

            teammateIndex = index;
        }

        if (teammateIndex < 0)
        {
            return false;
        }

        teammateProfileId = _profileIds[teammateIndex];
        return teammateProfileId.IsValid && teammateProfileId != localProfileId;
    }

    public bool TryGetTeam(RaidParticipantId participantId, out RaidTeamId teamId)
    {
        teamId = default;
        if (!participantId.IsValid)
        {
            return false;
        }

        for (int index = 0; index < _participantIds.Length; index++)
        {
            if (_participantIds[index] == participantId)
            {
                teamId = _teamIds[index];
                return teamId.IsValid;
            }
        }

        return false;
    }

    public bool TryAreInitialTeammates(
        RaidParticipantId left,
        RaidParticipantId right,
        out bool areTeammates)
    {
        areTeammates = false;
        if (!TryGetTeam(left, out RaidTeamId leftTeam) ||
            !TryGetTeam(right, out RaidTeamId rightTeam))
        {
            return false;
        }

        areTeammates = leftTeam == rightTeam;
        return true;
    }
}
