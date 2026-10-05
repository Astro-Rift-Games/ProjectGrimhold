using System;
using UnityEngine;

/// <summary>One of the eight equipment slots as the character sheet shows it.</summary>
public readonly struct TownEquipmentSlotEntry : IEquatable<TownEquipmentSlotEntry>
{
    public EquipmentSlot Slot { get; }
    public LootId LootId { get; }
    public string DisplayName { get; }
    public Sprite Icon { get; }

    /// <summary>True when nothing is prepared in the slot.</summary>
    public bool IsEmpty => !LootId.IsValid;

    internal TownEquipmentSlotEntry(EquipmentSlot slot, LootId lootId, string displayName, Sprite icon)
    {
        Slot = slot;
        LootId = lootId;
        DisplayName = displayName ?? string.Empty;
        Icon = icon;
    }

    public bool Equals(TownEquipmentSlotEntry other) =>
        Slot == other.Slot && LootId.Equals(other.LootId) &&
        string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal) &&
        Icon == other.Icon;

    public override bool Equals(object obj) => obj is TownEquipmentSlotEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Slot, LootId, DisplayName);
}
