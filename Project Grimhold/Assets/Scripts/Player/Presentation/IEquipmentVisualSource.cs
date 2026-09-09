/// <summary>
/// Read-only Equipment projection used by the shared modular armor presenter.
/// Implementations may expose Raid simulation state or Town presentation state, but the
/// presenter never mutates Equipment or activates weapon gameplay.
/// </summary>
public interface IEquipmentVisualSource
{
    int ObservedEquipmentRevision { get; }

    bool TryGetSlotDefinition(EquipmentSlot slot, out LootDefinition definition);
}
