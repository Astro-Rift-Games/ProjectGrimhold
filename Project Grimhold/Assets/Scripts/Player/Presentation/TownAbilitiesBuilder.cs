using System.Collections.Generic;

/// <summary>
/// Pure projection of the unlocked repertoire, prepared slots and confirmed attributes into the
/// Town Abilities tab. Unlock is independent of attribute requirements: an unlocked ability whose
/// requirements are not met is still listed, flagged as <see cref="TownAbilityEntryState.RequirementsNotMet"/>.
/// </summary>
public static class TownAbilitiesBuilder
{
    public static bool TryBuild(
        IReadOnlyList<AbilityDefinition> definitions,
        IReadOnlyCollection<AbilityId> unlocked,
        in PreparedAbilityLoadout prepared,
        in CharacterAttributeState attributes,
        out TownAbilitiesPresentation presentation)
    {
        presentation = default;
        if (definitions == null)
        {
            return false;
        }

        var unlockedIds = new HashSet<AbilityId>();
        if (unlocked != null)
        {
            foreach (AbilityId abilityId in unlocked)
            {
                if (abilityId.IsValid)
                {
                    unlockedIds.Add(abilityId);
                }
            }
        }

        var entries = new List<TownAbilityEntry>(definitions.Count);
        TownAbilityEntry? slot1 = null;
        TownAbilityEntry? slot2 = null;
        for (int index = 0; index < definitions.Count; index++)
        {
            AbilityDefinition definition = definitions[index];
            if (definition == null)
            {
                continue;
            }

            AbilityId id = definition.AbilityId;
            if (!id.IsValid || !unlockedIds.Contains(id))
            {
                continue;
            }

            TownAbilityEntry entry = CreateEntry(definition, id, prepared, attributes);
            entries.Add(entry);
            if (entry.HasEquippedSlot)
            {
                if (entry.EquippedSlot == UniversalAbilitySlot.Slot1) slot1 = entry;
                else slot2 = entry;
            }
        }

        presentation = new TownAbilitiesPresentation(entries, slot1, slot2);
        return true;
    }

    private static TownAbilityEntry CreateEntry(
        AbilityDefinition definition,
        AbilityId id,
        in PreparedAbilityLoadout prepared,
        in CharacterAttributeState attributes)
    {
        bool hasSlot = false;
        UniversalAbilitySlot slot = UniversalAbilitySlot.Slot1;
        if (prepared.Slot1 == id)
        {
            hasSlot = true;
        }
        else if (prepared.Slot2 == id)
        {
            hasSlot = true;
            slot = UniversalAbilitySlot.Slot2;
        }

        var checks = new List<TownAbilityRequirementCheck>();
        bool allMet = true;
        CharacterAttributeRequirements requirements = definition.AttributeRequirements;
        if (requirements != null)
        {
            IReadOnlyList<CharacterAttributeRequirement> authored = requirements.Requirements;
            for (int index = 0; index < authored.Count; index++)
            {
                attributes.TryGetValue(authored[index].Attribute, out int current);
                var check = new TownAbilityRequirementCheck(
                    authored[index].Attribute, authored[index].MinimumValue, current);
                checks.Add(check);
                allMet &= check.IsMet;
            }
        }

        TownAbilityEntryState state = hasSlot
            ? TownAbilityEntryState.Equipped
            : allMet ? TownAbilityEntryState.Available : TownAbilityEntryState.RequirementsNotMet;

        return new TownAbilityEntry(
            id,
            definition.DisplayName,
            definition.Description,
            definition.Icon,
            definition.Resource,
            definition.Cost,
            definition.CooldownSeconds,
            checks,
            state,
            hasSlot,
            slot);
    }
}
