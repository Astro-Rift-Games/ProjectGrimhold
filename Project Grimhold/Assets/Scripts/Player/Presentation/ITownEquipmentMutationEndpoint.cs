/// <summary>
/// Local Town capability for persistent Equipment intentions. Implementations own target-slot
/// resolution and the temporary Ready-state gate; presenters never mutate persistence directly.
/// </summary>
public interface ITownEquipmentMutationEndpoint
{
    bool CanMutate { get; }
    bool CanEquip(LootId lootId);
    StashOperationResult TryEquip(LootId lootId);
    StashOperationResult TryUnequip(EquipmentSlot slot);
}
