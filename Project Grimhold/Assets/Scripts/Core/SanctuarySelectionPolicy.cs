using System;

/// <summary>
/// Pure deterministic policy for selecting one index from an already ordered candidate set.
/// </summary>
public static class SanctuarySelectionPolicy
{
    /// <summary>
    /// Selects one index reproducibly from a positive candidate count.
    /// The caller owns candidate ordering and candidate-set validation.
    /// </summary>
    public static int SelectIndex(ulong sessionSeed, int simulationTick, EntityId playerId, int candidateCount)
    {
        if (playerId.Value == 0)
        {
            throw new ArgumentException("Player identity must be valid.", nameof(playerId));
        }

        if (candidateCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateCount));
        }

        unchecked
        {
            ulong mixed = sessionSeed;
            mixed ^= (ulong)(uint)simulationTick * 0x9E3779B97F4A7C15UL;
            mixed ^= (ulong)(uint)playerId.Value * 0xBF58476D1CE4E5B9UL;
            mixed ^= (ulong)(uint)candidateCount * 0x94D049BB133111EBUL;
            mixed += 0x9E3779B97F4A7C15UL;
            mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            return (int)(mixed % (ulong)candidateCount);
        }
    }
}
