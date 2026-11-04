/// <summary>
/// Resolves the total flat damage bonus of the gems installed in a weapon instance.
/// Like the instance scaling resolver, it is not an active runtime source until unique
/// Item Instances exist end to end.
/// </summary>
public static class WeaponGemDamageBonusResolver
{
    /// <summary>
    /// Sums every installed gem. Fails when an installed gem is not a catalog gem; a weapon
    /// without installed gems resolves to zero and does not require a catalog.
    /// </summary>
    public static bool TryResolve(
        in WeaponInstanceModifiers instance,
        LootDefinitionCatalog catalog,
        out int damageBonus)
    {
        damageBonus = 0;
        int total = 0;
        for (int index = 0; index < instance.GemSocketCount; index++)
        {
            if (!instance.TryGetGemSocket(index, out WeaponGemSocket socket) || !socket.IsOccupied)
            {
                continue;
            }

            if (!TryGetGem(socket.GemLootId, catalog, out GemDefinition gem))
            {
                return false;
            }

            total += gem.DamageBonus;
        }

        damageBonus = total;
        return true;
    }

    /// <summary>Resolves an installed gem's catalog definition, used for its presentation name.</summary>
    public static bool TryGetGemLoot(LootId gemLootId, LootDefinitionCatalog catalog, out LootDefinition loot)
    {
        loot = null;
        if (catalog == null || !catalog.TryGet(gemLootId.Value, out LootDefinition definition) ||
            definition == null || definition.GemDefinition == null)
        {
            return false;
        }

        loot = definition;
        return true;
    }

    private static bool TryGetGem(LootId gemLootId, LootDefinitionCatalog catalog, out GemDefinition gem)
    {
        gem = null;
        if (!TryGetGemLoot(gemLootId, catalog, out LootDefinition loot) ||
            !loot.GemDefinition.TryValidate(out _))
        {
            return false;
        }

        gem = loot.GemDefinition;
        return true;
    }
}
