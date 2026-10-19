using System;

/// <summary>
/// Local application-level view of the player's Quick Slots for one inventory the player is
/// looking at (the Town Loadout or the Raid inventory). It validates assignments against that
/// inventory, persists them through <see cref="LocalProfileStore"/> and releases a binding when
/// its last available unit is observed leaving the inventory.
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
    /// Whether a unit count dropping to zero may unassign the slot right now. It must be false
    /// while the inventory is emptied for another reason (reservation, extraction, defeat), so
    /// those transitions never erase bindings.
    /// </param>
    public QuickSlotController(
        LocalProfileStore store,
        IInventoryReadSource inventory,
        Func<bool> canReleaseDepleted)
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
        definition.QuickSlotCompatible;

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
    /// A slot whose binding just changed starts from the units available now, so only a later
    /// positive-to-zero transition releases it. Unchanged slots keep their observed baseline.
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
        bool mayRelease = _canReleaseDepleted != null && _canReleaseDepleted();
        bool released = mayRelease &&
            QuickSlotAssignmentRules.TryReleaseDepleted(
                Bindings, _previousAmounts, _currentAmounts, out QuickSlotBindings next) &&
            _store.TrySetQuickSlots(next) == StashOperationResult.Success;

        // Amounts are tracked even while releasing is not allowed, so a unit count that was
        // already zero when releasing becomes allowed again is never treated as a new depletion.
        Array.Copy(_currentAmounts, _previousAmounts, _previousAmounts.Length);

        // A release commits through the store and already raised Changed.
        if (!released)
        {
            Changed?.Invoke();
        }
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
