using Fusion;

/// <summary>Current simulation context, not a second owner of mutable execution state.</summary>
public readonly struct AbilityExecutionContext
{
    public NetworkRunner Runner { get; }
    public PlayerCharacter Character { get; }
    public UniversalAbilitySlot Slot { get; }
    public AbilityDefinition Definition { get; }
    public CharacterAttributeState Attributes { get; }

    internal AbilityExecutionContext(NetworkRunner runner, PlayerCharacter character,
        UniversalAbilitySlot slot, AbilityDefinition definition, in CharacterAttributeState attributes)
    {
        Runner = runner;
        Character = character;
        Slot = slot;
        Definition = definition;
        Attributes = attributes;
    }
}
