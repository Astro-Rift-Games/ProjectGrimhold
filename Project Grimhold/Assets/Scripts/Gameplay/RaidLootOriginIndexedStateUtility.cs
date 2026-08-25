using System;
using System.Collections.Generic;

/// <summary>Shared deterministic operations for endpoint-local compact origin tables.</summary>
public static class RaidLootOriginIndexedStateUtility
{
    public static bool TryResolveTransfer(
        in RaidLootOriginPackedState state,
        int catalogIndex,
        int requestedAmount,
        out RaidLootOriginTransfer transfer)
    {
        transfer = null;
        if (requestedAmount <= 0 || !TryValidateState(state))
        {
            return false;
        }

        var available = new List<RaidLootOriginBucket>(RaidLootOriginPackedBuffer.OriginsPerLoot);
        for (int index = 0; index < state.BucketCount; index++)
        {
            RaidLootOriginCompactBucket compact = RaidLootOriginPackedBuffer.Read(state, index);
            if (!RaidLootOriginPackedBuffer.TryUnpackKey(
                    compact.CatalogIndexAndOriginSlot,
                    out int storedCatalogIndex,
                    out int originSlot) || storedCatalogIndex != catalogIndex ||
                !TryResolveOrigin(state, originSlot, out RaidLootOrigin origin))
            {
                continue;
            }

            available.Add(new RaidLootOriginBucket(origin, compact.Amount));
        }

        available.Sort((left, right) => left.Origin.CompareTo(right.Origin));
        var selected = new List<RaidLootOriginBucket>(available.Count);
        int remaining = requestedAmount;
        for (int index = 0; index < available.Count && remaining > 0; index++)
        {
            RaidLootOriginBucket bucket = available[index];
            int selectedAmount = Math.Min(bucket.Amount, remaining);
            selected.Add(new RaidLootOriginBucket(bucket.Origin, selectedAmount));
            remaining -= selectedAmount;
        }

        if (remaining != 0)
        {
            return false;
        }

        transfer = new RaidLootOriginTransfer(selected);
        return transfer.TryGetTotal(out int total) && total == requestedAmount;
    }

    public static bool CanAdd(
        in RaidLootOriginPackedState state,
        int catalogIndex,
        RaidLootOriginTransfer transfer)
    {
        if (transfer == null || catalogIndex < 0 ||
            catalogIndex >= RaidLootOriginPackedBuffer.MaximumCatalogEntries ||
            !transfer.TryGetTotal(out _) || !TryValidateState(state))
        {
            return false;
        }

        int newBuckets = 0;
        for (int index = 0; index < transfer.Count; index++)
        {
            RaidLootOriginBucket bucket = transfer.Buckets[index];
            if (!TryFindBucket(state, catalogIndex, bucket.Origin, out _))
            {
                newBuckets++;
            }
            else if (!TryFindBucket(state, catalogIndex, bucket.Origin, out int foundIndex) ||
                     RaidLootOriginPackedBuffer.Read(state, foundIndex).Amount > int.MaxValue - bucket.Amount)
            {
                return false;
            }
        }

        return state.BucketCount <= RaidLootOriginPackedBuffer.MaximumBuckets - newBuckets;
    }

    public static bool TryAdd(
        ref RaidLootOriginPackedState state,
        int catalogIndex,
        RaidLootOriginTransfer transfer)
    {
        if (!CanAdd(state, catalogIndex, transfer))
        {
            return false;
        }

        for (int index = 0; index < transfer.Count; index++)
        {
            RaidLootOriginBucket incoming = transfer.Buckets[index];
            if (!TryGetOriginSlot(incoming.Origin, out int originSlot))
            {
                return false;
            }

            int key = RaidLootOriginPackedBuffer.PackKey(catalogIndex, originSlot);
            if (TryFindBucketByKey(state, key, out int bucketIndex))
            {
                RaidLootOriginCompactBucket existing = RaidLootOriginPackedBuffer.Read(state, bucketIndex);
                existing.Amount = checked(existing.Amount + incoming.Amount);
                RaidLootOriginPackedBuffer.Write(ref state, bucketIndex, existing);
                continue;
            }

            RaidLootOriginPackedBuffer.Write(ref state, state.BucketCount, new RaidLootOriginCompactBucket
            {
                CatalogIndexAndOriginSlot = key,
                Amount = incoming.Amount
            });
            state.BucketCount++;
        }

        return true;
    }

    public static bool TryRemove(
        ref RaidLootOriginPackedState state,
        int catalogIndex,
        RaidLootOriginTransfer transfer)
    {
        if (transfer == null || !transfer.TryGetTotal(out _) ||
            !TryValidateState(state))
        {
            return false;
        }

        for (int index = 0; index < transfer.Count; index++)
        {
            RaidLootOriginBucket outgoing = transfer.Buckets[index];
            if (!TryFindBucket(state, catalogIndex, outgoing.Origin, out int bucketIndex) ||
                RaidLootOriginPackedBuffer.Read(state, bucketIndex).Amount < outgoing.Amount)
            {
                return false;
            }
        }

        for (int index = 0; index < transfer.Count; index++)
        {
            RaidLootOriginBucket outgoing = transfer.Buckets[index];
            TryFindBucket(state, catalogIndex, outgoing.Origin, out int bucketIndex);
            RaidLootOriginCompactBucket existing = RaidLootOriginPackedBuffer.Read(state, bucketIndex);
            if (existing.Amount == outgoing.Amount)
            {
                int lastIndex = state.BucketCount - 1;
                if (bucketIndex != lastIndex)
                {
                    RaidLootOriginPackedBuffer.Write(
                        ref state,
                        bucketIndex,
                        RaidLootOriginPackedBuffer.Read(state, lastIndex));
                }

                RaidLootOriginPackedBuffer.Clear(ref state, lastIndex);
                state.BucketCount--;
            }
            else
            {
                existing.Amount -= outgoing.Amount;
                RaidLootOriginPackedBuffer.Write(ref state, bucketIndex, existing);
            }
        }

        return true;
    }

