using System;
using System.Collections.Generic;

/// <summary>
/// Immutable assignment of loot definitions to the player's four Quick Slots.
/// A slot references a <see cref="LootId"/>, never a physical stack, and a
/// definition occupies at most one slot.
/// </summary>
public readonly struct QuickSlotBindings : IEquatable<QuickSlotBindings>
{
    public const int SlotCount = 4;

    private readonly LootId _slot0;
    private readonly LootId _slot1;
    private readonly LootId _slot2;
    private readonly LootId _slot3;

    private QuickSlotBindings(LootId slot0, LootId slot1, LootId slot2, LootId slot3)
    {
        _slot0 = slot0;
        _slot1 = slot1;
        _slot2 = slot2;
        _slot3 = slot3;
    }

    public static QuickSlotBindings Empty => default;

    public bool IsEmpty => !_slot0.IsValid && !_slot1.IsValid && !_slot2.IsValid && !_slot3.IsValid;

    public static bool IsValidSlotIndex(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;

    /// <summary>Returns the bound definition, or a default (invalid) id when the slot is empty.</summary>
    public LootId Get(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return _slot0;
            case 1: return _slot1;
            case 2: return _slot2;
            case 3: return _slot3;
            default: throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
    }

    public bool IsAssigned(int slotIndex) => Get(slotIndex).IsValid;

    /// <summary>Finds the slot holding the definition, or -1 when it is not assigned.</summary>
    public int FindSlot(LootId lootId)
    {
        if (!lootId.IsValid)
        {
            return -1;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (Get(i).Equals(lootId))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns a copy with the definition in the slot. A previous slot holding the same
    /// definition is cleared so the binding moves instead of duplicating.
    /// </summary>
    public QuickSlotBindings With(int slotIndex, LootId lootId)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        if (!lootId.IsValid)
        {
            throw new ArgumentException("LootId must be valid.", nameof(lootId));
        }

        return new QuickSlotBindings(
            Resolve(0, slotIndex, lootId),
            Resolve(1, slotIndex, lootId),
            Resolve(2, slotIndex, lootId),
            Resolve(3, slotIndex, lootId));
    }

    public QuickSlotBindings Without(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        return new QuickSlotBindings(
            slotIndex == 0 ? default : _slot0,
            slotIndex == 1 ? default : _slot1,
            slotIndex == 2 ? default : _slot2,
            slotIndex == 3 ? default : _slot3);
    }

    /// <summary>
    /// Builds bindings from persisted identifiers. Null or blank entries are unassigned.
    /// Fails on a wrong length or a repeated definition.
    /// </summary>
    public static bool TryCreate(IReadOnlyList<string> ids, out QuickSlotBindings bindings, out string error)
    {
        bindings = default;
        error = null;

        if (ids == null || ids.Count != SlotCount)
        {
            error = $"Quick Slot bindings require exactly {SlotCount} entries.";
            return false;
        }

        var slots = new LootId[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            if (string.IsNullOrWhiteSpace(ids[i]))
            {
                continue;
            }

            var lootId = new LootId(ids[i]);
            for (int j = 0; j < i; j++)
            {
                if (slots[j].Equals(lootId))
                {
                    error = $"Quick Slot definition '{lootId}' is assigned to more than one slot.";
                    return false;
                }
            }

            slots[i] = lootId;
        }

        bindings = new QuickSlotBindings(slots[0], slots[1], slots[2], slots[3]);
        return true;
    }

    public bool Equals(QuickSlotBindings other) =>
        _slot0.Equals(other._slot0) &&
        _slot1.Equals(other._slot1) &&
        _slot2.Equals(other._slot2) &&
        _slot3.Equals(other._slot3);

    public override bool Equals(object obj) => obj is QuickSlotBindings other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_slot0, _slot1, _slot2, _slot3);

    public static bool operator ==(QuickSlotBindings left, QuickSlotBindings right) => left.Equals(right);
    public static bool operator !=(QuickSlotBindings left, QuickSlotBindings right) => !left.Equals(right);

    private LootId Resolve(int index, int targetIndex, LootId lootId)
    {
        if (index == targetIndex)
        {
            return lootId;
        }

        LootId current = Get(index);
        return current.Equals(lootId) ? default : current;
    }
}
