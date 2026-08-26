using System;
using System.Collections.Generic;

/// <summary>
/// Immutable initial Raid affiliation keyed by the canonical <see cref="RaidParticipantId"/>.
/// </summary>
public sealed class RaidInitialAffiliationSnapshot
{
    private readonly RaidParticipantId[] _participantIds;
    private readonly RaidTeamId[] _teamIds;

    private RaidInitialAffiliationSnapshot(
        RaidParticipantId[] participantIds,
        RaidTeamId[] teamIds)
    {
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

        snapshot = new RaidInitialAffiliationSnapshot(participantIds, teamIds);
        return true;
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
