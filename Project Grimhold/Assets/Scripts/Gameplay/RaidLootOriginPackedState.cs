using System;
using Fusion;

/// <summary>Dense replicated buckets whose 5-bit origin is Dungeon (0) or RaidParticipantId (1..16).</summary>
public unsafe struct RaidLootOriginPackedState : INetworkStruct
{
    public fixed int Words[RaidLootOriginPackedBuffer.WordCapacity];
    public int BucketCount;

    public int GetWord(int index)
    {
        ValidateWordIndex(index);
        fixed (int* words = Words)
        {
            return words[index];
        }
    }

    public void SetWord(int index, int value)
    {
        ValidateWordIndex(index);
        fixed (int* words = Words)
        {
            words[index] = value;
        }
    }

    private static void ValidateWordIndex(int index)
    {
        if ((uint)index >= RaidLootOriginPackedBuffer.WordCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