    public static bool TryGetEntries(
        in RaidLootOriginPackedState state,
        LootDefinitionCatalog catalog,
        out IReadOnlyList<RaidLootOriginEntry> entries)
    {
        entries = Array.Empty<RaidLootOriginEntry>();
        if (catalog == null || !TryValidateState(state))
        {
            return false;
        }

        var result = new List<RaidLootOriginEntry>(state.BucketCount);
        for (int index = 0; index < state.BucketCount; index++)
        {
            RaidLootOriginCompactBucket bucket = RaidLootOriginPackedBuffer.Read(state, index);
            if (!RaidLootOriginPackedBuffer.TryUnpackKey(
                    bucket.CatalogIndexAndOriginSlot,
                    out int catalogIndex,
                    out int originSlot) ||
                !catalog.TryGetByIndex(catalogIndex, out LootDefinition definition) ||
                !TryResolveOrigin(state, originSlot, out RaidLootOrigin origin))
            {
                return false;
            }

            result.Add(new RaidLootOriginEntry(definition.LootId, origin, bucket.Amount));
        }

        result.Sort((left, right) =>
        {
            if (!catalog.TryGetIndex(left.LootId, out int leftIndex) ||
                !catalog.TryGetIndex(right.LootId, out int rightIndex))
            {
                return string.Compare(left.LootId.Value, right.LootId.Value, StringComparison.Ordinal);
            }

            int catalogComparison = leftIndex.CompareTo(rightIndex);
            return catalogComparison != 0 ? catalogComparison : left.Origin.CompareTo(right.Origin);
        });
        entries = result.AsReadOnly();
        return true;
    }

    public static bool HasExactTotal(
        in RaidLootOriginPackedState state,
        int catalogIndex,
        int expectedAmount)
    {
        if (expectedAmount <= 0 || !TryValidateState(state))
        {
            return false;
        }

        int total = 0;
        try
        {
            for (int index = 0; index < state.BucketCount; index++)
            {
                RaidLootOriginCompactBucket bucket = RaidLootOriginPackedBuffer.Read(state, index);
                RaidLootOriginPackedBuffer.TryUnpackKey(
                    bucket.CatalogIndexAndOriginSlot,
                    out int storedCatalogIndex,
                    out _);
                if (storedCatalogIndex == catalogIndex)
                {
                    total = checked(total + bucket.Amount);
                }
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        return total == expectedAmount;
    }

    public static bool TryValidateState(
        in RaidLootOriginPackedState state)
    {
        if (state.BucketCount < 0 || state.BucketCount > RaidLootOriginPackedBuffer.MaximumBuckets)
        {
            return false;
        }

        var keys = new HashSet<int>();
        for (int index = 0; index < state.BucketCount; index++)
        {
            RaidLootOriginCompactBucket bucket = RaidLootOriginPackedBuffer.Read(state, index);
            if (bucket.Amount <= 0 || !keys.Add(bucket.CatalogIndexAndOriginSlot) ||
                !RaidLootOriginPackedBuffer.TryUnpackKey(
                    bucket.CatalogIndexAndOriginSlot,
                    out _,
                    out int originSlot) ||
                !TryResolveOrigin(state, originSlot, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryFindBucket(
        in RaidLootOriginPackedState state,
        int catalogIndex,
        RaidLootOrigin origin,
        out int bucketIndex)
    {
        bucketIndex = -1;
        int originSlot;
        if (origin.IsDungeon)
        {
            originSlot = 0;
        }
        else if (!origin.IsPlayer)
        {
            return false;
        }
        else
        {
            originSlot = origin.PlayerParticipantId.Value;
        }

        return TryFindBucketByKey(
            state,
            RaidLootOriginPackedBuffer.PackKey(catalogIndex, originSlot),
            out bucketIndex);
    }

    private static bool TryFindBucketByKey(
        in RaidLootOriginPackedState state,
        int key,
        out int bucketIndex)
    {
        for (int index = 0; index < state.BucketCount; index++)
        {
            if (RaidLootOriginPackedBuffer.Read(state, index).CatalogIndexAndOriginSlot == key)
            {
                bucketIndex = index;
                return true;
            }
        }

        bucketIndex = -1;
        return false;
    }

    private static bool TryGetOriginSlot(
        RaidLootOrigin origin,
        out int originSlot)
    {
        originSlot = 0;
        if (origin.IsDungeon)
        {
            return true;
        }

        if (!origin.IsPlayer)
        {
            return false;
        }

        originSlot = origin.PlayerParticipantId.Value;
        return originSlot >= 1 && originSlot <= RaidLootOriginPackedBuffer.MaximumPlayerOrigins;
    }

    private static bool TryResolveOrigin(
        in RaidLootOriginPackedState state,
        int originSlot,
        out RaidLootOrigin origin)
    {
        origin = default;
        if (originSlot == 0)
        {
            origin = RaidLootOrigin.Dungeon;
            return true;
        }

        if (!RaidParticipantId.TryCreate(originSlot, out RaidParticipantId participantId))
        {
            return false;
        }

        return RaidLootOrigin.TryCreatePlayer(participantId, out origin);
    }
}
