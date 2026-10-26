using System;
using System.Collections.Generic;
using Grimhold.Backend;

/// <summary>Application-owned, memory-only read model. Never persisted or shared through Fusion.</summary>
public sealed class BuybackLedger
{
    private IReadOnlyList<MerchantBuybackEntry> _entries = Array.Empty<MerchantBuybackEntry>();
    public IReadOnlyList<MerchantBuybackEntry> Entries => _entries;
    public event Action Changed;

    public void Replace(BuybackData data)
    {
        var rows = new List<MerchantBuybackEntry>();
        if (data.entries != null)
            foreach (var entry in data.entries)
                rows.Add(new MerchantBuybackEntry(entry.entryId, new LootId(entry.lootId), entry.amount, entry.unitPrice));
        _entries = rows.AsReadOnly();
        Changed?.Invoke();
    }
}
