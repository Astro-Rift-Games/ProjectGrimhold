/// <summary>
/// Categoría canónica y estructural para la clasificación de items y loot (Doc 14 §4 y Doc 09).
/// </summary>
public enum LootCategory
{
    None = 0,

    // Categorías no equipables estructurales (Doc 14)
    Resource = 1,
    Treasure = 2,
    Consumable = 3,
    Utility = 4,

    // Categorías de equipamiento (Doc 09)
    Weapon = 5,
    Helmet = 6,
    Armor = 7,
    Gloves = 8,
    Boots = 9,
    Shield = 10,

    // Aliases heredados para compatibilidad
    [System.Obsolete("Usar Resource en su lugar.")]
    Material = 1,
    [System.Obsolete("Usar Treasure en su lugar.")]
    Valuable = 2,
    [System.Obsolete("Categoría Quest deprecada; usar la propiedad MissionCompatible en su lugar.")]
    Quest = 11,
    [System.Obsolete("Usar Utility o la categoría específica en su lugar.")]
    Miscellaneous = 4
}
