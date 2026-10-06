using System;

/// <summary>
/// Adapts Town Abilities intentions to <see cref="LocalProfileStore"/>. It keeps the Ready gate
/// outside persistence: a blocked attempt never reaches the store, and the refresh callback runs
/// only after a persisted change.
/// </summary>
public sealed class TownAbilityMutationEndpoint : ITownAbilityMutationEndpoint
{
    private readonly LocalProfileStore _store;
    private readonly AbilityDefinitionCatalog _catalog;
    private readonly Func<bool> _canMutate;
    private readonly Action _onMutated;

    public TownAbilityMutationEndpoint(
        LocalProfileStore store,
        AbilityDefinitionCatalog catalog,
        Func<bool> canMutate,
        Action onMutated = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _canMutate = canMutate ?? throw new ArgumentNullException(nameof(canMutate));
        _onMutated = onMutated;
    }

    public bool CanMutate => _canMutate();

    public bool CanEquip(AbilityId abilityId, UniversalAbilitySlot slot)
    {
        if (!CanMutate || !PreparedAbilityLoadout.IsKnownSlot(slot) || !abilityId.IsValid ||
            !_catalog.TryGet(abilityId, out AbilityDefinition definition) || definition == null ||
            !_store.IsAbilityUnlocked(abilityId) || !_store.TryGetCharacterAttributeState(out CharacterAttributeState attributes) ||
            !definition.AreAttributeRequirementsSatisfiedBy(attributes))
        {
            return false;
        }

        UniversalAbilitySlot otherSlot = slot == UniversalAbilitySlot.Slot1
            ? UniversalAbilitySlot.Slot2
            : UniversalAbilitySlot.Slot1;
        return _store.GetPreparedAbilities().Get(otherSlot) != abilityId;
    }

    public TownAbilityMutationResult TryEquip(UniversalAbilitySlot slot, AbilityId abilityId)
    {
        if (!CanMutate)
        {
            return TownAbilityMutationResult.Blocked();
        }

        return Complete(_store.TrySetPreparedAbility(slot, abilityId));
    }

    public TownAbilityMutationResult TryClear(UniversalAbilitySlot slot)
    {
        if (!CanMutate)
        {
            return TownAbilityMutationResult.Blocked();
        }

        return Complete(_store.TryClearPreparedAbility(slot));
    }

    private TownAbilityMutationResult Complete(AbilityPreparationResult preparation)
    {
        TownAbilityMutationResult result = TownAbilityMutationResult.From(preparation);
        if (result.IsSuccess)
        {
            _onMutated?.Invoke();
        }

        return result;
    }
}
