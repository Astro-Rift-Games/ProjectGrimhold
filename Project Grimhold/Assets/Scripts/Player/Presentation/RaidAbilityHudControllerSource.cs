using System;

/// <summary>Adapts the confirmed ability runtime and resource owners to <see cref="IRaidAbilityHudSource"/>.</summary>
internal sealed class RaidAbilityHudControllerSource : IRaidAbilityHudSource
{
    private readonly PlayerAbilityRuntimeNetworkController _abilities;
    private readonly PlayerManaNetworkController _mana;
    private readonly PlayerStaminaNetworkController _stamina;

    public RaidAbilityHudControllerSource(
        PlayerAbilityRuntimeNetworkController abilities,
        PlayerManaNetworkController mana,
        PlayerStaminaNetworkController stamina)
    {
        _abilities = abilities;
        _mana = mana;
        _stamina = stamina;
    }

    public event Action<UniversalAbilitySlot, AbilityActivationFailure> ActivationRejected
    {
        add => _abilities.ActivationRejected += value;
        remove => _abilities.ActivationRejected -= value;
    }

    public event Action<UniversalAbilitySlot, AbilityExecutionStopReason> ExecutionInterrupted
    {
        add => _abilities.ExecutionInterrupted += value;
        remove => _abilities.ExecutionInterrupted -= value;
    }

    public bool TryRead(UniversalAbilitySlot slot, out RaidAbilityHudReading reading)
    {
        reading = default;
        if (_abilities == null ||
            !_abilities.TryGetSlot(slot, out AbilityRuntimeSlot runtimeSlot) ||
            !_abilities.TryGetExecutionSnapshot(slot, out AbilityExecutionSnapshot snapshot))
        {
            return false;
        }

        AbilityDefinition definition = runtimeSlot.IsPrepared ? runtimeSlot.Definition : null;
        reading.Definition = definition;
        reading.Sequence = snapshot.Sequence;
        reading.Facts.IsPrepared = definition != null;
        reading.Facts.Phase = snapshot.Phase;
        if (definition == null)
        {
            return true;
        }

        reading.Facts.IsOnCooldown = _abilities.IsOnCooldown(slot);
        reading.Facts.RemainingCooldownSeconds = _abilities.GetRemainingCooldownSeconds(slot);
        reading.Facts.TotalCooldownSeconds = definition.CooldownSeconds;
        reading.Facts.Resource = definition.Resource;
        reading.Facts.Cost = definition.Cost;
        ReadResource(definition.Resource, ref reading.Facts);
        return true;
    }

    private void ReadResource(AbilityResourceType resource, ref RaidAbilityHudSlotFacts facts)
    {
        switch (resource)
        {
            case AbilityResourceType.Mana:
                if (_mana != null && _mana.IsInitialized && _mana.TryGetMaximumMana(out _))
                {
                    facts.HasResourceReading = true;
                    facts.AvailableResource = _mana.CurrentMana;
                }

                break;
            case AbilityResourceType.Stamina:
                if (_stamina != null && _stamina.TryGetMaximumStamina(out _))
                {
                    facts.HasResourceReading = true;
                    facts.AvailableResource = _stamina.CurrentStamina;
                }

                break;
        }
    }
}
