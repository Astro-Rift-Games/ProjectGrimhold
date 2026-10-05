using System;
using System.Collections.Generic;

/// <summary>
/// Immutable content of the Town "Attributes" tab. Created only by
/// <see cref="TownCharacterStatisticsBuilder"/>.
/// </summary>
public readonly struct TownCharacterStatisticsPresentation
{
    private static readonly TownWeaponStatistics[] NoWeapons = Array.Empty<TownWeaponStatistics>();
    private static readonly TownEquipmentSlotEntry[] NoSlots = Array.Empty<TownEquipmentSlotEntry>();

    private readonly TownWeaponStatistics[] _weapons;
    private readonly TownEquipmentSlotEntry[] _slots;

    public TownStatBreakdown MaximumHealth { get; }
    public TownStatBreakdown MaximumStamina { get; }
    public TownStatBreakdown MaximumMana { get; }
    public int PhysicalDefense { get; }
    public int MagicalDefense { get; }
    public float PhysicalMitigationPercent { get; }
    public float MagicalMitigationPercent { get; }
    public float LootBonusPercent { get; }

    /// <summary>Equipped weapons only: shields are excluded and a two-handed weapon appears once.</summary>
    public IReadOnlyList<TownWeaponStatistics> Weapons => _weapons ?? NoWeapons;

    /// <summary>All eight slots: Helmet, Armor, Gloves, Boots, then both Weapon Sets.</summary>
    public IReadOnlyList<TownEquipmentSlotEntry> Slots => _slots ?? NoSlots;

    internal TownCharacterStatisticsPresentation(
        TownStatBreakdown maximumHealth,
        TownStatBreakdown maximumStamina,
        TownStatBreakdown maximumMana,
        int physicalDefense,
        int magicalDefense,
        float physicalMitigationPercent,
        float magicalMitigationPercent,
        float lootBonusPercent,
        TownWeaponStatistics[] weapons,
        TownEquipmentSlotEntry[] slots)
    {
        MaximumHealth = maximumHealth;
        MaximumStamina = maximumStamina;
        MaximumMana = maximumMana;
        PhysicalDefense = physicalDefense;
        MagicalDefense = magicalDefense;
        PhysicalMitigationPercent = physicalMitigationPercent;
        MagicalMitigationPercent = magicalMitigationPercent;
        LootBonusPercent = lootBonusPercent;
        _weapons = weapons;
        _slots = slots;
    }
}
