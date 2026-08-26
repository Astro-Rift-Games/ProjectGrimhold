/// <summary>Resolves the provisional progression Value assigned to one Raid loot type.</summary>
public interface IRaidLootValueSource
{
    bool TryGetValuePerUnit(LootId lootId, out long valuePerUnit);
}
