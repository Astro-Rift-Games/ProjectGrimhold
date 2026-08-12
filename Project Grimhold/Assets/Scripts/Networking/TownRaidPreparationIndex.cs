using System.Collections.Generic;

/// <summary>
/// Rebuildable lookup derived from preparation snapshots.
/// Snapshots remain the source of membership; this index only validates and routes lookups.
/// </summary>
public sealed class TownRaidPreparationIndex
{
    private readonly Dictionary<RaidCode, TownRaidPreparationSnapshot> _preparationsByCode = new();
    private readonly Dictionary<ProfileId, RaidCode> _preparationCodesByProfile = new();

    public int Count => _preparationsByCode.Count;

    /// <summary>Registers one valid snapshot when its code and every profile are unclaimed.</summary>
    public bool TryRegister(in TownRaidPreparationSnapshot snapshot)
    {
        if (!CanRegister(snapshot, _preparationsByCode, _preparationCodesByProfile))
        {
            return false;
        }

        Register(snapshot, _preparationsByCode, _preparationCodesByProfile);
        return true;
    }

    /// <summary>Atomically replaces the indexed observation for one existing Raid code.</summary>
    public bool TryUpdate(in TownRaidPreparationSnapshot snapshot)
    {
        if (!_preparationsByCode.ContainsKey(snapshot.RaidCode))
        {
            return false;
        }

        var snapshots = new List<TownRaidPreparationSnapshot>(_preparationsByCode.Count);
        foreach (KeyValuePair<RaidCode, TownRaidPreparationSnapshot> pair in _preparationsByCode)
        {
            snapshots.Add(pair.Key == snapshot.RaidCode ? snapshot : pair.Value);
        }

        return TryRebuild(snapshots);
    }

    /// <summary>Removes one preparation and only the profile mappings derived from it.</summary>
    public bool Remove(RaidCode raidCode)
    {
        if (!_preparationsByCode.TryGetValue(raidCode, out TownRaidPreparationSnapshot snapshot))
        {
            return false;
        }

        _preparationsByCode.Remove(raidCode);
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            ProfileId profileId = snapshot.Members[index].ProfileId;
            if (_preparationCodesByProfile.TryGetValue(profileId, out RaidCode mappedCode) &&
                mappedCode == raidCode)
            {
                _preparationCodesByProfile.Remove(profileId);
            }
        }

        return true;
    }

    /// <summary>Resolves one preparation by its unique user-facing code.</summary>
    public bool TryGetByRaidCode(RaidCode raidCode, out TownRaidPreparationSnapshot snapshot)
    {
        return _preparationsByCode.TryGetValue(raidCode, out snapshot);
    }

    /// <summary>Resolves the single preparation containing a stable profile identity.</summary>
    public bool TryGetByProfile(ProfileId profileId, out TownRaidPreparationSnapshot snapshot)
    {
        if (_preparationCodesByProfile.TryGetValue(profileId, out RaidCode raidCode))
        {
            return _preparationsByCode.TryGetValue(raidCode, out snapshot);
        }

        snapshot = default;
        return false;
    }

    /// <summary>
    /// Atomically rebuilds both derived indexes. Invalid input leaves the previous indexes intact.
    /// </summary>
    public bool TryRebuild(IReadOnlyList<TownRaidPreparationSnapshot> snapshots)
    {
        if (!TryBuild(snapshots, out Dictionary<RaidCode, TownRaidPreparationSnapshot> byCode,
                out Dictionary<ProfileId, RaidCode> byProfile))
        {
            return false;
        }

        _preparationsByCode.Clear();
        _preparationCodesByProfile.Clear();
        foreach (KeyValuePair<RaidCode, TownRaidPreparationSnapshot> pair in byCode)
        {
            _preparationsByCode.Add(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<ProfileId, RaidCode> pair in byProfile)
        {
            _preparationCodesByProfile.Add(pair.Key, pair.Value);
        }

        return true;
    }

    private static bool TryBuild(
        IReadOnlyList<TownRaidPreparationSnapshot> snapshots,
        out Dictionary<RaidCode, TownRaidPreparationSnapshot> byCode,
        out Dictionary<ProfileId, RaidCode> byProfile)
    {
        byCode = new Dictionary<RaidCode, TownRaidPreparationSnapshot>();
        byProfile = new Dictionary<ProfileId, RaidCode>();
        if (snapshots == null)
        {
            return false;
        }

        for (int index = 0; index < snapshots.Count; index++)
        {
            TownRaidPreparationSnapshot snapshot = snapshots[index];
            if (!CanRegister(snapshot, byCode, byProfile))
            {
                return false;
            }

            Register(snapshot, byCode, byProfile);
        }

        return true;
    }

    private static bool CanRegister(
        in TownRaidPreparationSnapshot snapshot,
        IReadOnlyDictionary<RaidCode, TownRaidPreparationSnapshot> byCode,
        IReadOnlyDictionary<ProfileId, RaidCode> byProfile)
    {
        if (!TownRaidPreparationRules.IsValidSnapshot(snapshot) ||
            byCode.ContainsKey(snapshot.RaidCode))
        {
            return false;
        }

        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            if (byProfile.ContainsKey(snapshot.Members[index].ProfileId))
            {
                return false;
            }
        }

        return true;
    }

    private static void Register(
        in TownRaidPreparationSnapshot snapshot,
        IDictionary<RaidCode, TownRaidPreparationSnapshot> byCode,
        IDictionary<ProfileId, RaidCode> byProfile)
    {
        byCode.Add(snapshot.RaidCode, snapshot);
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            byProfile.Add(snapshot.Members[index].ProfileId, snapshot.RaidCode);
        }
    }
}
