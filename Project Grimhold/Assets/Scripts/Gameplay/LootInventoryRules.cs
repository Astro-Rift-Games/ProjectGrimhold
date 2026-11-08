using System;

/// <summary>
/// Deterministic physical Inventory rules plus legacy aggregate rules for unmigrated endpoints.
/// Owns no state and does not access Unity scenes or Fusion simulation.
/// </summary>
public static class LootInventoryRules
{
    public const int PhysicalSlotCount = 30;

    /// <summary>Whether a definition may be voluntarily dropped from Inventory into the world (Doc 07 §15).</summary>
    public static bool CanDiscardItem(LootDefinition definition) =>
        definition != null && definition.CanBeDiscarded;

    /// <summary>Joint quantity/origin/modifier reception. Weapons are individual units, never stacks.</summary>
    public static LootTransferFailureReason TryReceive(Span<LootInventorySlot> slots,
        Span<WeaponInstanceModifiersNetworkData> modifiers, int catalogIndex, RaidLootOrigin origin,
        int stackMax, int maxTransportable, int requestedAmount, bool isWeapon,
        WeaponInstanceModifiersNetworkData incoming, int slotCapacity = PhysicalSlotCount)
    {
        if (modifiers.Length != slots.Length) return LootTransferFailureReason.ContainerUnavailable;
        if (isWeapon && requestedAmount != 1) return LootTransferFailureReason.InvalidAmount;
        if (!isWeapon && !incoming.IsEmpty) return LootTransferFailureReason.InvalidLoot;
        int target = -1;
        if (isWeapon)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsEmpty) { target = i; break; }
        }
        LootTransferFailureReason failure = TryReceive(slots, catalogIndex, origin,
            isWeapon ? 1 : stackMax, maxTransportable, requestedAmount, slotCapacity);
        if (failure != LootTransferFailureReason.None) return failure;
        if (isWeapon) modifiers[target] = incoming;
        else
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsValid && slots[i].CatalogIndex == catalogIndex) modifiers[i] = default;
        return LootTransferFailureReason.None;
    }

    /// <summary>Clears a depleted unit's modifiers with its physical slot.</summary>
    public static LootTransferFailureReason TryExtract(Span<LootInventorySlot> slots,
        Span<WeaponInstanceModifiersNetworkData> modifiers, int slotIndex, int requestedAmount)
    {
        if (modifiers.Length != slots.Length) return LootTransferFailureReason.ContainerUnavailable;
        LootTransferFailureReason failure = TryExtract(slots, slotIndex, requestedAmount);
        if (failure == LootTransferFailureReason.None && slots[slotIndex].IsEmpty) modifiers[slotIndex] = default;
        return failure;
    }

    /// <summary>A non-weapon split never manufactures a weapon modifier.</summary>
    public static LootTransferFailureReason TrySplit(Span<LootInventorySlot> slots,
        Span<WeaponInstanceModifiersNetworkData> modifiers, int sourceSlotIndex, int splitAmount,
        int destinationSlotIndex, int slotCapacity = PhysicalSlotCount)
    {
        if (modifiers.Length != slots.Length) return LootTransferFailureReason.ContainerUnavailable;
        LootTransferFailureReason failure = ValidateSplit(slots, sourceSlotIndex, splitAmount, destinationSlotIndex, slotCapacity);
        if (failure != LootTransferFailureReason.None) return failure;
        if (!modifiers[sourceSlotIndex].IsEmpty) return LootTransferFailureReason.InvalidLoot;
        failure = TrySplit(slots, sourceSlotIndex, splitAmount, destinationSlotIndex, slotCapacity);
        if (failure == LootTransferFailureReason.None) modifiers[destinationSlotIndex] = default;
        return failure;
    }

    /// <summary>
    /// Validates a complete reception without mutation. The caller supplies the target
    /// definition's StackMax (1 for nonstackables) and MaxTransportable (0 for unlimited).
    /// Other definitions' configuration remains the caller's initialization invariant.
    /// </summary>
    public static LootTransferFailureReason ValidateReceive(
        ReadOnlySpan<LootInventorySlot> slots,
        int catalogIndex,
        RaidLootOrigin origin,
        int stackMax,
        int maxTransportable,
        int requestedAmount,
        int slotCapacity = PhysicalSlotCount)
    {
        LootTransferFailureReason failure = GetReceiveCapacity(
            slots, catalogIndex, origin, stackMax, maxTransportable, requestedAmount,
            slotCapacity, out int currentTotal, out long capacity);
        if (failure != LootTransferFailureReason.None)
            return failure;

        if (requestedAmount > int.MaxValue - currentTotal)
            return LootTransferFailureReason.Overflow;
        if (maxTransportable > 0 && requestedAmount > maxTransportable - currentTotal)
            return LootTransferFailureReason.MaxTransportableExceeded;

        return requestedAmount > capacity
            ? LootTransferFailureReason.InventoryFull
            : LootTransferFailureReason.None;
    }

    /// <summary>
    /// Returns up to the requested amount without mutation. None may mean partial capacity;
    /// callers must transfer only the returned quantity and leave the remainder at source.
    /// </summary>
    public static LootTransferFailureReason GetReceivableAmount(
        ReadOnlySpan<LootInventorySlot> slots,
        int catalogIndex,
        RaidLootOrigin origin,
        int stackMax,
        int maxTransportable,
        int requestedAmount,
        out int transferableAmount,
        int slotCapacity = PhysicalSlotCount)
    {
        transferableAmount = 0;
        LootTransferFailureReason failure = GetReceiveCapacity(
            slots, catalogIndex, origin, stackMax, maxTransportable, requestedAmount,
            slotCapacity, out int currentTotal, out long capacity);
        if (failure != LootTransferFailureReason.None)
            return failure;

        if (currentTotal == int.MaxValue)
            return LootTransferFailureReason.Overflow;
        if (maxTransportable > 0 && currentTotal >= maxTransportable)
            return LootTransferFailureReason.MaxTransportableExceeded;
        if (capacity == 0)
            return LootTransferFailureReason.InventoryFull;

        capacity = Math.Min(capacity, int.MaxValue - currentTotal);
        if (maxTransportable > 0)
            capacity = Math.Min(capacity, maxTransportable - currentTotal);
        transferableAmount = (int)Math.Min(capacity, requestedAmount);
        return LootTransferFailureReason.None;
    }

    /// <summary>
    /// Revalidates and commits the entire amount or changes nothing. Partial stacks are
    /// filled first, then empty slots, both in ascending address order. No compaction.
    /// The caller must provide exclusive synchronous access through validation and commit.
    /// </summary>
    public static LootTransferFailureReason TryReceive(
        Span<LootInventorySlot> slots,
        int catalogIndex,
        RaidLootOrigin origin,
        int stackMax,
        int maxTransportable,
        int requestedAmount,
        int slotCapacity = PhysicalSlotCount)
    {
        LootTransferFailureReason failure = ValidateReceive(
            slots, catalogIndex, origin, stackMax, maxTransportable, requestedAmount, slotCapacity);
        if (failure != LootTransferFailureReason.None)
            return failure;

        int remaining = requestedAmount;
        for (int pass = 0; pass < 2 && remaining > 0; pass++)
        {
            for (int index = 0; index < slots.Length && remaining > 0; index++)
            {
                LootInventorySlot slot = slots[index];
                bool compatible = slot.IsValid && slot.CatalogIndex == catalogIndex && slot.Origin == origin;
                if (pass == 0 ? !compatible : !slot.IsEmpty)
                    continue;

                int current = slot.IsEmpty ? 0 : slot.Quantity;
                int added = Math.Min(remaining, stackMax - current);
                // The complete operation was validated above; construction cannot fail.
                LootInventorySlot.TryCreate(catalogIndex, current + added, origin, out slots[index]);
                remaining -= added;
            }
        }

        return LootTransferFailureReason.None;
    }

    /// <summary>Validates removal from one addressed slot, never from adjacent stacks.</summary>
    public static LootTransferFailureReason ValidateExtraction(
        ReadOnlySpan<LootInventorySlot> slots, int slotIndex, int requestedAmount)
    {
        if (requestedAmount <= 0)
            return LootTransferFailureReason.InvalidAmount;
        if (slots.Length != PhysicalSlotCount)
            return LootTransferFailureReason.ContainerUnavailable;
        if (slotIndex < 0 || slotIndex >= slots.Length)
            return LootTransferFailureReason.InvalidLoot;

        LootInventorySlot slot = slots[slotIndex];
        if (slot.IsEmpty)
            return LootTransferFailureReason.InsufficientAmount;
        if (!slot.IsValid)
            return LootTransferFailureReason.ContainerUnavailable;
        return requestedAmount > slot.Quantity
            ? LootTransferFailureReason.InsufficientAmount
            : LootTransferFailureReason.None;
    }

    /// <summary>Removes exactly the requested quantity, clearing only a fully depleted slot.</summary>
    public static LootTransferFailureReason TryExtract(
        Span<LootInventorySlot> slots, int slotIndex, int requestedAmount)
    {
        LootTransferFailureReason failure = ValidateExtraction(slots, slotIndex, requestedAmount);
        if (failure != LootTransferFailureReason.None)
            return failure;

        LootInventorySlot slot = slots[slotIndex];
        int remaining = slot.Quantity - requestedAmount;
        if (remaining == 0)
            slots[slotIndex] = default;
        else
            LootInventorySlot.TryCreate(slot.CatalogIndex, remaining, slot.Origin, out slots[slotIndex]);
        return LootTransferFailureReason.None;
    }

    /// <summary>
    /// Validates splitting part of one addressed stack into an empty addressed slot without mutation.
    /// The amount must leave at least one unit at source; an occupied or identical destination is
    /// rejected as <see cref="LootTransferFailureReason.DestinationNotFound"/>, and the new stack
    /// must fit the gameplay slot capacity.
    /// </summary>
    public static LootTransferFailureReason ValidateSplit(
        ReadOnlySpan<LootInventorySlot> slots,
        int sourceSlotIndex,
        int splitAmount,
        int destinationSlotIndex,
        int slotCapacity = PhysicalSlotCount)
    {
        if (splitAmount <= 0)
            return LootTransferFailureReason.InvalidAmount;
        if (slots.Length != PhysicalSlotCount || !IsValidSlotCapacity(slotCapacity, PhysicalSlotCount))
            return LootTransferFailureReason.ContainerUnavailable;
        if (sourceSlotIndex < 0 || sourceSlotIndex >= slots.Length ||
            destinationSlotIndex < 0 || destinationSlotIndex >= slots.Length ||
            sourceSlotIndex == destinationSlotIndex)
            return LootTransferFailureReason.InvalidLoot;

        LootInventorySlot source = slots[sourceSlotIndex];
        if (source.IsEmpty)
            return LootTransferFailureReason.InsufficientAmount;
        if (!source.IsValid)
            return LootTransferFailureReason.ContainerUnavailable;
        if (splitAmount >= source.Quantity)
            return LootTransferFailureReason.InvalidAmount;
        if (!slots[destinationSlotIndex].IsEmpty)
            return LootTransferFailureReason.DestinationNotFound;

        int occupied = 0;
        for (int index = 0; index < slots.Length; index++)
            if (!slots[index].IsEmpty)
                occupied++;
        return occupied >= slotCapacity
            ? LootTransferFailureReason.InventoryFull
            : LootTransferFailureReason.None;
    }

    /// <summary>
    /// Revalidates and commits a split or changes nothing. The destination receives exactly the
    /// split amount with the source's item and Origin; the total and every Origin bucket are conserved.
    /// </summary>
    public static LootTransferFailureReason TrySplit(
        Span<LootInventorySlot> slots,
        int sourceSlotIndex,
        int splitAmount,
        int destinationSlotIndex,
        int slotCapacity = PhysicalSlotCount)
    {
        LootTransferFailureReason failure = ValidateSplit(
            slots, sourceSlotIndex, splitAmount, destinationSlotIndex, slotCapacity);
        if (failure != LootTransferFailureReason.None)
            return failure;

        LootInventorySlot source = slots[sourceSlotIndex];
        // Validated above: both quantities are positive and the identity is valid.
        LootInventorySlot.TryCreate(source.CatalogIndex, source.Quantity - splitAmount, source.Origin, out slots[sourceSlotIndex]);
        LootInventorySlot.TryCreate(source.CatalogIndex, splitAmount, source.Origin, out slots[destinationSlotIndex]);
        return LootTransferFailureReason.None;
    }

    private static LootTransferFailureReason GetReceiveCapacity(
        ReadOnlySpan<LootInventorySlot> slots,
        int catalogIndex,
        RaidLootOrigin origin,
        int stackMax,
        int maxTransportable,
        int requestedAmount,
        int slotCapacity,
        out int currentTotal,
        out long capacity)
    {
        currentTotal = 0;
        capacity = 0;
        if (requestedAmount <= 0)
            return LootTransferFailureReason.InvalidAmount;
        if (catalogIndex < 0 || catalogIndex >= RaidLootOriginPackedBuffer.MaximumCatalogEntries || !origin.IsValid)
            return LootTransferFailureReason.InvalidLoot;
        if (slots.Length != PhysicalSlotCount || stackMax <= 0 || maxTransportable < 0 ||
            !IsValidSlotCapacity(slotCapacity, PhysicalSlotCount))
            return LootTransferFailureReason.ContainerUnavailable;

        long total = 0;
        int occupied = 0;
        for (int index = 0; index < slots.Length; index++)
        {
            LootInventorySlot slot = slots[index];
            if (slot.IsEmpty)
                continue;
            if (!slot.IsValid)
                return LootTransferFailureReason.ContainerUnavailable;
            occupied++;
            if (slot.CatalogIndex != catalogIndex)
                continue;
            if (slot.Quantity > stackMax)
                return LootTransferFailureReason.ContainerUnavailable;

            total += slot.Quantity;
            if (slot.Origin == origin)
                capacity += stackMax - slot.Quantity;
        }

        // A lower gameplay limit never resizes storage or moves existing slots. It only
        // limits how many additional holes may be occupied; extraction remains possible.
        capacity += (long)Math.Max(0, slotCapacity - occupied) * stackMax;
        if (total > int.MaxValue)
            return LootTransferFailureReason.Overflow;
        currentTotal = (int)total;
        return LootTransferFailureReason.None;
    }

    /// <summary>
    /// Determines whether gameplay capacity fits within its technical representation.
    /// </summary>
    public static bool IsValidSlotCapacity(int slotCapacity, int maximumRepresentableSlots)
    {
        return slotCapacity > 0 && slotCapacity <= maximumRepresentableSlots;
    }

    /// <summary>
    /// Validates whether a complete quantity can be added to an aggregated stack.
    /// </summary>
    public static LootTransferFailureReason ValidateReceive(
        bool alreadyHeld,
        int currentAmount,
        int occupiedSlotCount,
        int slotCapacity,
        int requestedAmount)
    {
        if (requestedAmount <= 0)
        {
            return LootTransferFailureReason.InvalidAmount;
        }

        if (slotCapacity <= 0 || occupiedSlotCount < 0 || occupiedSlotCount > slotCapacity)
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        if (alreadyHeld)
        {
            if (currentAmount <= 0)
            {
                return LootTransferFailureReason.ContainerUnavailable;
            }

            return currentAmount > int.MaxValue - requestedAmount
                ? LootTransferFailureReason.Overflow
                : LootTransferFailureReason.None;
        }

        if (currentAmount != 0)
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        return occupiedSlotCount >= slotCapacity
            ? LootTransferFailureReason.InventoryFull
            : LootTransferFailureReason.None;
    }

    /// <summary>
    /// Validates whether a complete quantity can be removed from an aggregated stack.
    /// </summary>
    public static LootTransferFailureReason ValidateExtraction(
        bool alreadyHeld,
        int currentAmount,
        int requestedAmount)
    {
        if (requestedAmount <= 0)
        {
            return LootTransferFailureReason.InvalidAmount;
        }

        if (!alreadyHeld)
        {
            return LootTransferFailureReason.InsufficientAmount;
        }

        if (currentAmount <= 0)
        {
            return LootTransferFailureReason.ContainerUnavailable;
        }

        return requestedAmount > currentAmount
            ? LootTransferFailureReason.InsufficientAmount
            : LootTransferFailureReason.None;
    }

    /// <summary>
    /// Calculates the stored amount after a reception that has already been validated.
    /// </summary>
    public static int CalculateReceivedAmount(int currentAmount, int requestedAmount)
    {
        return checked(currentAmount + requestedAmount);
    }

    /// <summary>
    /// Calculates the stored amount after an extraction that has already been validated.
    /// Zero means that the caller must remove the stack.
    /// </summary>
    public static int CalculateRemainingAmount(int currentAmount, int requestedAmount)
    {
        return checked(currentAmount - requestedAmount);
    }
}
