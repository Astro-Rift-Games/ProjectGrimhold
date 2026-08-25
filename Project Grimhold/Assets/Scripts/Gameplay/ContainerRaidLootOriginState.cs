using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>Replicated Raid provenance owned by one NetworkLootContainer endpoint.</summary>
[DisallowMultipleComponent]
public sealed class ContainerRaidLootOriginState : NetworkBehaviour
{
    [Networked]
    private RaidLootOriginPackedState Origins { get; set; }

    public bool TryInitializeDungeon(
        IReadOnlyList<KeyValuePair<int, int>> entries,
        out string error)
    {
        error = null;
        RaidLootOriginPackedState state = Origins;
        if (!HasStateAuthority || entries == null || state.BucketCount != 0)
        {
            error = "Container Dungeon provenance requires empty authoritative state.";
            return false;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            KeyValuePair<int, int> entry = entries[index];
            if (!RaidLootOriginIndexedStateUtility.TryAdd(
                    ref state,
                    entry.Key,
                    RaidLootOriginTransfer.Dungeon(entry.Value)))
            {
                error = "Natural container provenance cannot be represented.";
                return false;
            }
        }

        Origins = state;
        return true;
    }

    public bool HasExactTotal(int catalogIndex, int expectedAmount) =>
        RaidLootOriginIndexedStateUtility.HasExactTotal(
            Origins, catalogIndex, expectedAmount);

    public bool TryResolveTransfer(int catalogIndex, int amount, out RaidLootOriginTransfer transfer) =>
        RaidLootOriginIndexedStateUtility.TryResolveTransfer(
            Origins, catalogIndex, amount, out transfer);

    public bool CanReceive(int catalogIndex, RaidLootOriginTransfer transfer) =>
        HasStateAuthority && RaidLootOriginIndexedStateUtility.CanAdd(
            Origins, catalogIndex, transfer);

    public void CommitReceive(int catalogIndex, RaidLootOriginTransfer transfer)
    {
        RaidLootOriginPackedState state = Origins;
        if (!HasStateAuthority || !RaidLootOriginIndexedStateUtility.TryAdd(
                ref state, catalogIndex, transfer))
        {
            throw new InvalidOperationException("Validated container provenance reception could not be committed.");
        }

        Origins = state;
    }

    public void CommitExtraction(int catalogIndex, RaidLootOriginTransfer transfer)
    {
        RaidLootOriginPackedState state = Origins;
        if (!HasStateAuthority || !RaidLootOriginIndexedStateUtility.TryRemove(
                ref state, catalogIndex, transfer))
        {
            throw new InvalidOperationException("Validated container provenance extraction could not be committed.");
        }

        Origins = state;
    }

    public bool TryGetEntries(
        LootDefinitionCatalog catalog,
        out IReadOnlyList<RaidLootOriginEntry> entries) =>
        RaidLootOriginIndexedStateUtility.TryGetEntries(
            Origins, catalog, out entries);

    public bool TryLoadExact(
        IReadOnlyList<RaidLootOriginEntry> entries,
        LootDefinitionCatalog catalog,
        out string error)
    {
        error = null;
        RaidLootOriginPackedState state = Origins;
        if (!HasStateAuthority || state.BucketCount != 0 || entries == null || catalog == null)
        {
            error = "Container provenance must be empty before exact loading.";
            return false;
        }

        var grouped = new Dictionary<int, List<RaidLootOriginBucket>>();
        for (int index = 0; index < entries.Count; index++)
        {
            RaidLootOriginEntry entry = entries[index];
            if (!entry.IsValid || !catalog.TryGetIndex(entry.LootId, out int catalogIndex))
            {
                error = "Exact provenance contains an invalid entry.";
                return false;
            }

            if (!grouped.TryGetValue(catalogIndex, out List<RaidLootOriginBucket> buckets))
            {
                buckets = new List<RaidLootOriginBucket>();
                grouped.Add(catalogIndex, buckets);
            }
            buckets.Add(new RaidLootOriginBucket(entry.Origin, entry.Amount));
        }

        var catalogIndices = new List<int>(grouped.Keys);
        catalogIndices.Sort();
        for (int index = 0; index < catalogIndices.Count; index++)
        {
            int catalogIndex = catalogIndices[index];
            List<RaidLootOriginBucket> buckets = grouped[catalogIndex];
            buckets.Sort((left, right) => left.Origin.CompareTo(right.Origin));
            if (!RaidLootOriginIndexedStateUtility.TryAdd(
                    ref state,
                    catalogIndex,
                    new RaidLootOriginTransfer(buckets)))
            {
                error = "Exact provenance exceeds container capacity or is inconsistent.";
                return false;
            }
        }

        Origins = state;
        return true;
    }

    public bool HasExactEntries(IReadOnlyList<RaidLootOriginEntry> expected, LootDefinitionCatalog catalog)
    {
        if (!TryGetEntries(catalog, out IReadOnlyList<RaidLootOriginEntry> current) ||
            expected == null || expected.Count != current.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.Count; index++)
        {
            if (!expected[index].Equals(current[index]))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryClearExact(
        IReadOnlyList<RaidLootOriginEntry> expected,
        LootDefinitionCatalog catalog,
        out string error)
    {
        error = null;
        if (!HasStateAuthority || !HasExactEntries(expected, catalog))
        {
            error = "Container provenance differs from the expected snapshot.";
            return false;
        }

        RaidLootOriginPackedState state = Origins;
        for (int index = state.BucketCount - 1; index >= 0; index--)
        {
            RaidLootOriginPackedBuffer.Clear(ref state, index);
        }
        state.BucketCount = 0;
        Origins = state;
        return true;
    }
}
