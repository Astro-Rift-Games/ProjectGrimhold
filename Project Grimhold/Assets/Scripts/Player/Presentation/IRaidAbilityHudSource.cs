using System;

/// <summary>One confirmed read of a slot: display facts plus the (Sequence, Phase) feedback identity.</summary>
internal struct RaidAbilityHudReading
{
    public AbilityDefinition Definition;
    public uint Sequence;
    public RaidAbilityHudSlotFacts Facts;
}

/// <summary>
/// Read-only seam between the ability HUD presenter and the confirmed ability runtime. It exists so
/// the presenter lifecycle can be tested without a Fusion runner.
/// </summary>
internal interface IRaidAbilityHudSource
{
    /// <summary>Raised only on the owning Input Authority peer.</summary>
    event Action<UniversalAbilitySlot, AbilityActivationFailure> ActivationRejected;

    /// <summary>Raised only on the owning Input Authority peer.</summary>
    event Action<UniversalAbilitySlot, AbilityExecutionStopReason> ExecutionInterrupted;

    /// <summary>Returns false while the runtime is not initialized or confirmed.</summary>
    bool TryRead(UniversalAbilitySlot slot, out RaidAbilityHudReading reading);
}
