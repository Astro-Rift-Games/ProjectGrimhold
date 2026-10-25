using System.Collections.Generic;

/// <summary>Owning-client display order only; neither replication nor durable ownership.</summary>
public sealed class RaidInventoryDisplayOrder
{
    private readonly List<LootEntry> _entries = new();
    private readonly List<LootId> _order = new();
    public IReadOnlyList<LootEntry> Entries => _entries;

    public void Seed(IReadOnlyList<LootEntry> seed)
    {
        _order.Clear();
        _entries.Clear();
        for (int i = 0; seed != null && i < seed.Count; i++)
            if (seed[i].IsValid && !_order.Contains(seed[i].LootId)) _order.Add(seed[i].LootId);
    }

    public void Reconcile(IReadOnlyList<LootEntry> snapshot)
    {
        _entries.Clear();
        for (int i = 0; i < _order.Count;)
        {
            bool found = false;
            for (int j = 0; snapshot != null && j < snapshot.Count; j++)
            {
                if (snapshot[j].LootId != _order[i]) continue;
                _entries.Add(snapshot[j]);
                found = true;
                break;
            }
            if (found) i++;
            else _order.RemoveAt(i);
        }
        for (int i = 0; snapshot != null && i < snapshot.Count; i++)
        {
            LootEntry entry = snapshot[i];
            if (_order.Contains(entry.LootId)) continue;
            _order.Add(entry.LootId);
            _entries.Add(entry);
        }
    }

    public void Sort(LootDefinitionCatalog catalog)
    {
        // Refresh-time only. Amount and Origin are not comparator inputs.
        _entries.Sort((a, b) => StashDisplayOrder.Compare(
            new StashItem(a.LootId, a.Amount), new StashItem(b.LootId, b.Amount), catalog));
        _order.Clear();
        for (int i = 0; i < _entries.Count; i++) _order.Add(_entries[i].LootId);
    }
}
