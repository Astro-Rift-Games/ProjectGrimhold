using System.Collections.Generic;

/// <summary>
/// Owns the local ordered snapshot used to issue independent full-stack loot requests.
/// It stores identities only and never mutates gameplay or networked state.
/// </summary>
public sealed class RaidLootTakeAllState
{
    private readonly List<LootId> _lootIds = new();
    // Parallel to _lootIds: a container weapon unit address and its observed modifiers, or -1.
    private readonly List<int> _unitIndices = new();
    private readonly List<WeaponInstanceModifiersNetworkData> _unitModifiers = new();
    private int _currentIndex;
    private bool _awaitingCompletion;

    /// <summary>Gets whether at least one captured identity remains to be finalized.</summary>
    public bool IsActive => _currentIndex < _lootIds.Count;

    /// <summary>Gets whether the current identity has an accepted request in flight.</summary>
    public bool IsAwaitingCompletion => IsActive && _awaitingCompletion;

    /// <summary>Gets the identity currently being sent or awaited.</summary>
    public LootId CurrentLootId => IsActive ? _lootIds[_currentIndex] : default;
    public int CurrentUnitIndex => IsActive ? _unitIndices[_currentIndex] : -1;
    public WeaponInstanceModifiersNetworkData CurrentUnitModifiers =>
        IsActive ? _unitModifiers[_currentIndex] : default;
    public bool CurrentHasUnitAddress => CurrentUnitIndex != -1;

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
                Add(entry.LootId, -1, default);
            }
        }

        return IsActive;
    }

    /// <summary>
    /// Captures the visible cells in presentation order: one step per stack identity and one per
    /// addressed weapon unit, so every unit moves alone with its own modifiers.
    /// </summary>
    public bool TryBegin(IReadOnlyList<RaidInventorySlotData> visibleCells)
    {
        Cancel();
        if (visibleCells == null)
        {
            return false;
        }

        for (int i = 0; i < visibleCells.Count; i++)
        {
            RaidInventorySlotData cell = visibleCells[i];
            if (!cell.IsOccupied || !cell.LootId.IsValid) continue;
            if (cell.HasSourceUnitAddress) Add(cell.LootId, cell.SourceUnitIndex, cell.SourceUnitModifiers);
            else if (!_lootIds.Contains(cell.LootId)) Add(cell.LootId, -1, default);
        }

        return IsActive;
    }

    private void Add(LootId lootId, int unitIndex, WeaponInstanceModifiersNetworkData modifiers)
    {
        _lootIds.Add(lootId);
        _unitIndices.Add(unitIndex);
        _unitModifiers.Add(modifiers);
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
        _unitIndices.Clear();
        _unitModifiers.Clear();
        _currentIndex = 0;
        _awaitingCompletion = false;
    }
}
