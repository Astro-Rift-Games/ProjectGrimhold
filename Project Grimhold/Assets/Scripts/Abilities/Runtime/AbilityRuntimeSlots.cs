/// <summary>Derived configuration, not a second source of Raid ability entitlement.</summary>
public sealed class AbilityRuntimeSlots
{
    private readonly AbilityRuntimeSlot _slot1;
    private readonly AbilityRuntimeSlot _slot2;

    private AbilityRuntimeSlots(AbilityRuntimeSlot slot1, AbilityRuntimeSlot slot2)
    {
        _slot1 = slot1;
        _slot2 = slot2;
    }

    public static bool TryCreate(
        in PreparedAbilityLoadout prepared,
        AbilityDefinitionCatalog catalog,
        out AbilityRuntimeSlots slots,
        out string error)
    {
        slots = null;
        if (!PreparedAbilityLoadout.TryValidateTransportShape(prepared, out error))
        {
            return false;
        }

        if (catalog == null)
        {
            error = "Raid ability runtime requires the authorized ability catalog.";
            return false;
        }

        if (!catalog.TryValidate(out error) ||
            !TryResolve(prepared.Slot1, catalog, out var first, out error) ||
            !TryResolve(prepared.Slot2, catalog, out var second, out error))
        {
            return false;
        }

        slots = new AbilityRuntimeSlots(
            new AbilityRuntimeSlot(UniversalAbilitySlot.Slot1, prepared.Slot1, first),
            new AbilityRuntimeSlot(UniversalAbilitySlot.Slot2, prepared.Slot2, second));
        error = null;
        return true;
    }

    public bool TryGetSlot(UniversalAbilitySlot slot, out AbilityRuntimeSlot state)
    {
        state = default;
        if (slot == UniversalAbilitySlot.Slot1)
        {
            state = _slot1;
            return true;
        }

        if (slot == UniversalAbilitySlot.Slot2)
        {
            state = _slot2;
            return true;
        }

        return false;
    }

    private static bool TryResolve(AbilityId id, AbilityDefinitionCatalog catalog,
        out AbilityDefinition definition, out string error)
    {
        definition = null;
        error = null;
        if (!id.IsValid || catalog.TryGet(id, out definition))
        {
            return true;
        }

        error = $"Prepared Raid ability '{id}' is unknown to the authorized catalog.";
        return false;
    }
}
