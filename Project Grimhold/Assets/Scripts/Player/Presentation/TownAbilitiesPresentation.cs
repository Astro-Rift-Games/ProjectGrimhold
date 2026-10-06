using System;
using System.Collections.Generic;

/// <summary>
/// Pure projection of the unlocked repertoire, the two prepared universal slots and the
/// requirement checks that the Town Abilities tab renders.
/// </summary>
public readonly struct TownAbilitiesPresentation
{
    private static readonly IReadOnlyList<TownAbilityEntry> NoEntries = Array.Empty<TownAbilityEntry>();

    private readonly IReadOnlyList<TownAbilityEntry> _entries;

    /// <summary>Unlocked abilities only, in catalog order.</summary>
    public IReadOnlyList<TownAbilityEntry> Entries => _entries ?? NoEntries;

    /// <summary>The entry prepared in slot 1, or null when the slot is empty.</summary>
    public TownAbilityEntry? Slot1 { get; }

    /// <summary>The entry prepared in slot 2, or null when the slot is empty.</summary>
    public TownAbilityEntry? Slot2 { get; }

    public TownAbilitiesPresentation(
        IReadOnlyList<TownAbilityEntry> entries,
        TownAbilityEntry? slot1,
        TownAbilityEntry? slot2)
    {
        _entries = entries;
        Slot1 = slot1;
        Slot2 = slot2;
    }

    public TownAbilityEntry? GetSlot(UniversalAbilitySlot slot) => slot switch
    {
        UniversalAbilitySlot.Slot1 => Slot1,
        UniversalAbilitySlot.Slot2 => Slot2,
        _ => null
    };

    /// <summary>Entries matching the resource filter, in catalog order. All returns every entry.</summary>
    public IReadOnlyList<TownAbilityEntry> Filtered(TownAbilitiesFilter filter)
    {
        IReadOnlyList<TownAbilityEntry> entries = Entries;
        if (filter == TownAbilitiesFilter.All)
        {
            return entries;
        }

        AbilityResourceType resource = filter == TownAbilitiesFilter.Stamina
            ? AbilityResourceType.Stamina
            : AbilityResourceType.Mana;
        var result = new List<TownAbilityEntry>(entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].Resource == resource)
            {
                result.Add(entries[index]);
            }
        }

        return result;
    }
}
