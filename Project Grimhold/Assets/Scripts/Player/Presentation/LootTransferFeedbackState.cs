using System.Collections.Generic;

/// <summary>Gates confirmed incoming feedback against the destination's presented snapshot.</summary>
public sealed class LootTransferFeedbackState
{
    private readonly Dictionary<LootId, int> _presentedAmounts = new();
    private readonly Dictionary<LootId, int> _pendingBaselines = new();
    private readonly List<LootId> _ready = new();
    private LootId _requestedLootId;
    private int _requestedBaseline;

    public void CaptureRequest(LootId lootId)
    {
        _requestedLootId = lootId;
        _presentedAmounts.TryGetValue(lootId, out _requestedBaseline);
    }

    public void CompleteRequest(LootId? lootId, bool success)
    {
        if (success && lootId.HasValue && lootId.Value == _requestedLootId)
        {
            if (!_pendingBaselines.ContainsKey(_requestedLootId))
            {
                _pendingBaselines.Add(_requestedLootId, _requestedBaseline);
            }
        }
        _requestedLootId = default;
    }

    public IReadOnlyList<LootId> Observe(IReadOnlyList<LootEntry> entries)
    {
        _ready.Clear();
        _presentedAmounts.Clear();
        if (entries == null) return _ready;
        for (int i = 0; i < entries.Count; i++)
        {
            LootEntry entry = entries[i];
            if (!entry.IsValid) continue;
            _presentedAmounts[entry.LootId] = entry.Amount;
            if (_pendingBaselines.TryGetValue(entry.LootId, out int baseline) && entry.Amount > baseline)
            {
                _ready.Add(entry.LootId);
                _pendingBaselines.Remove(entry.LootId);
            }
        }
        return _ready;
    }

    public void Clear()
    {
        _presentedAmounts.Clear();
        _pendingBaselines.Clear();
        _ready.Clear();
        _requestedLootId = default;
    }
}
