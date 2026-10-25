/// <summary>Read-only prepared Equipment observed for one Town member.</summary>
public interface IPreparedEquipmentReader
{
    int ObservedEquipmentRevision { get; }
    bool TryGetPreparedSlotDefinition(EquipmentSlot slot, out LootDefinition definition);
}
