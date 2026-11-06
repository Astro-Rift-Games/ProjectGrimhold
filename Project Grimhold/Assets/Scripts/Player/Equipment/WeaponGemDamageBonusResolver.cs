/// <summary>
/// Resolves the total flat damage bonus of the gems installed in a weapon instance.
/// Used by Town statistics, instance tooltips and equipment damage.
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
        return TryResolveCore(instance, catalog, null, out damageBonus);
    }

    /// <summary>Uses an explicit template lookup when the caller already owns one (Town statistics).</summary>
    public static bool TryResolveWithLookup(
        in WeaponInstanceModifiers instance,
        System.Func<LootId, LootDefinition> resolveLoot,
        out int damageBonus) => TryResolveCore(instance, null, resolveLoot, out damageBonus);

    private static bool TryResolveCore(
        in WeaponInstanceModifiers instance,
        LootDefinitionCatalog catalog,
        System.Func<LootId, LootDefinition> resolveLoot,
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

            LootDefinition loot = resolveLoot != null ? resolveLoot(socket.GemLootId) : null;
            GemDefinition gem;
            if (resolveLoot != null)
            {
                gem = loot != null ? loot.GemDefinition : null;
                if (gem == null || !gem.TryValidate(out _)) return false;
            }
            else if (!TryGetGem(socket.GemLootId, catalog, out gem))
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
