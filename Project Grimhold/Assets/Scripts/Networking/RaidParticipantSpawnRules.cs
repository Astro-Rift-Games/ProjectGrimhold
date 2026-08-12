using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic Raid player-spawn rules. Stable ProfileId order selects a spawn;
/// PlayerRef is intentionally absent because it belongs only to the current runner.
/// </summary>
public static class RaidParticipantSpawnRules
{
    public static bool ValidateSpawnPoints(
        IReadOnlyList<Transform> spawnPoints,
        int expectedParticipants,
        out string failure)
    {
        failure = null;
        if (spawnPoints == null)
        {
            failure = "Player spawn group is missing.";
            return false;
        }

        var positions = new List<Vector3>(spawnPoints.Count);
        for (int index = 0; index < spawnPoints.Count; index++)
        {
            if (spawnPoints[index] == null)
            {
                failure = $"Player spawn point {index} is null.";
                return false;
            }

            positions.Add(spawnPoints[index].position);
        }

        return ValidateSpawnPositions(positions, expectedParticipants, out failure);
    }

    public static bool TryGetSpawnIndex(
        IReadOnlyList<ProfileId> frozenProfiles,
        ProfileId profileId,
        out int spawnIndex)
    {
        spawnIndex = -1;
        if (!profileId.IsValid || frozenProfiles == null ||
            frozenProfiles.Count < 1 || frozenProfiles.Count > RaidSessionRules.MaxParticipants)
        {
            return false;
        }

        for (int index = 0; index < frozenProfiles.Count; index++)
        {
            if (!frozenProfiles[index].IsValid)
            {
                return false;
            }

            if (frozenProfiles[index] == profileId)
            {
                if (spawnIndex >= 0)
                {
                    spawnIndex = -1;
                    return false;
                }

                spawnIndex = index;
            }
        }

        return spawnIndex >= 0;
    }

    public static bool ValidateSpawnPositions(
        IReadOnlyList<Vector3> positions,
        int expectedParticipants,
        out string failure)
    {
        failure = null;
        if (!RaidSessionRules.IsValidParticipantCount(expectedParticipants))
        {
            failure = "Expected participant count is outside Raid capacity.";
            return false;
        }

        if (positions == null || positions.Count < expectedParticipants)
        {
            failure = $"Player spawn group requires {expectedParticipants} valid points.";
            return false;
        }

        for (int index = 0; index < expectedParticipants; index++)
        {
            Vector3 position = positions[index];
            for (int other = index + 1; other < expectedParticipants; other++)
            {
                if (position == positions[other])
                {
                    failure = $"Player spawn points {index} and {other} share the same position.";
                    return false;
                }
            }
        }

        return true;
    }
}
