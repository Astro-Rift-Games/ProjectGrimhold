using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Owns the player's temporary physical inventory. Quantity and Raid origin live together
/// in each replicated slot; aggregate snapshots are detached projections, never a second store.
/// State Authority is the only writer. Proxies may read slots and aggregate quantities.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLootReceiver : NetworkBehaviour,
    IInventoryReadSource, ILootReceiver, ILootExtractor, ILootContentReader,
    ILootQuantityReader, ILootSlotCapacityReader, ILootPickupFeedbackSink,
    IRaidLootOriginSource, IRaidLootOriginReceiver
{
    public const int MaxCatalogEntries = RaidLootOriginPackedBuffer.MaximumCatalogEntries;
    public const int MaxDistinctLootTypes = LootInventoryRules.PhysicalSlotCount;
    public const int PhysicalSlotCount = LootInventoryRules.PhysicalSlotCount;
    private const int OriginCount = RaidLootOriginPackedBuffer.MaximumPlayerOrigins + 1;

    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    [SerializeField, Range(1, PhysicalSlotCount)] private int _slotCapacity = PhysicalSlotCount;

    [Networked, Capacity(PhysicalSlotCount)]
    private NetworkArray<LootInventorySlot> LootInventory => default;

    [Networked] public int LootChangeSequence { get; private set; }

    private EntityRegistry _registry;
    private ICharacter _character;
    private PlayerExtractionController _extractionController;
    private PlayerRaidLootOriginState _raidOriginState;
    private bool _isRegistered;
    private EntityId _registeredId;
    private readonly Queue<LootGrantPresentationEvent> _pendingPresentationEvents = new();
    private int _lastNotifiedLootRevision;

    public LootDefinitionCatalog LootCatalog => _lootCatalog;
    public event Action<LootGrantPresentationEvent> LootGranted;
    public event Action Changed;
    public int Revision => LootChangeSequence;
    public bool IsRaidLootOriginAware => _raidOriginState != null;

    /// <summary>
    /// Production capacity is 30. A lower serialized/test limit restricts occupied slots,
    /// not addresses or storage length. Lowering it never compacts or discards existing state.
    /// </summary>
    public int SlotCapacity => _slotCapacity;
    public int OccupiedSlotCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < PhysicalSlotCount; index++)
                if (!LootInventory[index].IsEmpty) count++;
            return count;
        }
    }

    public int DistinctLootCount
    {
        get
        {
            // Seven-bit catalog indices; no allocation or secondary mutable count.
            Span<bool> seen = stackalloc bool[MaxCatalogEntries];
            seen.Clear();
            int count = 0;
            for (int index = 0; index < PhysicalSlotCount; index++)
            {
                LootInventorySlot slot = LootInventory[index];
                if (!slot.IsValid || seen[slot.CatalogIndex]) continue;
                seen[slot.CatalogIndex] = true;
                count++;
            }
            return count;
        }
    }

    public new EntityId Id => _character != null ? _character.Id : default;

    /// <summary>Reads one stable physical address, including an empty slot, without authority.</summary>
    public bool TryGetInventorySlot(int slotIndex, out LootInventorySlot slot)
    {
        slot = default;
        if (slotIndex < 0 || slotIndex >= PhysicalSlotCount) return false;
        slot = LootInventory[slotIndex];
        return slot.IsEmpty || slot.IsValid;
    }

    private void Awake()
    {
        _character = GetComponent<ICharacter>();
        _extractionController = GetComponent<PlayerExtractionController>();
        _raidOriginState = GetComponent<PlayerRaidLootOriginState>();
    }

    public override void Spawned()
    {
        // Never initialize or repack slots here: CopyStateFrom owns restore storage.
        _lastNotifiedLootRevision = LootChangeSequence;
        if (!ValidateDependencies() || !HasStateAuthority) return;
        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: EntityRegistry was not found on the NetworkRunner.", this);
            return;
        }
        _registeredId = Id;
        _isRegistered = _registry.TryRegisterLootReceiver(_registeredId, this);
        if (!_isRegistered)
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Failed to register to {nameof(EntityRegistry)} with ID {_registeredId}.", this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_isRegistered && _registry != null)
            _registry.TryUnregisterLootReceiver(_registeredId, this);
        _isRegistered = false;
        _pendingPresentationEvents.Clear();
    }

    public override void Render()
    {
        if (_lastNotifiedLootRevision != LootChangeSequence)
        {
            _lastNotifiedLootRevision = LootChangeSequence;
            Changed?.Invoke();
        }
        // Dequeue before the null-conditional invoke: without a subscriber, `?.` would skip
        // evaluating the argument and this loop would never drain.
        while (_pendingPresentationEvents.Count > 0)
        {
            LootGrantPresentationEvent grantEvent = _pendingPresentationEvents.Dequeue();
            LootGranted?.Invoke(grantEvent);
        }
    }

    public LootTransferFailureReason ValidateReceive(in LootTransferRequest request)
    {
        LootTransferFailureReason failure = ValidateRequest(request, receiving: true, out int catalogIndex);
        if (failure != LootTransferFailureReason.None) return failure;
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (!TryReadSlots(slots)) return LootTransferFailureReason.ContainerUnavailable;
        if (!_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition) ||
            definition.StackMax <= 0 || definition.MaxTransportable < 0)
            return LootTransferFailureReason.ContainerUnavailable;
        if (!TryGetTotal(slots, catalogIndex, out int current)) return LootTransferFailureReason.Overflow;
        if (current > int.MaxValue - request.RequestedAmount) return LootTransferFailureReason.Overflow;
        if (definition.MaxTransportable > 0 && request.RequestedAmount > definition.MaxTransportable - current)
            return LootTransferFailureReason.MaxTransportableExceeded;
        if (!IsRaidLootOriginAware)
            return LootInventoryRules.ValidateReceive(slots, catalogIndex, RaidLootOrigin.Dungeon,
                definition.StackMax, definition.MaxTransportable, request.RequestedAmount, _slotCapacity);

        // The ordinary request has no origin. This necessary upper bound must not reject a
        // full inventory's compatible Player partial stack by assuming Dungeon. Exact joint
        // capacity (including competition for holes) is checked with the resolved payload below.
        int occupied = 0;
        long capacity = 0;
        foreach (LootInventorySlot slot in slots)
        {
            if (slot.IsEmpty) continue;
            occupied++;
            if (slot.CatalogIndex == catalogIndex) capacity += definition.StackMax - slot.Quantity;
        }
        capacity += (long)Math.Max(0, _slotCapacity - occupied) * definition.StackMax;
        return request.RequestedAmount > capacity ? LootTransferFailureReason.InventoryFull : LootTransferFailureReason.None;
    }

    /// <summary>Builds an exact-address request without substituting another stack or revision.</summary>
    public LootTransferFailureReason ResolveInventorySlotExtraction(EntityId destinationId, LootId lootId,
        LootTransferQuantityMode quantityMode, int sourceInventorySlotIndex, int expectedInventoryRevision,
        int simulationTick, out LootTransferRequest request)
    {
        request = default;
        if (!LootTransferRequest.IsValidInventorySlotAddress(sourceInventorySlotIndex, expectedInventoryRevision))
            return LootTransferFailureReason.InvalidLoot;
        var unit = new LootTransferRequest(Id, destinationId, lootId, 1, simulationTick,
            sourceInventorySlotIndex, expectedInventoryRevision);
        LootTransferFailureReason failure = ValidateExtraction(unit);
        if (failure != LootTransferFailureReason.None) return failure;
        failure = LootTransferQuantityResolver.Resolve(quantityMode,
            LootInventory[sourceInventorySlotIndex].Quantity, out int amount);
        if (failure != LootTransferFailureReason.None) return failure;
        request = new LootTransferRequest(Id, destinationId, lootId, amount, simulationTick,
            sourceInventorySlotIndex, expectedInventoryRevision);
        return LootTransferFailureReason.None;
    }

    public LootTransferFailureReason ValidateExtraction(in LootTransferRequest request)
    {
        LootTransferFailureReason failure = ValidateRequest(request, receiving: false, out int catalogIndex);
        if (failure != LootTransferFailureReason.None) return failure;
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (!TryReadSlots(slots)) return LootTransferFailureReason.ContainerUnavailable;
        if (request.HasInventorySlotAddress) return ValidateAddressedSlot(slots, catalogIndex, request);
        if (!TryGetTotal(slots, catalogIndex, out int amount)) return LootTransferFailureReason.Overflow;
        return amount < request.RequestedAmount ? LootTransferFailureReason.InsufficientAmount : LootTransferFailureReason.None;
    }

    public void CommitReceive(in LootTransferRequest request)
    {
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (IsRaidLootOriginAware || ValidateReceive(request) != LootTransferFailureReason.None ||
            !TryReadSlots(slots) || !_lootCatalog.TryGetIndex(request.LootId, out int catalogIndex) ||
            TryReceiveOrigin(slots, catalogIndex, RaidLootOrigin.Dungeon, request.RequestedAmount) != LootTransferFailureReason.None)
        {
            FailCommitContract(nameof(CommitReceive));
            return;
        }
        WriteSlots(slots);
    }

    public void CommitExtraction(in LootTransferRequest request)
    {
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (IsRaidLootOriginAware || ValidateExtraction(request) != LootTransferFailureReason.None ||
            !TryReadSlots(slots) || !_lootCatalog.TryGetIndex(request.LootId, out int catalogIndex) ||
            !TryExtractInventoryRequest(slots, catalogIndex, request, null))
        {
            FailCommitContract(nameof(CommitExtraction));
            return;
        }
        WriteSlots(slots);
    }

    public LootTransferFailureReason ValidateRaidLootOriginReceive(in LootTransferRequest request, RaidLootOriginTransfer transfer)
    {
        LootTransferFailureReason failure = ValidateReceive(request);
        if (failure != LootTransferFailureReason.None) return failure;
        if (!IsRaidLootOriginAware || !IsExactTransfer(transfer, request.RequestedAmount))
            return LootTransferFailureReason.ContainerUnavailable;
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (!TryReadSlots(slots) || !_lootCatalog.TryGetIndex(request.LootId, out int catalogIndex))
            return LootTransferFailureReason.ContainerUnavailable;
        return TryReceiveTransfer(slots, catalogIndex, transfer);
    }

    public void CommitRaidLootReceive(in LootTransferRequest request, RaidLootOriginTransfer transfer)
    {
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (ValidateRaidLootOriginReceive(request, transfer) != LootTransferFailureReason.None ||
            !TryReadSlots(slots) || !_lootCatalog.TryGetIndex(request.LootId, out int catalogIndex) ||
            TryReceiveTransfer(slots, catalogIndex, transfer) != LootTransferFailureReason.None)
        {
            FailCommitContract(nameof(CommitRaidLootReceive));
            return;
        }
        WriteSlots(slots);
    }

    public void CommitRaidLootExtraction(in LootTransferRequest request, RaidLootOriginTransfer transfer)
    {
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (!IsRaidLootOriginAware || ValidateExtraction(request) != LootTransferFailureReason.None ||
            !IsExactTransfer(transfer, request.RequestedAmount) || !TryReadSlots(slots) ||
            !_lootCatalog.TryGetIndex(request.LootId, out int catalogIndex) ||
            !TryExtractInventoryRequest(slots, catalogIndex, request, transfer))
        {
            FailCommitContract(nameof(CommitRaidLootExtraction));
            return;
        }
        WriteSlots(slots);
    }

    public bool TryResolveRaidLootOriginTransfer(in LootTransferRequest request, out RaidLootOriginTransfer transfer) =>
        TryResolveRaidLootOriginTransfer(request, RaidLootOriginSelectionOrder.Canonical, out transfer);

    public bool TryResolveRaidLootOriginTransfer(in LootTransferRequest request,
        RaidLootOriginSelectionOrder selectionOrder, out RaidLootOriginTransfer transfer)
    {
        transfer = null;
        if (!IsRaidLootOriginAware || request.SourceId != Id || _lootCatalog == null ||
            (selectionOrder != RaidLootOriginSelectionOrder.Canonical && selectionOrder != RaidLootOriginSelectionOrder.PlayerFirst) ||
            !LootTransferRequest.IsValidInventorySource(request.SourceInventorySlotIndex, request.ExpectedInventoryRevision) ||
            !_lootCatalog.TryGetIndex(request.LootId, out int catalogIndex)) return false;
        if (request.HasInventorySlotAddress)
        {
            Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
            if (!request.IsValid || request.ExpectedInventoryRevision != Revision || !TryReadSlots(slots) ||
                ValidateAddressedSlot(slots, catalogIndex, request) != LootTransferFailureReason.None) return false;
            return RaidLootOriginTransfer.TryCreate(slots[request.SourceInventorySlotIndex].Origin,
                request.RequestedAmount, out transfer);
        }
        return TryResolveInventoryTransfer(catalogIndex, request.RequestedAmount, selectionOrder, out transfer);
    }

    internal bool TryResolveInventoryTransfer(int catalogIndex, int amount,
        RaidLootOriginSelectionOrder selectionOrder, out RaidLootOriginTransfer transfer)
    {
        transfer = null;
        if (amount <= 0 || (selectionOrder != RaidLootOriginSelectionOrder.Canonical &&
            selectionOrder != RaidLootOriginSelectionOrder.PlayerFirst)) return false;
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        Span<int> quantities = stackalloc int[OriginCount];
        quantities.Clear();
        if (!TryReadSlots(slots) || !TryGetTotal(slots, catalogIndex, out int total) || total < amount) return false;
        foreach (LootInventorySlot slot in slots)
            if (slot.IsValid && slot.CatalogIndex == catalogIndex) quantities[OriginIndex(slot.Origin)] += slot.Quantity;
        Span<int> selected = stackalloc int[OriginCount];
        selected.Clear();
        int remaining = amount;
        int count = 0;
        for (int step = 0; step < OriginCount && remaining > 0; step++)
        {
            int originIndex = selectionOrder == RaidLootOriginSelectionOrder.PlayerFirst
                ? (step + 1) % OriginCount : step;
            int taken = Math.Min(remaining, quantities[originIndex]);
            selected[originIndex] = taken;
            remaining -= taken;
            if (taken > 0) count++;
        }
        var buckets = new RaidLootOriginBucket[count];
        int destination = 0;
        for (int index = 0; index < OriginCount; index++)
            if (selected[index] > 0) buckets[destination++] = new RaidLootOriginBucket(OriginAt(index), selected[index]);
        transfer = new RaidLootOriginTransfer(buckets);
        return true;
    }

    /// <summary>
    /// Preflights the complete equipment exchange on stack-local storage. Container equip
    /// passes no outgoing Inventory item; addressed equip removes only its selected physical unit.
    /// This writes nothing, including on a later displaced-item failure.
    /// </summary>
    internal bool CanApplyInventoryExchange(LootId? outgoingLootId, RaidLootOriginTransfer outgoing,
        IReadOnlyList<LootEntry?> incoming, IReadOnlyList<RaidLootOrigin> origins)
    {
        if (!HasStateAuthority) return false;
        LootTransferRequest? request = null;
        if (outgoingLootId.HasValue)
        {
            if (!TryGetTransferTotal(outgoing, out int amount)) return false;
            request = new LootTransferRequest(Id, Id, outgoingLootId.Value, amount, 0);
        }
        return CanApplyInventoryExchangeCore(request, outgoing, incoming, origins);
    }

    internal bool CanApplyInventoryExchange(in LootTransferRequest outgoing, RaidLootOriginTransfer transfer,
        IReadOnlyList<LootEntry?> incoming, IReadOnlyList<RaidLootOrigin> origins) =>
        CanApplyInventoryExchangeCore(outgoing, transfer, incoming, origins);

    private bool CanApplyInventoryExchangeCore(LootTransferRequest? outgoing, RaidLootOriginTransfer transfer,
        IReadOnlyList<LootEntry?> incoming, IReadOnlyList<RaidLootOrigin> origins)
    {
        if (!HasStateAuthority || IsExtractionLocked() || !IsRaidLootOriginAware || incoming == null ||
            origins == null || incoming.Count != origins.Count) return false;
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (!TryReadSlots(slots)) return false;
        if (outgoing.HasValue)
        {
            LootTransferRequest request = outgoing.Value;
            if (ValidateExtraction(request) != LootTransferFailureReason.None ||
                !IsExactTransfer(transfer, request.RequestedAmount) ||
                !_lootCatalog.TryGetIndex(request.LootId, out int catalogIndex) ||
                !TryExtractInventoryRequest(slots, catalogIndex, request, transfer)) return false;
        }
        for (int index = 0; index < incoming.Count; index++)
        {
            if (!incoming[index].HasValue) continue;
            LootEntry entry = incoming[index].Value;
            if (!entry.IsValid || !_lootCatalog.TryGetIndex(entry.LootId, out int incomingIndex) ||
                TryReceiveOrigin(slots, incomingIndex, origins[index], entry.Amount) != LootTransferFailureReason.None)
                return false;
        }
        return true;
    }

    public int GetLootAmount(LootId lootId)
    {
        if (_lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int catalogIndex)) return 0;
        long total = 0;
        for (int index = 0; index < PhysicalSlotCount; index++)
        {
            LootInventorySlot slot = LootInventory[index];
            if (slot.IsValid && slot.CatalogIndex == catalogIndex) total += slot.Quantity;
        }
        return total <= int.MaxValue ? (int)total : 0;
    }

    public IReadOnlyList<LootEntry> GetLootContent()
    {
        if (!TryGetLootContent(out IReadOnlyList<LootEntry> entries))
            throw new InvalidOperationException($"{nameof(PlayerLootReceiver)} cannot resolve the complete loot contents from its catalog.");
        return entries;
    }

    public bool TryGetLootContent(out IReadOnlyList<LootEntry> content)
    {
        content = Array.Empty<LootEntry>();
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (!TryReadSlots(slots)) return false;
        var entries = new List<LootEntry>();
        for (int index = 0; index < _lootCatalog.DefinitionCount; index++)
        {
            if (!TryGetTotal(slots, index, out int amount)) return false;
            if (amount == 0) continue;
            if (!_lootCatalog.TryGetByIndex(index, out LootDefinition definition)) return false;
            entries.Add(new LootEntry(definition.LootId, amount));
        }
        content = entries.AsReadOnly();
        return true;
    }

    internal bool TryGetRaidLootOriginEntries(out IReadOnlyList<RaidLootOriginEntry> entries)
    {
        entries = Array.Empty<RaidLootOriginEntry>();
        Span<LootInventorySlot> slots = stackalloc LootInventorySlot[PhysicalSlotCount];
        if (!IsRaidLootOriginAware || !TryReadSlots(slots)) return false;
        var result = new List<RaidLootOriginEntry>();
        Span<int> quantities = stackalloc int[OriginCount];
        for (int catalogIndex = 0; catalogIndex < _lootCatalog.DefinitionCount; catalogIndex++)
        {
            quantities.Clear();
            if (!TryGetTotal(slots, catalogIndex, out _)) return false;
            foreach (LootInventorySlot slot in slots)
                if (slot.IsValid && slot.CatalogIndex == catalogIndex) quantities[OriginIndex(slot.Origin)] += slot.Quantity;
            for (int originIndex = 0; originIndex < OriginCount; originIndex++)
            {
                if (quantities[originIndex] == 0) continue;
                if (!_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition)) return false;
                result.Add(new RaidLootOriginEntry(definition.LootId, OriginAt(originIndex), quantities[originIndex]));
            }
        }
        entries = result.AsReadOnly();
        return true;
    }

    internal bool TryGetCatalogIndex(LootId lootId, out int catalogIndex)
    {
        catalogIndex = -1;
        return _lootCatalog != null && _lootCatalog.TryGetIndex(lootId, out catalogIndex);
    }

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

    internal bool TryMatchesExactRaidContent(IReadOnlyList<LootEntry> expectedLoot,
        IReadOnlyList<RaidLootOriginEntry> expectedOrigins, out string error)
    {
        if (!TryMatchesExactContent(expectedLoot, out error) ||
            !TryGetRaidLootOriginEntries(out IReadOnlyList<RaidLootOriginEntry> currentOrigins) ||
            !RaidOriginEntriesEqual(expectedOrigins, currentOrigins))
        {
            error ??= "Raid inventory provenance differs from the expected snapshot.";
            return false;
        }
        return true;
    }

    internal bool TryClearExactContent(IReadOnlyList<LootEntry> expected, out string error)
    {
        error = null;
        if (!HasStateAuthority || IsRaidLootOriginAware)
        {
            error = "State Authority and a non-Raid inventory are required for quantity-only clear.";
            return false;
        }
        if (!TryMatchesExactContent(expected, out error)) return false;
        ClearSlots();
        return true;
    }

    internal bool TryClearExactRaidContent(IReadOnlyList<LootEntry> expectedLoot,
        IReadOnlyList<RaidLootOriginEntry> expectedOrigins, out string error)
    {
        error = null;
        if (!HasStateAuthority || !IsRaidLootOriginAware)
        {
            error = "Raid inventory clear requires State Authority and Raid origin composition.";
            return false;
        }
        if (!TryMatchesExactRaidContent(expectedLoot, expectedOrigins, out error)) return false;
        ClearSlots();
        return true;
    }

    public bool TryInitializeLoadout(IReadOnlyList<LootEntry> loadoutItems, out string error) =>
        TrySetLoadout(loadoutItems, RaidLootOrigin.Dungeon, raid: false, replace: false, out error);

    public bool TryInitializeRaidLoadout(IReadOnlyList<LootEntry> loadoutItems,
        RaidParticipantId participantId, out string error)
    {
        if (!RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin origin))
        {
            error = "Raid loadout requires a valid admitted RaidParticipantId.";
            return false;
        }
        return TrySetLoadout(loadoutItems, origin, raid: true, replace: false, out error);
    }

    public bool TryForceSyncLoadout(IReadOnlyList<LootEntry> loadoutItems, out string error) =>
        TrySetLoadout(loadoutItems, RaidLootOrigin.Dungeon, raid: false, replace: true, out error);

    private bool TrySetLoadout(IReadOnlyList<LootEntry> entries, RaidLootOrigin origin,
        bool raid, bool replace, out string error)
    {
        error = null;
        if (!HasStateAuthority || IsExtractionLocked() || IsRaidLootOriginAware != raid ||
            !LootInventoryRules.IsValidSlotCapacity(_slotCapacity, PhysicalSlotCount))
        {
            error = "Loadout requires available State Authority, valid capacity and the matching Raid origin composition.";
            return false;
        }
        if (!RaidLoadoutRules.TryValidate(entries, _lootCatalog, _slotCapacity, out error)) return false;
        Span<LootInventorySlot> planned = stackalloc LootInventorySlot[PhysicalSlotCount];
        planned.Clear();
        // Build and validate the complete physical loadout before writing any quantity/origin.
        for (int index = 0; index < entries.Count; index++)
        {
            LootEntry entry = entries[index];
            if (!_lootCatalog.TryGetIndex(entry.LootId, out int catalogIndex) ||
                TryReceiveOrigin(planned, catalogIndex, origin, entry.Amount) != LootTransferFailureReason.None)
            {
                error = "Loadout exceeds physical stack capacity or item transport limits.";
                return false;
            }
        }
        if (!replace && OccupiedSlotCount > 0)
        {
            // Preserve the non-Raid initialization's existing idempotent aggregate contract;
            // Raid admission is one-shot and never reconstructs origins on restored storage.
            if (!raid && TryMatchesExactContent(entries, out _)) return true;
            error = "Player loot inventory was already initialized.";
            return false;
        }
        WriteSlots(planned, advanceIfUnchanged: replace);
        return true;
    }

    public long CalculateTotalValue()
    {
        if (!TryCalculateTotalValue(out long total))
            throw new InvalidOperationException($"{nameof(PlayerLootReceiver)} cannot calculate a complete value from its catalog.");
        return total;
    }

    public bool TryCalculateTotalValue(out long total)
    {
        total = 0;
        return TryGetLootContent(out IReadOnlyList<LootEntry> content) &&
            LootInventoryValueCalculator.TryCalculate(content, _lootCatalog, out total);
    }

    public bool TryResolveDefinition(LootId lootId, out LootDefinition definition)
    {
        definition = null;
        return _lootCatalog != null && _lootCatalog.TryGet(lootId.Value, out definition);
    }

    public void PublishPickupGrant(in LootTransferRequest request)
    {
        if (!HasStateAuthority || request.DestinationId != Id || request.SourceId.Value == 0 ||
            request.RequestedAmount <= 0 || _lootCatalog == null ||
            !_lootCatalog.TryGetIndex(request.LootId, out int definitionIndex))
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Invalid pickup feedback contract.", this);
            throw new InvalidOperationException("Pickup feedback requires a successfully committed request on State Authority.");
        }
        RPC_ReceiveLootGrant(LootChangeSequence, request.SourceId.Value, definitionIndex, request.RequestedAmount, request.SimulationTick);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveLootGrant(int sequence, int sourceIdValue, int definitionIndex, int amount, int simulationTick)
    {
        if (_lootCatalog == null || !_lootCatalog.TryGetByIndex(definitionIndex, out LootDefinition definition))
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Cannot resolve loot grant index {definitionIndex} for local presentation.", this);
            return;
        }
        _pendingPresentationEvents.Enqueue(new LootGrantPresentationEvent(
            sequence, new EntityId(sourceIdValue), Id, definition.LootId, amount, simulationTick));
    }

    private LootTransferFailureReason ValidateRequest(in LootTransferRequest request, bool receiving, out int catalogIndex)
    {
        catalogIndex = -1;
        if (!HasStateAuthority) return LootTransferFailureReason.MissingAuthority;
        if (IsExtractionLocked()) return LootTransferFailureReason.ContainerUnavailable;
        if (request.SourceId.Value == 0 || (!receiving && request.SourceId != Id)) return LootTransferFailureReason.SourceNotFound;
        if (request.DestinationId.Value == 0 || (receiving && request.DestinationId != Id)) return LootTransferFailureReason.DestinationNotFound;
        if (!request.LootId.IsValid || !LootTransferRequest.IsValidInventorySource(
            request.SourceInventorySlotIndex, request.ExpectedInventoryRevision)) return LootTransferFailureReason.InvalidLoot;
        // The token always belongs to SourceId, never to an incoming receiver.
        if (!receiving && request.HasInventorySlotAddress && request.ExpectedInventoryRevision != Revision)
            return LootTransferFailureReason.ContainerUnavailable;
        if (request.RequestedAmount <= 0) return LootTransferFailureReason.InvalidAmount;
        if (_lootCatalog == null || !LootInventoryRules.IsValidSlotCapacity(_slotCapacity, PhysicalSlotCount))
            return LootTransferFailureReason.ContainerUnavailable;
        if (!_lootCatalog.TryGetIndex(request.LootId, out catalogIndex)) return LootTransferFailureReason.InvalidLoot;
        return catalogIndex < 0 || catalogIndex >= MaxCatalogEntries
            ? LootTransferFailureReason.ContainerUnavailable : LootTransferFailureReason.None;
    }

    private bool TryReadSlots(Span<LootInventorySlot> slots)
    {
        if (_lootCatalog == null || _lootCatalog.DefinitionCount > MaxCatalogEntries ||
            !LootInventoryRules.IsValidSlotCapacity(_slotCapacity, PhysicalSlotCount)) return false;
        for (int index = 0; index < PhysicalSlotCount; index++)
        {
            LootInventorySlot slot = LootInventory[index];
            if (!slot.IsEmpty && (!slot.IsValid || !_lootCatalog.TryGetByIndex(slot.CatalogIndex, out LootDefinition definition) ||
                definition.StackMax < slot.Quantity || definition.MaxTransportable < 0)) return false;
            slots[index] = slot;
        }
        return true;
    }

    private LootTransferFailureReason TryReceiveOrigin(Span<LootInventorySlot> slots, int catalogIndex, RaidLootOrigin origin, int amount)
    {
        if (catalogIndex < 0 || catalogIndex >= MaxCatalogEntries ||
            !_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition)) return LootTransferFailureReason.InvalidLoot;
        return LootInventoryRules.TryReceive(slots, catalogIndex, origin, definition.StackMax,
            definition.MaxTransportable, amount, _slotCapacity);
    }

    private LootTransferFailureReason TryReceiveTransfer(Span<LootInventorySlot> slots, int catalogIndex, RaidLootOriginTransfer transfer)
    {
        for (int index = 0; index < transfer.Count; index++)
        {
            RaidLootOriginBucket bucket = transfer.Buckets[index];
            LootTransferFailureReason failure = TryReceiveOrigin(slots, catalogIndex, bucket.Origin, bucket.Amount);
            if (failure != LootTransferFailureReason.None) return failure;
        }
        return LootTransferFailureReason.None;
    }

    private static LootTransferFailureReason ValidateAddressedSlot(ReadOnlySpan<LootInventorySlot> slots,
        int catalogIndex, in LootTransferRequest request)
    {
        LootInventorySlot slot = slots[request.SourceInventorySlotIndex];
        if (slot.IsEmpty) return LootTransferFailureReason.InsufficientAmount;
        if (!slot.IsValid) return LootTransferFailureReason.ContainerUnavailable;
        if (slot.CatalogIndex != catalogIndex) return LootTransferFailureReason.InvalidLoot;
        return slot.Quantity < request.RequestedAmount
            ? LootTransferFailureReason.InsufficientAmount : LootTransferFailureReason.None;
    }

    private static bool TryExtractInventoryRequest(Span<LootInventorySlot> slots, int catalogIndex,
        in LootTransferRequest request, RaidLootOriginTransfer transfer)
    {
        if (!request.HasInventorySlotAddress)
            return transfer == null
                ? TryExtractOrigin(slots, catalogIndex, RaidLootOrigin.Dungeon, request.RequestedAmount)
                : TryExtractTransfer(slots, catalogIndex, transfer);
        if (!LootTransferRequest.IsValidInventorySlotAddress(request.SourceInventorySlotIndex, request.ExpectedInventoryRevision) ||
            ValidateAddressedSlot(slots, catalogIndex, request) != LootTransferFailureReason.None) return false;
        LootInventorySlot selected = slots[request.SourceInventorySlotIndex];
        if (transfer != null && (transfer.Count != 1 || transfer.Buckets[0].Origin != selected.Origin ||
            transfer.Buckets[0].Amount != request.RequestedAmount)) return false;
        return LootInventoryRules.TryExtract(slots, request.SourceInventorySlotIndex, request.RequestedAmount) ==
            LootTransferFailureReason.None;
    }

    private static bool TryExtractOrigin(Span<LootInventorySlot> slots, int catalogIndex, RaidLootOrigin origin, int amount)
    {
        int remaining = amount;
        for (int index = 0; index < slots.Length && remaining > 0; index++)
        {
            LootInventorySlot slot = slots[index];
            if (!slot.IsValid || slot.CatalogIndex != catalogIndex || slot.Origin != origin) continue;
            int taken = Math.Min(remaining, slot.Quantity);
            if (LootInventoryRules.TryExtract(slots, index, taken) != LootTransferFailureReason.None) return false;
            remaining -= taken;
        }
        return remaining == 0;
    }

    private static bool TryExtractTransfer(Span<LootInventorySlot> slots, int catalogIndex, RaidLootOriginTransfer transfer)
    {
        for (int index = 0; index < transfer.Count; index++)
        {
            RaidLootOriginBucket bucket = transfer.Buckets[index];
            if (!TryExtractOrigin(slots, catalogIndex, bucket.Origin, bucket.Amount)) return false;
        }
        return true;
    }

    private static bool TryGetTransferTotal(RaidLootOriginTransfer transfer, out int total)
    {
        // Canonical strict ordering proves uniqueness without TryGetTotal's HashSet allocation.
        total = 0;
        if (transfer == null || transfer.Count == 0) return false;
        for (int index = 0; index < transfer.Count; index++)
        {
            RaidLootOriginBucket bucket = transfer.Buckets[index];
            if (!bucket.IsValid || (index > 0 && transfer.Buckets[index - 1].Origin.CompareTo(bucket.Origin) >= 0) ||
                total > int.MaxValue - bucket.Amount) return false;
            total += bucket.Amount;
        }
        return true;
    }

    private static bool IsExactTransfer(RaidLootOriginTransfer transfer, int amount) =>
        TryGetTransferTotal(transfer, out int total) && total == amount;

    private static bool TryGetTotal(ReadOnlySpan<LootInventorySlot> slots, int catalogIndex, out int total)
    {
        long sum = 0;
        foreach (LootInventorySlot slot in slots)
            if (slot.IsValid && slot.CatalogIndex == catalogIndex) sum += slot.Quantity;
        total = sum <= int.MaxValue ? (int)sum : 0;
        return sum <= int.MaxValue;
    }

    private void WriteSlots(ReadOnlySpan<LootInventorySlot> slots, bool advanceIfUnchanged = false)
    {
        bool changed = false;
        NetworkArray<LootInventorySlot> inventory = LootInventory;
        for (int index = 0; index < PhysicalSlotCount; index++)
        {
            if (inventory[index] == slots[index]) continue;
            inventory.Set(index, slots[index]);
            changed = true;
        }
        if (changed || advanceIfUnchanged) LootChangeSequence++;
    }

    private void ClearSlots()
    {
        Span<LootInventorySlot> empty = stackalloc LootInventorySlot[PhysicalSlotCount];
        empty.Clear();
        WriteSlots(empty);
    }

    private static int OriginIndex(RaidLootOrigin origin) => origin.IsDungeon ? 0 : origin.PlayerParticipantId.Value;
    private static RaidLootOrigin OriginAt(int index)
    {
        if (index == 0) return RaidLootOrigin.Dungeon;
        RaidParticipantId.TryCreate(index, out RaidParticipantId participantId);
        RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin origin);
        return origin;
    }

    private static bool TryCompareExactContent(IReadOnlyList<LootEntry> expected, IReadOnlyList<LootEntry> current, out string error)
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
                if (current[currentIndex].Equals(entry)) { found = true; break; }
            if (found) continue;
            error = "Snapshot content differs from the current inventory.";
            return false;
        }
        return true;
    }

    private static bool RaidOriginEntriesEqual(IReadOnlyList<RaidLootOriginEntry> expected, IReadOnlyList<RaidLootOriginEntry> current)
    {
        if (expected == null || current == null || expected.Count != current.Count) return false;
        for (int index = 0; index < expected.Count; index++)
            if (!expected[index].Equals(current[index])) return false;
        return true;
    }

    private bool ValidateDependencies()
    {
        if (_character == null || _lootCatalog == null ||
            !LootInventoryRules.IsValidSlotCapacity(_slotCapacity, PhysicalSlotCount))
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Character, loot catalog and physical slot capacity (1..30) are required.", this);
            return false;
        }
        if (!_lootCatalog.TryValidate(out string error) || _lootCatalog.DefinitionCount > MaxCatalogEntries)
        {
            Debug.LogError($"{nameof(PlayerLootReceiver)}: Invalid or oversized loot catalog. {error}", this);
            return false;
        }
        return true;
    }

    private bool IsExtractionLocked() => _extractionController != null && _extractionController.State == ExtractionState.Extracted;

    private void FailCommitContract(string commitName)
    {
        Debug.LogError($"{nameof(PlayerLootReceiver)}: {commitName} contract was violated after successful validation.", this);
        throw new InvalidOperationException($"{commitName} preconditions changed after successful validation.");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_lootCatalog != null) return;
        string[] catalogGuids = UnityEditor.AssetDatabase.FindAssets("t:LootDefinitionCatalog");
        if (catalogGuids.Length != 1) return;
        _lootCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            UnityEditor.AssetDatabase.GUIDToAssetPath(catalogGuids[0]));
    }
#endif
}
