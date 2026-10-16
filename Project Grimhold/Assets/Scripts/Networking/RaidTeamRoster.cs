using System;
using System.Collections.Generic;

/// <summary>
/// Immutable projection of the frozen launch cohort into verified team groupings.
/// Enforces rules A-1 (team size must be 1 or 2) and A-2 (total teams must not exceed configured capacity).
/// </summary>
public sealed class RaidTeamRoster
{
    private readonly RaidTeamId[] _teams;
    private readonly Dictionary<RaidTeamId, RaidParticipantId[]> _teamMembers;
    private readonly Dictionary<RaidTeamId, ProfileId[]> _teamProfiles;
    private readonly Dictionary<RaidParticipantId, RaidTeamId> _participantTeams;
    private readonly Dictionary<ProfileId, RaidTeamId> _profileTeams;

    private RaidTeamRoster(
        RaidTeamId[] teams,
        Dictionary<RaidTeamId, RaidParticipantId[]> teamMembers,
        Dictionary<RaidTeamId, ProfileId[]> teamProfiles,
        Dictionary<RaidParticipantId, RaidTeamId> participantTeams,
        Dictionary<ProfileId, RaidTeamId> profileTeams)
    {
        _teams = teams;
        _teamMembers = teamMembers;
        _teamProfiles = teamProfiles;
        _participantTeams = participantTeams;
        _profileTeams = profileTeams;
    }

    /// <summary>
    /// Total number of distinct teams in this roster.
    /// </summary>
    public int TeamCount => _teams.Length;

    /// <summary>
    /// Ordered list of unique teams in the cohort.
    /// </summary>
    public IReadOnlyList<RaidTeamId> Teams => _teams;

    /// <summary>
    /// Attempts to build a verified <see cref="RaidTeamRoster"/> using the default ceiling <see cref="RaidSessionRules.MaxTeams"/>.
    /// </summary>
    public static bool TryCreate(
        IReadOnlyList<RaidLaunchParticipant> participants,
        out RaidTeamRoster roster)
    {
        return TryCreate(participants, RaidSessionRules.MaxTeams, out roster, out _);
    }

    /// <summary>
    /// Attempts to build a verified <see cref="RaidTeamRoster"/> validating team sizes (1-2) and team capacity limit.
    /// </summary>
    public static bool TryCreate(
        IReadOnlyList<RaidLaunchParticipant> participants,
        int maxTeamsPerRaid,
        out RaidTeamRoster roster,
        out string validationError)
    {
        roster = null;
        validationError = null;

        if (participants == null || participants.Count < 1 || participants.Count > RaidSessionRules.MaxParticipants)
        {
            validationError = $"Participant count must be between 1 and {RaidSessionRules.MaxParticipants}.";
            return false;
        }

        if (maxTeamsPerRaid < 1 || maxTeamsPerRaid > RaidSessionRules.MaxTeams)
        {
            validationError = $"MaxTeamsPerRaid must be between 1 and {RaidSessionRules.MaxTeams}.";
            return false;
        }

        var profiles = new ProfileId[participants.Count];
        var seenProfiles = new HashSet<ProfileId>();
        for (int i = 0; i < participants.Count; i++)
        {
            RaidLaunchParticipant p = participants[i];
            if (!p.IsValid)
            {
                validationError = $"Participant at index {i} is invalid.";
                return false;
            }

            if (!seenProfiles.Add(p.ProfileId))
            {
                validationError = $"Duplicate profile '{p.ProfileId}' in cohort.";
                return false;
            }

            profiles[i] = p.ProfileId;
        }

        var participantIds = new RaidParticipantId[participants.Count];
        for (int i = 0; i < participants.Count; i++)
        {
            ProfileId profile = profiles[i];
            if (!RaidParticipantIdAssignment.TryResolve(profiles, profile, out RaidParticipantId participantId))
            {
                validationError = $"Could not assign RaidParticipantId for profile '{profile}'.";
                return false;
            }

            participantIds[i] = participantId;
        }

        var teamOrder = new List<RaidTeamId>();
        var teamMembersList = new Dictionary<RaidTeamId, List<RaidParticipantId>>();
        var teamProfilesList = new Dictionary<RaidTeamId, List<ProfileId>>();
        var participantTeams = new Dictionary<RaidParticipantId, RaidTeamId>();
        var profileTeams = new Dictionary<ProfileId, RaidTeamId>();

        for (int i = 0; i < participants.Count; i++)
        {
            RaidLaunchParticipant p = participants[i];
            RaidTeamId teamId = p.TeamId;
            RaidParticipantId participantId = participantIds[i];
            ProfileId profileId = p.ProfileId;

            if (!teamMembersList.TryGetValue(teamId, out List<RaidParticipantId> mList))
            {
                mList = new List<RaidParticipantId>();
                teamMembersList[teamId] = mList;
                teamProfilesList[teamId] = new List<ProfileId>();
                teamOrder.Add(teamId);
            }

            mList.Add(participantId);
            teamProfilesList[teamId].Add(profileId);
            participantTeams[participantId] = teamId;
            profileTeams[profileId] = teamId;
        }

        if (teamOrder.Count > maxTeamsPerRaid)
        {
            validationError = $"Total teams ({teamOrder.Count}) exceeds maximum allowed teams ({maxTeamsPerRaid}).";
            return false;
        }

        // Validate A-1: team size must be exactly 1 or 2
        var teamMembers = new Dictionary<RaidTeamId, RaidParticipantId[]>();
        var teamProfiles = new Dictionary<RaidTeamId, ProfileId[]>();

        foreach (RaidTeamId teamId in teamOrder)
        {
            List<RaidParticipantId> mList = teamMembersList[teamId];
            if (mList.Count < 1 || mList.Count > 2)
            {
                validationError = $"Team '{teamId}' has invalid size ({mList.Count}). Only Solo (1) and Duo (2) teams are supported.";
                return false;
            }

            teamMembers[teamId] = mList.ToArray();
            teamProfiles[teamId] = teamProfilesList[teamId].ToArray();
        }

        roster = new RaidTeamRoster(
            teamOrder.ToArray(),
            teamMembers,
            teamProfiles,
            participantTeams,
            profileTeams);

        return true;
    }

