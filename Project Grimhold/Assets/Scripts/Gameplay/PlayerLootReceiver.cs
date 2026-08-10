using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Owns the player's temporary loot collection for the current incursion.
/// State Authority is the only writer, while Fusion snapshots provide a consistent
/// read-only view to the owning player and other peers observing the player object.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLootReceiver : NetworkBehaviour,
    ILootReceiver,
    ILootExtractor,
    ILootContentReader,
    ILootQuantityReader,
    ILootSlotCapacityReader,
    ILootPickupFeedbackSink
{
    public const int MaxLootTypes = 64;

    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    [SerializeField, Range(1, MaxLootTypes)]
    private int _slotCapacity = 16;

    [Networked, Capacity(MaxLootTypes)]
    private NetworkDictionary<int, int> LootInventory => default;

    [Networked]
    public int LootChangeSequence { get; private set; }

    private EntityRegistry _registry;
    private ICharacter _character;
    private PlayerExtractionController _extractionController;
    private bool _isRegistered;
    private EntityId _registeredId;
    private readonly Queue<LootGrantPresentationEvent> _pendingPresentationEvents = new();

    /// <summary>
    /// Gets the loot definition catalog.
    /// </summary>
    public LootDefinitionCatalog LootCatalog => _lootCatalog;

    /// <summary>
    /// Local presentation notification emitted during Render on the receiving player's peer.
    /// </summary>
    public event Action<LootGrantPresentationEvent> LootGranted;

    /// <summary>
    /// Gets the number of distinct loot definitions currently held.
    /// </summary>
    public int DistinctLootCount => LootInventory.Count;

    /// <summary>
    /// Gets the configured gameplay capacity measured in distinct positive loot stacks.
    /// </summary>
    public int SlotCapacity => _slotCapacity;

    /// <summary>
    /// Gets the number of occupied gameplay slots from the replicated inventory.
    /// Commits preserve the invariant that every stored quantity is positive.
    /// </summary>
    public int OccupiedSlotCount => LootInventory.Count;

    public new EntityId Id
    {
        get
        {
            if (_character != null)
            {
                return _character.Id;
            }
            return default;
        }
    }

    private void Awake()
    {
        _character = GetComponent<ICharacter>();
        _extractionController = GetComponent<PlayerExtractionController>();
    }

    public override void Spawned()
    {
        if (!ValidateDependencies())
        {
            return;
        }

        if (!HasStateAuthority)
        {
            return;
        }

        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry != null)
        {
            _registeredId = Id;
            _isRegistered = _registry.TryRegisterLootReceiver(_registeredId, this);
            if (!_isRegistered)
            {
                Debug.LogError($"{nameof(PlayerLootReceiver)}: Failed to register to {nameof(EntityRegistry)} with ID {_registeredId}.", this);
            }
        }
        else
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: EntityRegistry was not found on the NetworkRunner.", this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_isRegistered && _registry != null)
        {
            _registry.TryUnregisterLootReceiver(_registeredId, this);
            _isRegistered = false;
        }

        _pendingPresentationEvents.Clear();
    }

    public override void Render()
    {
        while (_pendingPresentationEvents.Count > 0)
        {
            LootGranted?.Invoke(_pendingPresentationEvents.Dequeue());
        }
    }

    /// <summary>
    /// Validates a complete loot reception without mutating replicated state.
    /// State Authority must call <see cref="CommitReceive"/> immediately after a
    /// successful validation and without allowing an intervening state change.
    /// </summary>
    public LootTransferFailureReason ValidateReceive(in LootTransferRequest request)
    {
        if (!HasStateAuthority)
        {
            return LootTransferFailureReason.MissingAuthority;
        }

        if (IsExtractionLocked())
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        if (request.SourceId.Value == 0)
        {
            return LootTransferFailureReason.SourceNotFound;
        }

        if (request.DestinationId.Value == 0 || request.DestinationId != Id)
        {
            return LootTransferFailureReason.DestinationNotFound;
        }

        if (!request.LootId.IsValid)
        {
            return LootTransferFailureReason.InvalidLoot;
        }

        if (request.RequestedAmount <= 0)
        {
            return LootTransferFailureReason.InvalidAmount;
        }

        if (_lootCatalog == null)
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        if (!LootInventoryRules.IsValidSlotCapacity(_slotCapacity, MaxLootTypes))
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        if (!_lootCatalog.TryGetIndex(request.LootId, out int definitionIndex))
        {
            return LootTransferFailureReason.InvalidLoot;
        }

        if (definitionIndex < 0 || definitionIndex >= MaxLootTypes)
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Loot index {definitionIndex} cannot be represented by the network inventory.", this);
            return LootTransferFailureReason.ContainerUnavailable;
        }

        NetworkDictionary<int, int> inventory = LootInventory;
        bool alreadyHeld = inventory.TryGet(definitionIndex, out int currentAmount);

        LootTransferFailureReason inventoryFailure = LootInventoryRules.ValidateReceive(
            alreadyHeld,
            currentAmount,
            inventory.Count,
            _slotCapacity,
            request.RequestedAmount);

        if (inventoryFailure != LootTransferFailureReason.None)
        {
            return inventoryFailure;
        }

        if (!alreadyHeld && inventory.Count >= inventory.Capacity)
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Network inventory capacity was exhausted despite validated catalog configuration.", this);
            return LootTransferFailureReason.ContainerUnavailable;
        }

        return LootTransferFailureReason.None;
    }

    /// <summary>
    /// Commits a previously validated complete reception.
    /// An inability to apply the request indicates a caller or integration contract violation,
    /// not a gameplay rejection.
    /// </summary>
    public void CommitReceive(in LootTransferRequest request)
    {
        EnsureReceiveCommitContract(request, out int definitionIndex, out int currentAmount);

        NetworkDictionary<int, int> inventory = LootInventory;

        inventory.Set(
            definitionIndex,
            LootInventoryRules.CalculateReceivedAmount(currentAmount, request.RequestedAmount));

        LootChangeSequence++;
    }

    /// <summary>
    /// Validates a complete loot extraction without mutating replicated state.
    /// State Authority must call <see cref="CommitExtraction"/> immediately after a
    /// successful validation and without allowing an intervening state change.
    /// </summary>
    public LootTransferFailureReason ValidateExtraction(in LootTransferRequest request)
    {
        if (!HasStateAuthority)
        {
            return LootTransferFailureReason.MissingAuthority;
        }

        if (IsExtractionLocked())
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        if (request.SourceId.Value == 0 || request.SourceId != Id)
        {
            return LootTransferFailureReason.SourceNotFound;
        }

        if (request.DestinationId.Value == 0)
        {
            return LootTransferFailureReason.DestinationNotFound;
        }

        if (!request.LootId.IsValid)
        {
            return LootTransferFailureReason.InvalidLoot;
        }

        if (request.RequestedAmount <= 0)
        {
            return LootTransferFailureReason.InvalidAmount;
        }

        if (_lootCatalog == null)
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        if (!LootInventoryRules.IsValidSlotCapacity(_slotCapacity, MaxLootTypes))
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        if (!_lootCatalog.TryGetIndex(request.LootId, out int definitionIndex))
        {
            return LootTransferFailureReason.InvalidLoot;
        }

        if (definitionIndex < 0 || definitionIndex >= MaxLootTypes)
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Loot index {definitionIndex} cannot be represented by the network inventory.", this);
            return LootTransferFailureReason.ContainerUnavailable;
        }

        NetworkDictionary<int, int> inventory = LootInventory;
        bool alreadyHeld = inventory.TryGet(definitionIndex, out int currentAmount);
        return LootInventoryRules.ValidateExtraction(
            alreadyHeld,
            currentAmount,
            request.RequestedAmount);
    }

    /// <summary>
    /// Commits a previously validated complete extraction without resolving or
    /// modifying the destination endpoint.
    /// </summary>
    public void CommitExtraction(in LootTransferRequest request)
    {
        EnsureExtractionCommitContract(request, out int definitionIndex, out int currentAmount);

        NetworkDictionary<int, int> inventory = LootInventory;

        int remainingAmount = LootInventoryRules.CalculateRemainingAmount(
            currentAmount,
            request.RequestedAmount);
        if (remainingAmount == 0)
        {
            inventory.Remove(definitionIndex);
        }
        else
        {
            inventory.Set(definitionIndex, remainingAmount);
        }

        LootChangeSequence++;
    }

    /// <summary>
    /// Sends pickup-specific presentation only after a world pickup has committed successfully.
    /// Generic loot transfers never call this integration boundary.
    /// </summary>
    public void PublishPickupGrant(in LootTransferRequest request)
    {
        if (!HasStateAuthority || request.DestinationId != Id || request.SourceId.Value == 0 ||
            request.RequestedAmount <= 0 || _lootCatalog == null ||
            !_lootCatalog.TryGetIndex(request.LootId, out int definitionIndex))
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Invalid pickup feedback contract.", this);
            throw new InvalidOperationException("Pickup feedback requires a successfully committed request on State Authority.");
        }

        RPC_ReceiveLootGrant(
            LootChangeSequence,
            request.SourceId.Value,
            definitionIndex,
            request.RequestedAmount,
            request.SimulationTick);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveLootGrant(
        int sequence,
        int sourceIdValue,
        int definitionIndex,
        int amount,
        int simulationTick)
    {
        if (_lootCatalog == null || !_lootCatalog.TryGetByIndex(definitionIndex, out LootDefinition definition))
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Cannot resolve loot grant index {definitionIndex} for local presentation.", this);
            return;
        }

        _pendingPresentationEvents.Enqueue(new LootGrantPresentationEvent(
            sequence,
            new EntityId(sourceIdValue),
            Id,
            definition.LootId,
            amount,
            simulationTick));
    }

    /// <summary>
    /// Gets the aggregated amount held for a loot definition.
    /// </summary>
    public int GetLootAmount(LootId lootId)
    {
        if (_lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int definitionIndex))
        {
            return 0;
        }

        return LootInventory.TryGet(definitionIndex, out int amount) && amount > 0 ? amount : 0;
    }

    /// <summary>
    /// Creates a stable, immutable snapshot of the currently held loot.
    /// Mutating or replacing the returned entries cannot change networked state.
    /// </summary>
    public IReadOnlyList<LootEntry> GetLootContent()
    {
        if (!TryGetLootContent(out IReadOnlyList<LootEntry> entries))
        {
            throw new InvalidOperationException($"{nameof(PlayerLootReceiver)} cannot resolve the complete loot contents from its catalog.");
        }

        return entries;
    }

    /// <summary>
    /// Attempts to create a complete immutable snapshot of the held loot.
    /// No partial content is returned when catalog resolution fails.
    /// </summary>
    public bool TryGetLootContent(out IReadOnlyList<LootEntry> content)
    {
        content = Array.Empty<LootEntry>();
        if (_lootCatalog == null)
        {
            return false;
        }

        var entries = new List<LootEntry>(LootInventory.Count);
        int definitionCount = _lootCatalog.DefinitionCount;

        for (int index = 0; index < definitionCount; index++)
        {
            if (!LootInventory.TryGet(index, out int amount))
            {
                continue;
            }

            if (!_lootCatalog.TryGetByIndex(index, out LootDefinition definition))
            {
                return false;
            }

            if (amount <= 0)
            {
                return false;
            }

            entries.Add(new LootEntry(definition.LootId, amount));
        }

        if (entries.Count != LootInventory.Count)
        {
            return false;
        }

        content = entries.AsReadOnly();
        return true;
    }

    /// <summary>
    /// Checks whether the replicated inventory still exactly matches a previously
    /// captured snapshot without mutating any loot state.
    /// </summary>
    internal bool TryMatchesExactContent(IReadOnlyList<LootEntry> expected, out string error)
    {
        error = null;
        if (!TryGetLootContent(out IReadOnlyList<LootEntry> current))
        {
            error = "Current inventory cannot be resolved completely.";
            return false;
        }

        return TryCompareExactContent(expected, current, out error);
    }

    /// <summary>
    /// Clears the complete inventory only when it still exactly matches a snapshot
    /// captured earlier in the same authoritative transaction.
    /// </summary>
    internal bool TryClearExactContent(IReadOnlyList<LootEntry> expected, out string error)
    {
        error = null;
        if (!HasStateAuthority)
        {
            error = "State Authority is required.";
            return false;
        }

        if (!TryGetLootContent(out IReadOnlyList<LootEntry> current) ||
            !TryCompareExactContent(expected, current, out error))
        {
            error ??= "Current inventory cannot be resolved completely.";
            return false;
        }

        if (current.Count == 0)
        {
            return true;
        }

        LootInventory.Clear();
        LootChangeSequence++;
        return true;
    }

    /// <summary>
    /// Calculates the current economic sell value from replicated quantities and local definitions.
    /// The total is derived and is never stored as separate mutable network state.
    /// </summary>
    public long CalculateTotalValue()
    {
        if (!TryCalculateTotalValue(out long total))
        {
            throw new InvalidOperationException($"{nameof(PlayerLootReceiver)} cannot calculate a complete value from its catalog.");
        }

        return total;
    }

    /// <summary>
    /// Attempts to calculate the complete derived economic sell value.
    /// The output remains zero when any catalog entry or arithmetic operation is invalid.
    /// </summary>
    public bool TryCalculateTotalValue(out long total)
    {
        total = 0;
        if (_lootCatalog == null)
        {
            return false;
        }

        try
        {
            foreach (KeyValuePair<int, int> pair in LootInventory)
            {
                if (pair.Value <= 0 || !_lootCatalog.TryGetByIndex(pair.Key, out LootDefinition definition))
                {
                    total = 0;
                    return false;
                }

                total = checked(total + checked((long)pair.Value * definition.SellValuePerUnit));
            }
        }
        catch (OverflowException)
        {
            total = 0;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves immutable visual metadata for a loot identifier from the assigned catalog.
    /// </summary>
    public bool TryResolveDefinition(LootId lootId, out LootDefinition definition)
    {
        definition = null;
        return _lootCatalog != null && _lootCatalog.TryGet(lootId.Value, out definition);
    }

    private static bool TryCompareExactContent(
        IReadOnlyList<LootEntry> expected,
        IReadOnlyList<LootEntry> current,
        out string error)
    {
        error = null;
        if (expected == null || current == null || expected.Count != current.Count)
        {
            error = "Snapshot count differs from the current inventory.";
            return false;
        }

        var expectedIds = new HashSet<LootId>();
        for (int index = 0; index < expected.Count; index++)
        {
            LootEntry entry = expected[index];
            if (!entry.IsValid || !expectedIds.Add(entry.LootId))
            {
                error = "Snapshot contains an invalid or duplicate stack.";
                return false;
            }

            bool found = false;
            for (int currentIndex = 0; currentIndex < current.Count; currentIndex++)
            {
                if (current[currentIndex].Equals(entry))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                error = "Snapshot content differs from the current inventory.";
                return false;
            }
        }

        return true;
    }

    private bool ValidateDependencies()
    {
        if (_character == null)
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: No component implementing {nameof(ICharacter)} is found on {gameObject.name}.", this);
            return false;
        }

        if (_lootCatalog == null)
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Loot catalog is not assigned on {gameObject.name}.", this);
            return false;
        }

        if (!LootInventoryRules.IsValidSlotCapacity(_slotCapacity, MaxLootTypes))
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Slot capacity must be between 1 and {MaxLootTypes}.", this);
            return false;
        }

        if (!_lootCatalog.TryValidate(out string catalogError))
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Loot catalog is invalid. {catalogError}", this);
            return false;
        }

        if (_lootCatalog.DefinitionCount > MaxLootTypes)
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Loot catalog contains {_lootCatalog.DefinitionCount} definitions, exceeding the network representation limit of {MaxLootTypes}.", this);
            return false;
        }

        return true;
    }

    private void EnsureReceiveCommitContract(
        in LootTransferRequest request,
        out int definitionIndex,
        out int currentAmount)
    {
        definitionIndex = default;
        currentAmount = default;
        if (!HasStateAuthority || IsExtractionLocked() || request.SourceId.Value == 0 || request.DestinationId != Id ||
            request.RequestedAmount <= 0 || _lootCatalog == null ||
            !_lootCatalog.TryGetIndex(request.LootId, out definitionIndex) ||
            definitionIndex < 0 || definitionIndex >= MaxLootTypes)
        {
            FailCommitContract(nameof(CommitReceive));
        }

        NetworkDictionary<int, int> inventory = LootInventory;
        bool alreadyHeld = inventory.TryGet(definitionIndex, out currentAmount);
        if ((!alreadyHeld && (inventory.Count >= _slotCapacity || inventory.Count >= inventory.Capacity)) ||
            (alreadyHeld && currentAmount <= 0) || currentAmount > int.MaxValue - request.RequestedAmount)
        {
            FailCommitContract(nameof(CommitReceive));
        }
    }

    private void EnsureExtractionCommitContract(
        in LootTransferRequest request,
        out int definitionIndex,
        out int currentAmount)
    {
        definitionIndex = default;
        currentAmount = default;
        if (!HasStateAuthority || IsExtractionLocked() || request.SourceId != Id || request.DestinationId.Value == 0 ||
            request.RequestedAmount <= 0 || _lootCatalog == null ||
            !_lootCatalog.TryGetIndex(request.LootId, out definitionIndex) ||
            definitionIndex < 0 || definitionIndex >= MaxLootTypes ||
            !LootInventory.TryGet(definitionIndex, out currentAmount) || currentAmount < request.RequestedAmount)
        {
            FailCommitContract(nameof(CommitExtraction));
        }
    }

    private void FailCommitContract(string commitName)
    {
        Debug.LogError($"{nameof(PlayerLootReceiver)}: {commitName} contract was violated after successful validation.", this);
        throw new InvalidOperationException($"{commitName} preconditions changed after successful validation.");
    }

    /// <summary>
    /// Extraction state is the single source of truth for the inventory freeze.
    /// The extraction saver may still call <see cref="TryClearExactContent"/>
    /// to finalize its already-validated transaction.
    /// </summary>
    private bool IsExtractionLocked()
    {
        return _extractionController != null &&
            _extractionController.State == ExtractionState.Extracted;
    }

    /// <summary>
    /// Initializes the exact loadout supplied by the authoritative raid admission.
    /// Validation completes before the network dictionary is modified, so invalid
    /// input cannot leave a partially initialized inventory.
    /// </summary>
    public bool TryInitializeLoadout(IReadOnlyList<LootEntry> loadoutItems, out string error)
    {
        error = null;
        if (!HasStateAuthority)
        {
            error = "Loadout initialization requires State Authority.";
            return false;
        }

        if (!RaidLoadoutRules.TryValidate(loadoutItems, _lootCatalog, _slotCapacity, out error))
        {
            return false;
        }

        NetworkDictionary<int, int> inventory = LootInventory;
        if (inventory.Count != 0)
        {
            if (TryMatchesInitialLoadout(inventory, loadoutItems))
            {
                return true;
            }

            error = "Player loot inventory was already initialized with different content.";
            return false;
        }

        for (int index = 0; index < loadoutItems.Count; index++)
        {
            LootEntry entry = loadoutItems[index];
            if (!_lootCatalog.TryGetIndex(entry.LootId, out int definitionIndex))
            {
                error = $"Loot catalog does not contain '{entry.LootId.Value}'.";
                return false;
            }

            inventory.Set(definitionIndex, entry.Amount);
        }

        if (loadoutItems.Count > 0)
        {
            LootChangeSequence++;
        }

        return true;
    }

    private bool TryMatchesInitialLoadout(
        NetworkDictionary<int, int> inventory,
        IReadOnlyList<LootEntry> expected)
    {
        if (inventory.Count != expected.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.Count; index++)
        {
            if (!_lootCatalog.TryGetIndex(expected[index].LootId, out int definitionIndex) ||
                !inventory.TryGet(definitionIndex, out int amount) ||
                amount != expected[index].Amount)
            {
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_lootCatalog != null)
        {
            return;
        }

        string[] catalogGuids = UnityEditor.AssetDatabase.FindAssets("t:LootDefinitionCatalog");
        if (catalogGuids.Length != 1)
        {
            return;
        }

        string catalogPath = UnityEditor.AssetDatabase.GUIDToAssetPath(catalogGuids[0]);
        _lootCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(catalogPath);
    }
#endif
}
