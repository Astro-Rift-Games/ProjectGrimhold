using UnityEngine;

public readonly struct DragPayload
{
    public readonly LootId LootId;
    public readonly DragSlotLocation Source;
    public readonly EquipmentSlot EquipmentSlot;
    public readonly Sprite Icon;
    public readonly int Amount;
    public readonly LootCategory Category;
    public readonly WeaponHandedness WeaponHandedness;

    // default(struct) must expose the legacy -1/-1 pair, not physical address/revision zero.
    private readonly int? _sourceInventorySlotIndex;
    private readonly int? _expectedInventoryRevision;
    public int SourceInventorySlotIndex => _sourceInventorySlotIndex ?? -1;
    public int ExpectedInventoryRevision => _expectedInventoryRevision ?? -1;
    public bool HasInventorySlotAddress => SourceInventorySlotIndex != -1 || ExpectedInventoryRevision != -1;
    private readonly int? _sourceUnitIndex;
    public int SourceUnitIndex => _sourceUnitIndex ?? -1;
    public readonly WeaponInstanceModifiersNetworkData SourceUnitModifiers;
    public bool HasSourceUnitAddress => SourceUnitIndex != -1;

    public bool IsValid => LootId.IsValid && Source != DragSlotLocation.None &&
        LootTransferRequest.IsValidInventorySource(SourceInventorySlotIndex, ExpectedInventoryRevision) &&
        (!HasInventorySlotAddress || Source == DragSlotLocation.Inventory) &&
        (!HasSourceUnitAddress || (Source == DragSlotLocation.Container && !HasInventorySlotAddress &&
            LootTransferRequest.IsValidSourceUnitAddress(SourceUnitIndex)));

    public static DragPayload Empty => default;

    public DragPayload(
        LootId lootId,
        DragSlotLocation source,
        EquipmentSlot equipmentSlot,
        Sprite icon,
        int amount,
        LootCategory category,
        WeaponHandedness weaponHandedness)
        : this(lootId, source, equipmentSlot, icon, amount, category, weaponHandedness, -1, -1) { }

    public DragPayload(
        LootId lootId, DragSlotLocation source, EquipmentSlot equipmentSlot, Sprite icon,
        int amount, LootCategory category, WeaponHandedness weaponHandedness,
        int sourceInventorySlotIndex, int expectedInventoryRevision,
        int sourceUnitIndex = -1, WeaponInstanceModifiersNetworkData sourceUnitModifiers = default)
    {
        _sourceUnitIndex = sourceUnitIndex;
        SourceUnitModifiers = sourceUnitModifiers;
        _sourceInventorySlotIndex = sourceInventorySlotIndex;
        _expectedInventoryRevision = expectedInventoryRevision;
        LootId = lootId;
        Source = source;
        EquipmentSlot = equipmentSlot;
        Icon = icon;
        Amount = amount;
        Category = category;
        WeaponHandedness = weaponHandedness;
    }

    public static DragPayload Create(RaidInventorySlotData data, DragSlotLocation source, EquipmentSlot equipmentSlot)
    {
        return new DragPayload(
            data.LootId,
            source,
            equipmentSlot,
            data.Icon,
            data.Amount,
            data.Category,
            data.WeaponHandedness,
            data.SourceInventorySlotIndex,
            data.ExpectedInventoryRevision,
            data.SourceUnitIndex,
            data.SourceUnitModifiers
        );
    }
}
