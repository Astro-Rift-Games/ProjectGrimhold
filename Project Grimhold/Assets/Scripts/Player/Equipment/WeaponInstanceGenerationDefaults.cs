using System.Collections.Generic;

/// <summary>
/// Provisional Dungeon weapon-variant generation values (TASK-283 T8) until Balance owns them
/// (Doc 09 section 8.1). One deterministic variant per weapon unit, derived from the chest seed.
/// </summary>
public static class WeaponInstanceGenerationDefaults
{
    private static readonly string[] GemPool = { "RES_ATTUNEMENT_GEM", "RES_RUBY_GEM", "RES_SAPPHIRE_GEM" };

    /// <summary>
    /// Builds the provisional rules. Variants require every gem of the pool in the catalog; a catalog
    /// without them (for example a reduced test catalog) simply generates plain units.
    /// </summary>
    public static bool TryGetRules(LootDefinitionCatalog catalog, out WeaponInstanceGenerationRules rules)
    {
        rules = default;
        if (catalog == null)
        {
            return false;
        }

        var pool = new List<LootId>(GemPool.Length);
        foreach (string gemId in GemPool)
        {
            if (!catalog.TryGet(gemId, out LootDefinition gem) || gem == null || gem.GemDefinition == null)
            {
                return false;
            }

            pool.Add(gem.LootId);
        }

        return WeaponInstanceGenerationRules.TryCreate(
            primaryScalingChanceBasisPoints: 4000,
            secondaryScalingChanceBasisPoints: 1000,
            gradeWeights: new[] { 40, 30, 18, 8, 3, 1 },
            gemSocketCountWeights: new[] { 80, 17, 3 },
            gemInstallChanceBasisPoints: 2500,
            gemPool: pool.ToArray(),
            out rules,
            out _);
    }

    /// <summary>Independent seed per unit so every weapon of a stack rolls its own variant.</summary>
    public static ulong UnitSeed(ulong containerSeed, int catalogIndex, int unitIndex)
    {
        unchecked
        {
            ulong value = containerSeed ^ ((ulong)(catalogIndex + 1) * 0x9E3779B97F4A7C15UL) ^
                          ((ulong)(unitIndex + 1) * 0xC2B2AE3D27D4EB4FUL);
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    public static bool TryGenerateUnit(
        LootDefinitionCatalog catalog,
        in WeaponInstanceGenerationRules rules,
        int catalogIndex,
        ulong containerSeed,
        int unitIndex,
        out WeaponInstanceModifiersNetworkData modifiers,
        out string error)
    {
        modifiers = default;
        error = null;
        if (!catalog.TryGetByIndex(catalogIndex, out LootDefinition definition) ||
            !WeaponInstanceGenerator.TryGenerate(
                definition, UnitSeed(containerSeed, catalogIndex, unitIndex), rules,
                out WeaponInstanceModifiers variant, out error))
        {
            error ??= "The weapon template is not in the catalog.";
            return false;
        }

        return WeaponInstanceModifiersNetworkData.TryCreate(
            variant, definition.WeaponDefinition.NaturalScalingAttribute, catalog, out modifiers, out error);
    }
}
