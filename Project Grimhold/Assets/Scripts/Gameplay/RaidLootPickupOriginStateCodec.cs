using System;
using System.Collections.Generic;

/// <summary>Pure validation and conversion for the pickup's compact single-stack origin state.</summary>
public static class RaidLootPickupOriginStateCodec
{
    public static bool TryDecode(
        in RaidLootPickupCompactOriginState state,
        int totalAmount,
        out RaidLootOriginTransfer transfer)
    {
        transfer = null;
        if (totalAmount <= 0 || state.DungeonAmount < 0)
        {
            return false;
        }

        var buckets = new List<RaidLootOriginBucket>(RaidLootOriginPackedBuffer.OriginsPerLoot);
        int representedTotal = 0;
        if (state.DungeonAmount > 0)
        {
            buckets.Add(new RaidLootOriginBucket(RaidLootOrigin.Dungeon, state.DungeonAmount));
            representedTotal = state.DungeonAmount;
        }

        try
        {
            for (int playerSlot = 1; playerSlot <= RaidLootOriginPackedBuffer.MaximumPlayerOrigins; playerSlot++)
            {
                int amount = playerSlot < RaidLootOriginPackedBuffer.MaximumPlayerOrigins
                    ? state.GetStoredAmount(playerSlot)
                    : checked(totalAmount - representedTotal);
                if (amount < 0)
                {
                    return false;
                }
                if (amount == 0)
                {
                    continue;
                }
                if (!RaidParticipantId.TryCreate(playerSlot, out RaidParticipantId participantId) ||
                    !RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin origin))
                {
                    return false;
                }

                buckets.Add(new RaidLootOriginBucket(origin, amount));
                representedTotal = checked(representedTotal + amount);
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        buckets.Sort((left, right) => left.Origin.CompareTo(right.Origin));
        transfer = new RaidLootOriginTransfer(buckets);
        return transfer.TryGetTotal(out int resolvedTotal) && resolvedTotal == totalAmount;
    }

    public static bool TryEncode(
        RaidLootOriginTransfer transfer,
        int expectedAmount,
        out RaidLootPickupCompactOriginState state)
    {
        state = default;
        if (transfer == null || !transfer.TryGetTotal(out int total) || total != expectedAmount ||
            transfer.Count > RaidLootOriginPackedBuffer.OriginsPerLoot)
        {
            return false;
        }

        for (int index = 0; index < transfer.Count; index++)
        {
            RaidLootOriginBucket bucket = transfer.Buckets[index];
            if (bucket.Origin.IsDungeon)
            {
                state.SetStoredAmount(0, bucket.Amount);
                continue;
            }

            if (!bucket.Origin.IsPlayer)
            {
                state = default;
                return false;
            }

            int playerSlot = bucket.Origin.PlayerParticipantId.Value;
            if (playerSlot < RaidLootOriginPackedBuffer.MaximumPlayerOrigins)
            {
                state.SetStoredAmount(playerSlot, bucket.Amount);
            }
        }

        return true;
    }
}
