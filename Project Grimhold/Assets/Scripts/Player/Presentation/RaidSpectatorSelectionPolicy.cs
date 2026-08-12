using System;
using System.Collections.Generic;

/// <summary>Deterministic ProfileId navigation rules for local raid spectators.</summary>
public static class RaidSpectatorSelectionPolicy
{
    public static int FindNextAfterInvalidated(
        IReadOnlyList<string> orderedProfileIds,
        string invalidatedProfileId)
    {
        if (orderedProfileIds == null || orderedProfileIds.Count == 0)
        {
            return -1;
        }

        if (!string.IsNullOrEmpty(invalidatedProfileId))
        {
            for (int index = 0; index < orderedProfileIds.Count; index++)
            {
                if (string.CompareOrdinal(orderedProfileIds[index], invalidatedProfileId) > 0)
                {
                    return index;
                }
            }
        }

        return 0;
    }

    public static int FindRelative(
        IReadOnlyList<string> orderedProfileIds,
        string currentProfileId,
        int direction)
    {
        if (orderedProfileIds == null || orderedProfileIds.Count == 0 || direction == 0)
        {
            return -1;
        }

        int currentIndex = -1;
        for (int index = 0; index < orderedProfileIds.Count; index++)
        {
            if (string.Equals(
                    orderedProfileIds[index],
                    currentProfileId,
                    StringComparison.Ordinal))
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return direction > 0 ? 0 : orderedProfileIds.Count - 1;
        }

        return direction > 0
            ? (currentIndex + 1) % orderedProfileIds.Count
            : (currentIndex - 1 + orderedProfileIds.Count) % orderedProfileIds.Count;
    }
}

