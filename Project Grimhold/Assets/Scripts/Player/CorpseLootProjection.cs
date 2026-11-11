using System;
using System.Collections.Generic;

/// <summary>
/// Projects the exact expedition snapshot into corpse content. Doc 09 §15.2: a training weapon is
/// never generated as corpse loot on Definitive Defeat or Abandon; it returns to the Trainer, so it
/// is still cleared from the player but never loaded into the container.
/// </summary>
public static class CorpseLootProjection
{
    public static void ExcludeTrainingWeapons(
        IReadOnlyList<LootEntry> loot,
        IReadOnlyList<RaidLootOriginEntry> origins,
        IReadOnlyList<ExpeditionWeaponUnit> weaponUnits,
        LootDefinitionCatalog catalog,
        out IReadOnlyList<LootEntry> corpseLoot,
        out IReadOnlyList<RaidLootOriginEntry> corpseOrigins,
        out IReadOnlyList<ExpeditionWeaponUnit> corpseWeaponUnits)
    {
        if (loot == null) throw new ArgumentNullException(nameof(loot));
        if (origins == null) throw new ArgumentNullException(nameof(origins));
        if (weaponUnits == null) throw new ArgumentNullException(nameof(weaponUnits));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        var keptLoot = new List<LootEntry>(loot.Count);
        foreach (LootEntry entry in loot)
            if (!IsTrainingWeapon(entry.LootId, catalog)) keptLoot.Add(entry);

        var keptOrigins = new List<RaidLootOriginEntry>(origins.Count);
        foreach (RaidLootOriginEntry entry in origins)
            if (!IsTrainingWeapon(entry.LootId, catalog)) keptOrigins.Add(entry);

        var keptUnits = new List<ExpeditionWeaponUnit>(weaponUnits.Count);
        foreach (ExpeditionWeaponUnit unit in weaponUnits)
            if (!IsTrainingWeapon(unit.LootId, catalog)) keptUnits.Add(unit);

        corpseLoot = keptLoot;
        corpseOrigins = keptOrigins;
        corpseWeaponUnits = keptUnits;
    }

    private static bool IsTrainingWeapon(LootId lootId, LootDefinitionCatalog catalog) =>
        lootId.IsValid && catalog.TryGet(lootId.Value, out LootDefinition definition) &&
        definition != null && definition.IsTrainingWeapon;
}
