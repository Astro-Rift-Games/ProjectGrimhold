using System.Collections.Generic;

public enum TownPartyContinuationClaimResult
{
    Rejected = 0,
    Pending = 1,
    ReadyToRestore = 2,
    AlreadyRestored = 3
}

/// <summary>
/// Pure authority-side aggregation for matching continuation claims. A withdrawn claim is a
/// tombstone for that exact Party descriptor and profile and cannot contribute later.
/// </summary>
public sealed class TownPartyContinuationClaimRegistry
{
    private sealed class Entry
    {
        public TownPartyContinuationContext Context;
        public readonly HashSet<ProfileId> Claimants = new();
        public readonly HashSet<ProfileId> Withdrawn = new();
        public bool Restored;
    }

    private readonly Dictionary<string, Entry> _entries = new();

    public TownPartyContinuationClaimResult Submit(
        ProfileId claimant,
        TownPartyContinuationContext context)
    {
        if (!claimant.IsValid || context == null || !context.Contains(claimant))
        {
            return TownPartyContinuationClaimResult.Rejected;
        }

        string key = GetKey(context);
        if (!_entries.TryGetValue(key, out Entry entry))
        {
            entry = new Entry { Context = context };
            _entries.Add(key, entry);
        }

        if (!entry.Context.Equals(context) || entry.Withdrawn.Contains(claimant))
        {
            return TownPartyContinuationClaimResult.Rejected;
        }

        if (entry.Restored)
        {
            return TownPartyContinuationClaimResult.AlreadyRestored;
        }

        entry.Claimants.Add(claimant);
        return HasEveryClaim(entry)
            ? TownPartyContinuationClaimResult.ReadyToRestore
            : TownPartyContinuationClaimResult.Pending;
    }

    public bool Withdraw(ProfileId claimant, TownPartyContinuationContext context)
    {
        if (!claimant.IsValid || context == null || !context.Contains(claimant))
        {
            return false;
        }

        string key = GetKey(context);
        if (!_entries.TryGetValue(key, out Entry entry))
        {
            entry = new Entry { Context = context };
            _entries.Add(key, entry);
        }

        if (!entry.Context.Equals(context))
        {
            return false;
        }

        entry.Claimants.Remove(claimant);
        entry.Withdrawn.Add(claimant);
        return true;
    }

    public bool MarkRestored(TownPartyContinuationContext context)
    {
        if (context == null || !_entries.TryGetValue(GetKey(context), out Entry entry) ||
            !entry.Context.Equals(context) || !HasEveryClaim(entry) || entry.Withdrawn.Count > 0)
        {
            return false;
        }

        entry.Restored = true;
        return true;
    }

    public void Clear() => _entries.Clear();

    private static bool HasEveryClaim(Entry entry)
    {
        if (entry.Withdrawn.Count > 0 || entry.Claimants.Count != entry.Context.Members.Count)
        {
            return false;
        }

        for (int index = 0; index < entry.Context.Members.Count; index++)
        {
            if (!entry.Claimants.Contains(entry.Context.Members[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetKey(TownPartyContinuationContext context)
    {
        string key = context.HostProfileId.Value;
        for (int index = 0; index < context.Members.Count; index++) key += $"|{context.Members[index].Value}";
        return key;
    }
}
