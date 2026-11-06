using System;
using Grimhold.Backend;

/// <summary>One admission projection for both ticket decoding and backend verification responses.</summary>
public static class RaidAdmissionSnapshotMapper
{
    public static bool TryProject(in AuthoritativeRaidAdmissionSnapshotDto snapshot,
        out CharacterAttributeState attributes, out PreparedAbilityLoadout abilities,
        out LootEntry[] items, out PreparedEquipmentLoadout equipment)
    {
        attributes = default; abilities = default; items = Array.Empty<LootEntry>(); equipment = default;
        var state = snapshot.characterAttributes;
        if (!CharacterAttributeState.TryCreate(state.vitality, state.resistance, state.strength, state.dexterity,
            state.intelligence, state.luck, state.availablePoints, out attributes)) return false;
        AbilityId first = default, second = default;
        if (!string.IsNullOrEmpty(snapshot.preparedAbilities.slot1) && !AbilityId.TryCreate(snapshot.preparedAbilities.slot1, out first) ||
            !string.IsNullOrEmpty(snapshot.preparedAbilities.slot2) && !AbilityId.TryCreate(snapshot.preparedAbilities.slot2, out second)) return false;
        abilities = new PreparedAbilityLoadout(first, second);
        if (!PreparedAbilityLoadout.TryValidateTransportShape(abilities, out _)) return false;
        var source = snapshot.items ?? Array.Empty<LootEntryDto>();
        if (source.Length > RaidLoadoutRules.MaximumEntries) return false;
        items = new LootEntry[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            WeaponInstanceModifiers? modifiers = null;
            if (source[index].modifiers != null)
            {
                if (!source[index].modifiers.TryToTransportModifiers(out var parsed, out _)) return false;
                modifiers = parsed;
            }
            items[index] = new LootEntry(new LootId(source[index].lootId), source[index].amount, modifiers);
            if (!items[index].IsValid) return false;
        }
        foreach (var slot in EquipmentSlotRules.AllSlots)
        {
            string id = snapshot.preparedEquipment.Definitions.Get(slot);
            if (string.IsNullOrEmpty(id)) continue;
            WeaponInstanceModifiers? modifiers = null;
            bool isUnit = snapshot.preparedEquipment.Units != null && snapshot.preparedEquipment.Units.ContainsKey(slot);
            if (isUnit && snapshot.preparedEquipment.Units[slot] != null)
            {
                if (!snapshot.preparedEquipment.Units[slot].TryToTransportModifiers(out var parsed, out _)) return false;
                modifiers = parsed;
            }
            equipment = equipment.With(slot, new LootId(id), isUnit ? new EquipmentInstance(null, modifiers) : null);
        }
        return true;
    }
}
