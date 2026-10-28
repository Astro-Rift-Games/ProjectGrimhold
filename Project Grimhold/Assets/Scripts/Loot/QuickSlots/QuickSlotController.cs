using System;

/// <summary>
/// Local application-level view of the player's Quick Slots for one inventory the player is
/// looking at (the Town Loadout or the Raid inventory). It validates assignments against that
/// inventory and persists them through <see cref="LocalProfileStore"/>.
/// Under Doc 14 §12.2 (TASK-230), slots reaching 0 units remain assigned in an inactive state.
/// Bindings only reference loot definitions, so nothing here moves or changes items.
/// </summary>
public sealed class QuickSlotController : IDisposable
{
    private readonly LocalProfileStore _store;
    private readonly IInventoryReadSource _inventory;
    private readonly Func<bool> _canReleaseDepleted;
    private readonly int[] _previousAmounts = new int[QuickSlotBindings.SlotCount];
    private readonly int[] _currentAmounts = new int[QuickSlotBindings.SlotCount];
    private QuickSlotBindings _seenBindings;
    private bool _disposed;

    /// <summary>Raised when the bindings or the amounts they resolve to may have changed.</summary>
    public event Action Changed;

    /// <param name="canReleaseDepleted">
    /// Legacy gate predicate kept for compatibility. Releasing on depletion is deprecated under TASK-230.
    /// </param>
    public QuickSlotController(
        LocalProfileStore store,
        IInventoryReadSource inventory,
        Func<bool> canReleaseDepleted = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _canReleaseDepleted = canReleaseDepleted;
        _seenBindings = Bindings;
        ReadSlotAmounts(_previousAmounts);
        _store.ProfileCommitted += OnProfileCommitted;
        _inventory.Changed += OnInventoryChanged;
    }

    public QuickSlotBindings Bindings => _store.GetQuickSlots();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _store.ProfileCommitted -= OnProfileCommitted;
        _inventory.Changed -= OnInventoryChanged;
    }

    /// <summary>Total units of the definition in the observed inventory, across every stack.</summary>
    public int GetAvailableAmount(LootId lootId)
    {
        if (!lootId.IsValid || !_inventory.TryGetLootContent(out var content) || content == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < content.Count; i++)
        {
            if (content[i].LootId.Equals(lootId))
            {
                total += content[i].Amount;
            }
        }

        return total;
    }

    public bool IsCompatible(LootId lootId) =>
        lootId.IsValid && _store.LootCatalog != null &&
        _store.LootCatalog.TryGet(lootId.Value, out LootDefinition definition) &&
        definition.QuickSlotCompatible &&
        (definition.Category == LootCategory.Consumable || definition.Category == LootCategory.Utility);

    public QuickSlotAssignmentResult TryAssign(int slotIndex, LootId lootId)
    {
        QuickSlotAssignmentResult result = QuickSlotAssignmentRules.TryAssign(
            Bindings,
            slotIndex,
            lootId,
            IsCompatible(lootId),
            GetAvailableAmount(lootId),
            out QuickSlotBindings next);
        return result == QuickSlotAssignmentResult.Success ? Commit(next) : result;
    }

    public QuickSlotAssignmentResult TryClear(int slotIndex)
    {
        QuickSlotAssignmentResult result = QuickSlotAssignmentRules.TryClear(
            Bindings, slotIndex, out QuickSlotBindings next);
        return result == QuickSlotAssignmentResult.Success ? Commit(next) : result;
    }

    private QuickSlotAssignmentResult Commit(QuickSlotBindings next) =>
        _store.TrySetQuickSlots(next) == StashOperationResult.Success
            ? QuickSlotAssignmentResult.Success
            : QuickSlotAssignmentResult.InvalidItem;

    private void OnProfileCommitted(ProfileId profileId)
    {
        if (profileId == _store.ProfileId)
        {
            BaselineChangedBindings();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// A slot whose binding just changed starts from the units available now.
    /// Unchanged slots keep their observed baseline.
    /// </summary>
    private void BaselineChangedBindings()
    {
        QuickSlotBindings bindings = Bindings;
        for (int i = 0; i < QuickSlotBindings.SlotCount; i++)
        {
            if (!bindings.Get(i).Equals(_seenBindings.Get(i)))
            {
                _previousAmounts[i] = bindings.IsAssigned(i) ? GetAvailableAmount(bindings.Get(i)) : 0;
            }
        }

        _seenBindings = bindings;
    }

    private void OnInventoryChanged()
    {
        BaselineChangedBindings();
        ReadSlotAmounts(_currentAmounts);

        // Depleted slots are kept bound with count 0 per Doc 14 §12.2 (TASK-230).
        Array.Copy(_currentAmounts, _previousAmounts, _previousAmounts.Length);

        Changed?.Invoke();
    }

    private void ReadSlotAmounts(int[] amounts)
    {
        QuickSlotBindings bindings = Bindings;
        for (int i = 0; i < QuickSlotBindings.SlotCount; i++)
        {
            amounts[i] = bindings.IsAssigned(i) ? GetAvailableAmount(bindings.Get(i)) : 0;
        }
    }
}
