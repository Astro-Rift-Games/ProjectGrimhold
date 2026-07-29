using System.Collections.Generic;

/// <summary>
/// Owns the local ordered snapshot used to issue independent full-stack loot requests.
/// It stores identities only and never mutates gameplay or networked state.
/// </summary>
public sealed class RaidLootTakeAllState
{
    private readonly List<LootId> _lootIds = new();
    private int _currentIndex;
    private bool _awaitingCompletion;

    /// <summary>Gets whether at least one captured identity remains to be finalized.</summary>
    public bool IsActive => _currentIndex < _lootIds.Count;

    /// <summary>Gets whether the current identity has an accepted request in flight.</summary>
    public bool IsAwaitingCompletion => IsActive && _awaitingCompletion;

    /// <summary>Gets the identity currently being sent or awaited.</summary>
    public LootId CurrentLootId => IsActive ? _lootIds[_currentIndex] : default;

    /// <summary>
    /// Captures the valid stacks visible when the operation begins, preserving their presentation order.
    /// </summary>
    public bool TryBegin(IReadOnlyList<LootEntry> visibleEntries)
    {
        Cancel();
        if (visibleEntries == null)
        {
            return false;
        }

        for (int i = 0; i < visibleEntries.Count; i++)
        {
            LootEntry entry = visibleEntries[i];
            if (entry.IsValid)
            {
                _lootIds.Add(entry.LootId);
            }
        }

        return IsActive;
    }

    /// <summary>Marks the current identity as having an accepted request awaiting finalization.</summary>
    public bool TryMarkRequestSent(LootId lootId)
    {
        if (!IsActive || _awaitingCompletion || CurrentLootId != lootId)
        {
            return false;
        }

        _awaitingCompletion = true;
        return true;
    }

    /// <summary>
    /// Completes or skips the current identity and advances without changing prior transfer results.
    /// </summary>
    public bool TryAdvance(LootId lootId)
    {
        if (!IsActive || CurrentLootId != lootId)
        {
            return false;
        }

        _currentIndex++;
        _awaitingCompletion = false;
        if (!IsActive)
        {
            Cancel();
        }

        return true;
    }

    /// <summary>Discards every captured identity without affecting already submitted gameplay.</summary>
    public void Cancel()
    {
        _lootIds.Clear();
        _currentIndex = 0;
        _awaitingCompletion = false;
    }
}
