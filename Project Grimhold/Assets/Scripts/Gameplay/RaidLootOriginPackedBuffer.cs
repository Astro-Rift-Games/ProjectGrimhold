using System;

/// <summary>Bit packing for 6-bit catalog index, 5-bit stable origin slot and positive int amount.</summary>
public static class RaidLootOriginPackedBuffer
{
    public const int MaximumCatalogEntries = 64;
    public const int MaximumPlayerOrigins = RaidSessionRules.MaxParticipants;
    public const int OriginsPerLoot = MaximumPlayerOrigins + 1;
    public const int MaximumStacks = 16;
    public const int MaximumBuckets = MaximumStacks * OriginsPerLoot;
    public const int KeyBits = 11;
    public const int AmountBits = 32;
    public const int BitsPerBucket = KeyBits + AmountBits;
    public const int WordCapacity = (MaximumBuckets * BitsPerBucket + 31) / 32;

    public static int PackKey(int catalogIndex, int originSlot) =>
        (catalogIndex << 5) | originSlot;

    public static bool TryUnpackKey(int key, out int catalogIndex, out int originSlot)
    {
        originSlot = key & 31;
        catalogIndex = key >> 5;
        return catalogIndex >= 0 && catalogIndex < MaximumCatalogEntries &&
            originSlot >= 0 && originSlot <= MaximumPlayerOrigins;
    }

    public static RaidLootOriginCompactBucket Read(in RaidLootOriginPackedState state, int bucketIndex)
    {
        int bitOffset = checked(bucketIndex * BitsPerBucket);
        return new RaidLootOriginCompactBucket
        {
            CatalogIndexAndOriginSlot = (int)ReadBits(state, bitOffset, KeyBits),
            Amount = unchecked((int)ReadBits(state, bitOffset + KeyBits, AmountBits))
        };
    }

    public static void Write(ref RaidLootOriginPackedState state, int bucketIndex, in RaidLootOriginCompactBucket bucket)
    {
        int bitOffset = checked(bucketIndex * BitsPerBucket);
        WriteBits(ref state, bitOffset, KeyBits, unchecked((uint)bucket.CatalogIndexAndOriginSlot));
        WriteBits(ref state, bitOffset + KeyBits, AmountBits, unchecked((uint)bucket.Amount));
    }

    public static void Clear(ref RaidLootOriginPackedState state, int bucketIndex)
    {
        Write(ref state, bucketIndex, default);
    }

    private static ulong ReadBits(in RaidLootOriginPackedState state, int bitOffset, int bitCount)
    {
        ulong result = 0;
        int copied = 0;
        while (copied < bitCount)
        {
            int wordIndex = bitOffset >> 5;
            int withinWord = bitOffset & 31;
            int take = Math.Min(bitCount - copied, 32 - withinWord);
            uint mask = take == 32 ? uint.MaxValue : (1u << take) - 1u;
            uint value = unchecked((uint)state.GetWord(wordIndex));
            result |= (ulong)((value >> withinWord) & mask) << copied;
            copied += take;
            bitOffset += take;
        }

        return result;
    }

    private static void WriteBits(ref RaidLootOriginPackedState state, int bitOffset, int bitCount, ulong value)
    {
        int written = 0;
        while (written < bitCount)
        {
            int wordIndex = bitOffset >> 5;
            int withinWord = bitOffset & 31;
            int take = Math.Min(bitCount - written, 32 - withinWord);
            uint rawMask = take == 32 ? uint.MaxValue : (1u << take) - 1u;
            uint mask = rawMask << withinWord;
            uint current = unchecked((uint)state.GetWord(wordIndex));
            uint incoming = unchecked((uint)(value >> written)) & rawMask;
            state.SetWord(wordIndex, unchecked((int)((current & ~mask) | (incoming << withinWord))));
            written += take;
            bitOffset += take;
        }
    }
}