    /// <summary>
    /// Checks whether the specified team belongs to this roster.
    /// </summary>
    public bool ContainsTeam(RaidTeamId teamId) => teamId.IsValid && _teamMembers.ContainsKey(teamId);

    /// <summary>
    /// Resolves the initial frozen team size (1 or 2) for the given team.
    /// </summary>
    public bool TryGetInitialTeamSize(RaidTeamId teamId, out int initialSize)
    {
        initialSize = 0;
        if (!teamId.IsValid || !_teamMembers.TryGetValue(teamId, out RaidParticipantId[] members))
        {
            return false;
        }

        initialSize = members.Length;
        return true;
    }

    /// <summary>
    /// Returns the initial frozen team size (1 or 2) or throws if not in roster.
    /// </summary>
    public int GetInitialTeamSize(RaidTeamId teamId)
    {
        if (TryGetInitialTeamSize(teamId, out int size))
        {
            return size;
        }

        throw new ArgumentException($"Team '{teamId}' does not exist in roster.", nameof(teamId));
    }

    /// <summary>
    /// Resolves the frozen participant identities of the given team.
    /// </summary>
    public bool TryGetTeamMembers(RaidTeamId teamId, out IReadOnlyList<RaidParticipantId> members)
    {
        members = null;
        if (!teamId.IsValid || !_teamMembers.TryGetValue(teamId, out RaidParticipantId[] array))
        {
            return false;
        }

        members = array;
        return true;
    }

    /// <summary>
    /// Resolves the frozen profiles belonging to the given team.
    /// </summary>
    public bool TryGetTeamProfiles(RaidTeamId teamId, out IReadOnlyList<ProfileId> profiles)
    {
        profiles = null;
        if (!teamId.IsValid || !_teamProfiles.TryGetValue(teamId, out ProfileId[] array))
        {
            return false;
        }

        profiles = array;
        return true;
    }

    /// <summary>
    /// Resolves the team of a participant identity.
    /// </summary>
    public bool TryGetTeam(RaidParticipantId participantId, out RaidTeamId teamId)
    {
        teamId = default;
        return participantId.IsValid && _participantTeams.TryGetValue(participantId, out teamId);
    }

    /// <summary>
    /// Resolves the team of a player profile.
    /// </summary>
    public bool TryGetTeam(ProfileId profileId, out RaidTeamId teamId)
    {
        teamId = default;
        return profileId.IsValid && _profileTeams.TryGetValue(profileId, out teamId);
    }

    /// <summary>
    /// Checks whether two participant identities were configured as initial teammates.
    /// </summary>
    public bool TryAreInitialTeammates(RaidParticipantId left, RaidParticipantId right, out bool areTeammates)
    {
        areTeammates = false;
        if (!TryGetTeam(left, out RaidTeamId leftTeam) || !TryGetTeam(right, out RaidTeamId rightTeam))
        {
            return false;
        }

        areTeammates = leftTeam == rightTeam;
        return true;
    }
}
