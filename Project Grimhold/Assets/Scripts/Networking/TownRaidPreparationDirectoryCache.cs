using System.Collections.Generic;

/// <summary>
/// Local derived index for replicated Town preparations. It tracks every claimant so
/// duplicate codes or profiles remain unresolved instead of using last-write-wins.
/// </summary>
public sealed class TownRaidPreparationDirectoryCache<TPreparation> where TPreparation : class
{
    private readonly Dictionary<TPreparation, TownRaidPreparationSnapshot> _snapshots = new();
    private readonly Dictionary<RaidCode, HashSet<TPreparation>> _codeClaims = new();
    private readonly Dictionary<ProfileId, HashSet<TPreparation>> _profileClaims = new();

    public int Count => _snapshots.Count;
    public bool IsConsistent { get; private set; } = true;

    public bool RegisterOrUpdate(TPreparation preparation, in TownRaidPreparationSnapshot snapshot)
    {
        if (preparation == null || !TownRaidPreparationRules.IsValidSnapshot(snapshot))
        {
            return false;
        }

        if (_snapshots.TryGetValue(preparation, out TownRaidPreparationSnapshot previous))
        {
            RemoveClaims(preparation, previous);
        }

        _snapshots[preparation] = snapshot;
        AddClaim(_codeClaims, snapshot.RaidCode, preparation);
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            AddClaim(_profileClaims, snapshot.Members[index].ProfileId, preparation);
        }

        RefreshConsistency();
        return true;
    }

    public bool Unregister(TPreparation preparation)
    {
        if (preparation == null || !_snapshots.Remove(preparation, out TownRaidPreparationSnapshot snapshot))
        {
            return false;
        }

        RemoveClaims(preparation, snapshot);
        RefreshConsistency();
        return true;
    }

    public bool TryResolve(RaidCode code, out TPreparation preparation)
    {
        return TryResolveClaim(_codeClaims, code, out preparation);
    }

    public bool TryResolve(ProfileId profileId, out TPreparation preparation)
    {
        return TryResolveClaim(_profileClaims, profileId, out preparation);
    }

    public bool Rebuild(IEnumerable<KeyValuePair<TPreparation, TownRaidPreparationSnapshot>> preparations)
    {
        var rebuilt = new TownRaidPreparationDirectoryCache<TPreparation>();
        if (preparations == null)
        {
            return false;
        }

        foreach (KeyValuePair<TPreparation, TownRaidPreparationSnapshot> entry in preparations)
        {
            if (!rebuilt.RegisterOrUpdate(entry.Key, entry.Value))
            {
                return false;
            }
        }

        _snapshots.Clear();
        _codeClaims.Clear();
        _profileClaims.Clear();
        Copy(rebuilt._snapshots, _snapshots);
        CopyClaims(rebuilt._codeClaims, _codeClaims);
        CopyClaims(rebuilt._profileClaims, _profileClaims);
        IsConsistent = rebuilt.IsConsistent;
        return IsConsistent;
    }

    private void RemoveClaims(TPreparation preparation, in TownRaidPreparationSnapshot snapshot)
    {
        RemoveClaim(_codeClaims, snapshot.RaidCode, preparation);
        for (int index = 0; index < snapshot.Members.Count; index++)
        {
            RemoveClaim(_profileClaims, snapshot.Members[index].ProfileId, preparation);
        }
    }

    private void RefreshConsistency()
    {
        IsConsistent = HasOnlyUniqueClaims(_codeClaims) && HasOnlyUniqueClaims(_profileClaims);
    }

    private static bool TryResolveClaim<TKey>(
        Dictionary<TKey, HashSet<TPreparation>> claims,
        TKey key,
        out TPreparation preparation)
    {
        preparation = null;
        if (!claims.TryGetValue(key, out HashSet<TPreparation> claimants) || claimants.Count != 1)
        {
            return false;
        }

        foreach (TPreparation claimant in claimants)
        {
            preparation = claimant;
        }

        return preparation != null;
    }

    private static void AddClaim<TKey>(
        Dictionary<TKey, HashSet<TPreparation>> claims,
        TKey key,
        TPreparation preparation)
    {
        if (!claims.TryGetValue(key, out HashSet<TPreparation> claimants))
        {
            claimants = new HashSet<TPreparation>();
            claims.Add(key, claimants);
        }

        claimants.Add(preparation);
    }

    private static void RemoveClaim<TKey>(
        Dictionary<TKey, HashSet<TPreparation>> claims,
        TKey key,
        TPreparation preparation)
    {
        if (!claims.TryGetValue(key, out HashSet<TPreparation> claimants))
        {
            return;
        }

        claimants.Remove(preparation);
        if (claimants.Count == 0)
        {
            claims.Remove(key);
        }
    }

    private static bool HasOnlyUniqueClaims<TKey>(Dictionary<TKey, HashSet<TPreparation>> claims)
    {
        foreach (HashSet<TPreparation> claimants in claims.Values)
        {
            if (claimants.Count != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static void Copy<TKey, TValue>(Dictionary<TKey, TValue> source, Dictionary<TKey, TValue> destination)
    {
        foreach (KeyValuePair<TKey, TValue> entry in source)
        {
            destination.Add(entry.Key, entry.Value);
        }
    }

    private static void CopyClaims<TKey>(
        Dictionary<TKey, HashSet<TPreparation>> source,
        Dictionary<TKey, HashSet<TPreparation>> destination)
    {
        foreach (KeyValuePair<TKey, HashSet<TPreparation>> entry in source)
        {
            destination.Add(entry.Key, new HashSet<TPreparation>(entry.Value));
        }
    }
}
