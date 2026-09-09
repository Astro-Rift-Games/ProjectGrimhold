/// <summary>
/// Local Town capability for persistent Equipment intentions. Implementations own target-slot
/// validation and the temporary Ready-state gate; presenters never mutate persistence directly.
/// </summary>
public interface ITownEquipmentMutationEndpoint
{
    bool CanMutate { get; }
    bool CanEquip(LootId lootId, EquipmentSlot slot);
    StashOperationResult TryEquip(LootId lootId, EquipmentSlot slot);
    StashOperationResult TryUnequip(EquipmentSlot slot);
}
