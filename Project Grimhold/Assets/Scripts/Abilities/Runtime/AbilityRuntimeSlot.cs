/// <summary>Read-only, locally resolved configuration for one universal Raid ability slot.</summary>
public readonly struct AbilityRuntimeSlot
{
    public UniversalAbilitySlot Slot { get; }
    public AbilityId AbilityId { get; }
    public AbilityDefinition Definition { get; }
    public bool IsPrepared => AbilityId.IsValid && Definition != null;

    internal AbilityRuntimeSlot(UniversalAbilitySlot slot, AbilityId abilityId, AbilityDefinition definition)
    {
        Slot = slot;
        AbilityId = abilityId;
        Definition = definition;
    }
}
