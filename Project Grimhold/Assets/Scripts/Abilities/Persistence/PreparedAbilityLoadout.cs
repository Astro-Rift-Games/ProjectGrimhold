using System;
using System.Collections.Generic;

/// <summary>Persistent ordered pair of equivalent optional universal ability slots.</summary>
public readonly struct PreparedAbilityLoadout : IEquatable<PreparedAbilityLoadout>
{
    public AbilityId Slot1 { get; }
    public AbilityId Slot2 { get; }

    public PreparedAbilityLoadout(AbilityId slot1, AbilityId slot2)
    {
        Slot1 = slot1;
        Slot2 = slot2;
    }

    public AbilityId Get(UniversalAbilitySlot slot) => slot switch
    {
        UniversalAbilitySlot.Slot1 => Slot1,
        UniversalAbilitySlot.Slot2 => Slot2,
        _ => default
    };

    public PreparedAbilityLoadout With(UniversalAbilitySlot slot, AbilityId abilityId) => slot switch
    {
        UniversalAbilitySlot.Slot1 => new PreparedAbilityLoadout(abilityId, Slot2),
        UniversalAbilitySlot.Slot2 => new PreparedAbilityLoadout(Slot1, abilityId),
        _ => this
    };

    public PreparedAbilityLoadout Without(UniversalAbilitySlot slot) => With(slot, default);

    public static bool IsKnownSlot(UniversalAbilitySlot slot) =>
        slot == UniversalAbilitySlot.Slot1 || slot == UniversalAbilitySlot.Slot2;

    public static bool TryValidate(
        in PreparedAbilityLoadout loadout,
        IReadOnlyList<AbilityId> unlockedAbilities,
        in CharacterAttributeState attributes,
        AbilityDefinitionCatalog catalog,
        out string error)
    {
        if (loadout.Slot1.IsValid && loadout.Slot1 == loadout.Slot2)
        {
            error = "Prepared ability slots contain a duplicate ability ID.";
            return false;
        }

        return TryValidateSlot(loadout.Slot1, unlockedAbilities, attributes, catalog, out error) &&
               TryValidateSlot(loadout.Slot2, unlockedAbilities, attributes, catalog, out error);
    }

    /// <summary>Validates the ordered pair's transport-only invariants.</summary>
    public static bool TryValidateTransportShape(
        in PreparedAbilityLoadout loadout,
        out string error)
    {
        if (loadout.Slot1.IsValid && loadout.Slot1 == loadout.Slot2)
        {
            error = "Prepared ability slots contain a duplicate ability ID.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Revalidates transported identities against Raid's authoritative catalog and admitted
    /// attributes. Ownership is validated locally before transport; the repertoire never crosses.
    /// </summary>
    public static bool TryValidateAdmission(
        in PreparedAbilityLoadout loadout,
        in CharacterAttributeState attributes,
        AbilityDefinitionCatalog catalog,
        out string error)
    {
        if (!TryValidateTransportShape(loadout, out error))
        {
            return false;
        }

        if (catalog == null)
        {
            error = "Ability catalog is missing.";
            return false;
        }

        return TryValidateAdmissionSlot(loadout.Slot1, attributes, catalog, out error) &&
               TryValidateAdmissionSlot(loadout.Slot2, attributes, catalog, out error);
    }

    /// <summary>
    /// Revalidates a previously valid loadout after confirmed attributes change. Requirement-invalid
    /// slots are cleared; unknown, locked, or duplicate state is rejected rather than normalized.
    /// </summary>
    public static bool TryRevalidateAfterAttributeChange(
        in PreparedAbilityLoadout loadout,
        IReadOnlyList<AbilityId> unlockedAbilities,
        in CharacterAttributeState attributes,
        AbilityDefinitionCatalog catalog,
        out PreparedAbilityLoadout revalidated,
        out string error)
    {
        revalidated = loadout;
        if (loadout.Slot1.IsValid && loadout.Slot1 == loadout.Slot2)
        {
            error = "Prepared ability slots contain a duplicate ability ID.";
            return false;
        }

        if (!TryResolveOwned(loadout.Slot1, unlockedAbilities, catalog, out AbilityDefinition slot1, out error) ||
            !TryResolveOwned(loadout.Slot2, unlockedAbilities, catalog, out AbilityDefinition slot2, out error))
        {
            return false;
        }

        if (slot1 != null && !slot1.AreAttributeRequirementsSatisfiedBy(attributes))
        {
            revalidated = revalidated.Without(UniversalAbilitySlot.Slot1);
        }

        if (slot2 != null && !slot2.AreAttributeRequirementsSatisfiedBy(attributes))
        {
            revalidated = revalidated.Without(UniversalAbilitySlot.Slot2);
        }

        return TryValidate(revalidated, unlockedAbilities, attributes, catalog, out error);
    }

    public bool Equals(PreparedAbilityLoadout other) => Slot1 == other.Slot1 && Slot2 == other.Slot2;
    public override bool Equals(object obj) => obj is PreparedAbilityLoadout other && Equals(other);
    public override int GetHashCode() => unchecked((Slot1.GetHashCode() * 397) ^ Slot2.GetHashCode());

    private static bool TryValidateSlot(
        AbilityId abilityId,
        IReadOnlyList<AbilityId> unlockedAbilities,
        in CharacterAttributeState attributes,
        AbilityDefinitionCatalog catalog,
        out string error)
    {
        if (!TryResolveOwned(abilityId, unlockedAbilities, catalog, out AbilityDefinition definition, out error))
        {
            return false;
        }

        if (definition != null && !definition.AreAttributeRequirementsSatisfiedBy(attributes))
        {
            error = $"Prepared ability '{abilityId.Value}' does not satisfy its attribute requirements.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateAdmissionSlot(
        AbilityId abilityId,
        in CharacterAttributeState attributes,
        AbilityDefinitionCatalog catalog,
        out string error)
    {
        if (!abilityId.IsValid)
        {
            error = null;
            return true;
        }

        if (!catalog.TryGet(abilityId, out AbilityDefinition definition) || definition == null)
        {
            error = $"Prepared ability '{abilityId.Value}' is unknown to the authoritative catalog.";
            return false;
        }

        if (!definition.AreAttributeRequirementsSatisfiedBy(attributes))
        {
            error = $"Prepared ability '{abilityId.Value}' does not satisfy its admitted attribute requirements.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryResolveOwned(
        AbilityId abilityId,
        IReadOnlyList<AbilityId> unlockedAbilities,
        AbilityDefinitionCatalog catalog,
        out AbilityDefinition definition,
        out string error)
    {
        definition = null;
        error = null;
        if (!abilityId.IsValid)
        {
            return true;
        }

        if (catalog == null || !catalog.TryGet(abilityId, out definition) || definition == null)
        {
            error = $"Prepared ability '{abilityId.Value}' is unknown to the authoritative catalog.";
            return false;
        }

        if (unlockedAbilities == null || !Contains(unlockedAbilities, abilityId))
        {
            error = $"Prepared ability '{abilityId.Value}' is not in the unlocked repertoire.";
            return false;
        }

        return true;
    }

    private static bool Contains(IReadOnlyList<AbilityId> values, AbilityId abilityId)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (values[index] == abilityId)
            {
                return true;
            }
        }

        return false;
    }
}
