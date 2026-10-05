using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Turns a <see cref="TownCharacterStatisticsPresentation"/> into the ordered rows of the Town
/// statistics column. Pure and culture-independent so the view only has to lay the rows out.
/// </summary>
public static class TownCharacterStatisticsLines
{
    public static IReadOnlyList<TownStatLine> Build(in TownCharacterStatisticsPresentation statistics)
    {
        var lines = new List<TownStatLine>(16)
        {
            new(TownStatLineKind.Header, "Core"),
            Resource("Max Health", statistics.MaximumHealth),
            Resource("Max Stamina", statistics.MaximumStamina),
            Resource("Max Mana", statistics.MaximumMana),
            new(TownStatLineKind.Header, "Defense"),
            Defense("Physical Defense", statistics.PhysicalDefense, statistics.PhysicalMitigationPercent),
            Defense("Magical Defense", statistics.MagicalDefense, statistics.MagicalMitigationPercent),
            new(TownStatLineKind.Header, "Weapons")
        };

        IReadOnlyList<TownWeaponStatistics> weapons = statistics.Weapons;
        if (weapons.Count == 0)
        {
            lines.Add(new TownStatLine(TownStatLineKind.Note, "No weapons equipped"));
        }

        for (int index = 0; index < weapons.Count; index++)
        {
            lines.Add(Weapon(weapons[index]));
        }

        lines.Add(new TownStatLine(TownStatLineKind.Header, "Utility"));
        lines.Add(new TownStatLine(
            TownStatLineKind.Stat,
            "Loot Bonus",
            Number(statistics.LootBonusPercent) + "%"));
        return lines;
    }

    private static TownStatLine Resource(string label, TownStatBreakdown breakdown) => new(
        TownStatLineKind.Stat,
        label,
        breakdown.Total.ToString(CultureInfo.InvariantCulture),
        breakdown.FromEquipment == 0
            ? string.Empty
            : (breakdown.FromEquipment > 0 ? "+" : string.Empty) +
              breakdown.FromEquipment.ToString(CultureInfo.InvariantCulture));

    private static TownStatLine Defense(string label, int defense, float mitigationPercent) => new(
        TownStatLineKind.Stat,
        label,
        defense.ToString(CultureInfo.InvariantCulture),
        Number(mitigationPercent) + "% reduction");

    private static TownStatLine Weapon(TownWeaponStatistics weapon)
    {
        string detail = $"{SlotLabel(weapon.Slot)} · {weapon.DamageType}";
        if (!weapon.EffectiveDamage.Equals(weapon.BaseDamage))
        {
            detail += $" · base {Number(weapon.BaseDamage)}";
        }

        return new TownStatLine(
            TownStatLineKind.Stat,
            weapon.DisplayName,
            Number(weapon.EffectiveDamage),
            detail);
    }

    private static string SlotLabel(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.WeaponSetAMainHand => "Set A Main Hand",
        EquipmentSlot.WeaponSetAOffHand => "Set A Off Hand",
        EquipmentSlot.WeaponSetBMainHand => "Set B Main Hand",
        EquipmentSlot.WeaponSetBOffHand => "Set B Off Hand",
        _ => slot.ToString()
    };

    private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
