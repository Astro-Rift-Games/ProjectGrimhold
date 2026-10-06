/// <summary>
/// Town-facing ability preparation mutations. Implementations enforce that changes are only
/// allowed while the player is Not Ready and delegate every persistent change to the profile store.
/// </summary>
public interface ITownAbilityMutationEndpoint
{
    /// <summary>True while Town allows ability changes (player Not Ready).</summary>
    bool CanMutate { get; }

    /// <summary>
    /// Checks the Ready gate and the preparation rules (unlocked, requirements, no duplicate)
    /// without mutating anything.
    /// </summary>
    bool CanEquip(AbilityId abilityId, UniversalAbilitySlot slot);

    TownAbilityMutationResult TryEquip(UniversalAbilitySlot slot, AbilityId abilityId);

    TownAbilityMutationResult TryClear(UniversalAbilitySlot slot);
}
