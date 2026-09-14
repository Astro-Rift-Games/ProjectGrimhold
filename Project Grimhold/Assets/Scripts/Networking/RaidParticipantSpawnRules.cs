using System.Collections.Generic;
using UnityEngine;
using Spawning;

/// <summary>
/// Deterministic fresh-Raid player-spawn rules. Stable team appearance order selects an area,
/// and stable profile order inside that team selects a position. PlayerRef is intentionally
/// absent because it belongs only to the current runner.
/// </summary>
public static class RaidParticipantSpawnRules
{
    public static bool ValidateFreshSpawnPreflight(
        RaidLaunchContext launchContext,
        IReadOnlyList<PlayerSpawnAreaDefinition> spawnAreas,
        out string failure)
    {
        failure = null;
        if (launchContext == null)
        {
            failure = "Canonical launch context is missing.";
            return false;
        }

        if (!launchContext.RaidCode.IsValid ||
            !RaidSessionRules.IsValidLaunchRevision(launchContext.LaunchRevision) ||
            !RaidSessionRules.IsValidParticipantCohort(
                launchContext.HostProfileId,
                launchContext.ParticipantProfileIds) ||
            !RaidSessionRules.ContainsProfile(
                launchContext.ParticipantProfileIds,
                launchContext.LocalProfileId))
        {
            failure = "Canonical launch context identity or revision is invalid.";
            return false;
        }

        IReadOnlyList<RaidLaunchParticipant> participants = launchContext.Participants;
        if (participants == null || !RaidSessionRules.IsValidParticipantCount(participants.Count))
        {
            failure = "Frozen Raid participant count is invalid.";
            return false;
        }

        if (spawnAreas == null || spawnAreas.Count == 0)
        {
            failure = "Player spawn areas are missing.";
            return false;
        }

        var seenTransforms = new HashSet<Transform>();
        var seenPositions = new HashSet<Vector3>();
        for (int areaIndex = 0; areaIndex < spawnAreas.Count; areaIndex++)
        {
            PlayerSpawnAreaDefinition area = spawnAreas[areaIndex];
            if (area == null || area.SpawnPoints == null || area.SpawnPoints.Count == 0)
            {
                failure = $"Player spawn area {areaIndex} is missing or empty.";
                return false;
            }

            for (int pointIndex = 0; pointIndex < area.SpawnPoints.Count; pointIndex++)
            {
                Transform spawnPoint = area.SpawnPoints[pointIndex];
                if (spawnPoint == null)
                {
                    failure = $"Player spawn area {areaIndex} point {pointIndex} is null.";
                    return false;
                }

                if (!seenTransforms.Add(spawnPoint))
                {
                    failure = $"Player spawn transform '{spawnPoint.name}' is configured more than once.";
                    return false;
                }

                if (!seenPositions.Add(spawnPoint.position))
                {
                    failure = $"Player spawn position {spawnPoint.position} is configured more than once.";
                    return false;
                }
            }
        }

        var seenAssignments = new HashSet<(int AreaIndex, int PointIndex)>();
        for (int index = 0; index < participants.Count; index++)
        {
            if (!TryResolveSpawnAssignment(
                    participants,
                    participants[index].ProfileId,
                    spawnAreas,
                    out int areaIndex,
                    out int pointIndex,
                    out failure))
            {
                return false;
            }

            if (!seenAssignments.Add((areaIndex, pointIndex)))
            {
                failure = $"Participants resolve to the same player spawn assignment {areaIndex}:{pointIndex}.";
                return false;
            }
        }

        return true;
    }

    public static bool TryResolveSpawnAssignment(
        IReadOnlyList<RaidLaunchParticipant> participants,
        ProfileId profileId,
        IReadOnlyList<PlayerSpawnAreaDefinition> spawnAreas,
        out int areaIndex,
        out int pointIndex,
        out string failure)
    {
        areaIndex = -1;
        pointIndex = -1;
        failure = null;
        if (!profileId.IsValid || participants == null ||
            !RaidSessionRules.IsValidParticipantCount(participants.Count))
        {
            failure = "Player profile or frozen Raid participants are invalid.";
            return false;
        }

        var orderedTeams = new List<RaidTeamId>();
        var teamMemberCounts = new List<int>();
        var seenProfiles = new HashSet<ProfileId>();
        bool found = false;
        int resolvedAreaIndex = -1;
        int resolvedPointIndex = -1;
        for (int index = 0; index < participants.Count; index++)
        {
            RaidLaunchParticipant participant = participants[index];
            if (!participant.IsValid || !seenProfiles.Add(participant.ProfileId))
            {
                failure = "Frozen Raid participants contain an invalid or duplicate entry.";
                return false;
            }

            int teamIndex = IndexOf(orderedTeams, participant.TeamId);
            if (teamIndex < 0)
            {
                teamIndex = orderedTeams.Count;
                orderedTeams.Add(participant.TeamId);
                teamMemberCounts.Add(0);
            }

            if (participant.ProfileId == profileId)
            {
                found = true;
                resolvedAreaIndex = teamIndex;
                resolvedPointIndex = teamMemberCounts[teamIndex];
            }

            teamMemberCounts[teamIndex]++;
        }

        if (!found)
        {
            areaIndex = -1;
            pointIndex = -1;
            failure = $"Profile '{profileId}' does not belong to the frozen Raid roster.";
            return false;
        }

        if (spawnAreas == null || resolvedAreaIndex < 0 || resolvedAreaIndex >= spawnAreas.Count)
        {
            failure = $"No player spawn area is available for team ordinal {resolvedAreaIndex}.";
            return false;
        }

        PlayerSpawnAreaDefinition area = spawnAreas[resolvedAreaIndex];
        if (area == null || area.SpawnPoints == null ||
            resolvedPointIndex < 0 || resolvedPointIndex >= area.SpawnPoints.Count ||
            area.SpawnPoints[resolvedPointIndex] == null)
        {
            failure = $"Player spawn area {resolvedAreaIndex} has no valid position {resolvedPointIndex}.";
            return false;
        }

        areaIndex = resolvedAreaIndex;
        pointIndex = resolvedPointIndex;
        return true;
    }

    private static int IndexOf(IReadOnlyList<RaidTeamId> teams, RaidTeamId teamId)
    {
        for (int index = 0; index < teams.Count; index++)
        {
            if (teams[index] == teamId)
            {
                return index;
            }
        }

        return -1;
    }
}
