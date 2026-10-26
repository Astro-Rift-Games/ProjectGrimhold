using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa la definición estática y canónica de un tipo de item o loot (Doc 14 y Doc 09).
/// Esta clase es puramente de configuración y no contiene estado mutable en runtime.
/// </summary>
[CreateAssetMenu(fileName = "LootDefinition", menuName = "Grimhold/Loot/Loot Definition")]
public sealed class LootDefinition : ScriptableObject
{
    [SerializeField]
    private string _id;

    [SerializeField]
    private string _displayName;

    [SerializeField, TextArea]
    private string _description;

    [SerializeField]
    private Sprite _icon;

    [SerializeField]
    private Sprite _worldSprite;

    [SerializeField]
    private LootCategory _category;

    [SerializeField]
    private LootRarity _rarity;

    [Header("ItemDefinition Canonical Properties (Doc 14)")]
    [SerializeField]
    [Tooltip("Valor canónico unificado del objeto según Doc 14 (entero base no negativo).")]
    private int _objectValue;

    [SerializeField]
    [Tooltip("Tamaño máximo de apilamiento en inventario (1 para no apilables, hasta 999).")]
    private int _stackMax = 1;

    [SerializeField]
    [Tooltip("Límite máximo transportable simultáneamente por el jugador (0 = sin límite especial).")]
    private int _maxTransportable = 0;

    [SerializeField]
    [Tooltip("Subcategoría o familia taxonómica (ej: Restos de Criatura, Componentes Arcanos, Maldito, Caos, etc.).")]
    private string _family;

    [SerializeField]
    [Tooltip("Etiquetas operacionales o descriptivas.")]
    private string[] _tags;

    [Header("Operational Lifecycle Flags (Doc 14 §5)")]
    [SerializeField]
    [Tooltip("¿El objeto puede comprarse o venderse con comerciantes?")]
    private bool _tradable = true;

    [SerializeField]
    [Tooltip("¿El objeto puede transferirse a cofres, contenedores o soltarse a otros jugadores?")]
    private bool _canBeTransferred = true;

    [SerializeField]
    [Tooltip("¿El objeto puede ser ingresado a la dungeon desde el alijo/loadout?")]
    private bool _canEnterDungeon = true;

    [SerializeField]
    [Tooltip("¿El objeto puede generarse o encontrarse dentro de la dungeon?")]
    private bool _canBeFoundInDungeon = true;

    [SerializeField]
    [Tooltip("¿El objeto puede ser descartado/arrojado al suelo?")]
    private bool _canBeDiscarded = true;

    [SerializeField]
    [Tooltip("¿El objeto tiene una acción ejecutable de uso?")]
    private bool _canBeUsed;

    [SerializeField]
    [Tooltip("¿El objeto es compatible o contabiliza para objetivos de misiones (sustituto del tag Quest)?")]
    private bool _missionCompatible;

    [SerializeField]
    [Tooltip("Modalidad de uso (Instant, Channeled, AimedQuickSlot).")]
    private ItemUseMode _useMode = ItemUseMode.None;

    [Header("Legacy & Derived Values")]
    [SerializeField]
    private int _extractionValuePerUnit;

    [SerializeField]
    private int _sellValuePerUnit;

    [SerializeField]
    [Tooltip("Gold paid per unit when buying this loot from a Merchant. Independent from extraction value.")]
    private int _buyValuePerUnit;

    [SerializeField]
    private int _defaultPickupQuantity = 1;

    [SerializeField]
    [Tooltip("Opcional. Configuración del consumible si este loot puede ser consumido.")]
    private ConsumableDefinition _consumableDefinition;

    [SerializeField]
    [Tooltip("Opcional. Configuración funcional cuando este loot es un arma equipable.")]
    private WeaponDefinition _weaponDefinition;

    [SerializeField]
    [Tooltip("Optional functional configuration when this loot is an equippable shield.")]
    private ShieldDefinition _shieldDefinition;

    [SerializeField]
    [Tooltip("Opcional. Configuración funcional cuando este loot es una pieza de armadura equipable.")]
    private ArmorDefinition _armorDefinition;

    [SerializeField]
    [Tooltip("Opcional. Configuración visual cuando este loot es una armadura o equipo visualizable.")]
    private EquipmentVisualDefinition _equipmentVisualDefinition;

    [SerializeField]
    [Tooltip("Optional held shield presentation sprites for each visual direction.")]
    private DirectionalShieldSpriteSet _defenseSprites;

    [SerializeField]
    [Tooltip("Whether this definition can be assigned to a Quick Slot. Requires a ConsumableDefinition or CanBeUsed flag.")]
    private bool _quickSlotCompatible;

    public string Id => _id;
    public LootId LootId => new LootId(_id);
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public Sprite WorldSprite => _worldSprite;
    public LootCategory Category => _category;
    public LootRarity Rarity => _rarity;
    public int ObjectValue => _objectValue;
    public int StackMax => _stackMax;
    public int MaxTransportable => _maxTransportable;
    public string Family => _family;
    public IReadOnlyList<string> Tags => _tags ?? System.Array.Empty<string>();
    public bool Tradable => _tradable;
    public bool CanBeTransferred => _canBeTransferred;
    public bool CanEnterDungeon => _canEnterDungeon;
    public bool CanBeFoundInDungeon => _canBeFoundInDungeon;
    public bool CanBeDiscarded => _canBeDiscarded;
    public bool CanBeUsed => _canBeUsed;
    public bool MissionCompatible => _missionCompatible;
    public ItemUseMode UseMode => _useMode;
    public int ExtractionValuePerUnit => _extractionValuePerUnit;
    public int SellValuePerUnit => _sellValuePerUnit;
    public int BuyValuePerUnit => _buyValuePerUnit;
    public int DefaultPickupQuantity => _defaultPickupQuantity;
    public ConsumableDefinition ConsumableDefinition => _consumableDefinition;
    public WeaponDefinition WeaponDefinition => _weaponDefinition;
    public ShieldDefinition ShieldDefinition => _shieldDefinition;
    public ArmorDefinition ArmorDefinition => _armorDefinition;
    public EquipmentVisualDefinition EquipmentVisualDefinition => _equipmentVisualDefinition;
    public DirectionalShieldSpriteSet DefenseSprites => _defenseSprites;
    public bool QuickSlotCompatible => _quickSlotCompatible;

    private void OnValidate()
    {
        _objectValue = Mathf.Max(0, _objectValue);
        _stackMax = Mathf.Max(1, _stackMax);
        _maxTransportable = Mathf.Max(0, _maxTransportable);
        _extractionValuePerUnit = Mathf.Max(0, _extractionValuePerUnit);
        _sellValuePerUnit = Mathf.Max(0, _sellValuePerUnit);
        _buyValuePerUnit = Mathf.Max(0, _buyValuePerUnit);
        _defaultPickupQuantity = Mathf.Max(1, _defaultPickupQuantity);
    }

    /// <summary>
    /// Valida que la definición de loot tenga una configuración consistente y válida.
    /// </summary>
    /// <param name="error">Mensaje descriptivo del error en caso de que falle la validación.</param>
    /// <returns>True si es válida, de lo contrario false.</returns>
    public bool TryValidate(out string error)
    {
        error = null;

        if (string.IsNullOrEmpty(_id))
        {
            error = $"Loot definition on asset '{name}' has an empty or null ID.";
            return false;
        }

        // Validar formato del ID (A-Z, a-z, 0-9, y _)
        for (int i = 0; i < _id.Length; i++)
        {
            char c = _id[i];
            bool isValidChar = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
            if (!isValidChar)
            {
                error = $"Loot definition '{name}' has an invalid ID '{_id}'. Only alphanumeric characters (A-Z, a-z, 0-9) and underscores are allowed.";
                return false;
            }
        }

        if (string.IsNullOrEmpty(_displayName))
        {
            error = $"Loot definition '{_id}' has an empty display name.";
            return false;
        }

        if (_category == LootCategory.None)
        {
            error = $"Loot definition '{_id}' must have a category other than None.";
            return false;
        }

        if ((int)_category == 11)
        {
            error = $"Loot definition '{_id}' uses deprecated category Quest; use MissionCompatible flag instead.";
            return false;
        }

        if (_objectValue < 0)
        {
            error = $"Loot definition '{_id}' has a negative ObjectValue: {_objectValue}.";
            return false;
        }

        if (_stackMax < 1)
        {
            error = $"Loot definition '{_id}' has a StackMax less than 1: {_stackMax}.";
            return false;
        }

        if (_maxTransportable < 0)
        {
            error = $"Loot definition '{_id}' has a negative MaxTransportable: {_maxTransportable}.";
            return false;
        }

        if (_category == LootCategory.Treasure && _stackMax > 1)
        {
            error = $"Loot definition '{_id}' is a Treasure and must not be stackable (StackMax must be 1, but is {_stackMax}).";
            return false;
        }

        if (_extractionValuePerUnit < 0)
        {
            error = $"Loot definition '{_id}' has a negative extraction value: {_extractionValuePerUnit}.";
            return false;
        }

        if (_sellValuePerUnit < 0)
        {
            error = $"Loot definition '{_id}' has a negative sell value: {_sellValuePerUnit}.";
            return false;
        }

        if (_buyValuePerUnit < 0)
        {
            error = $"Loot definition '{_id}' has a negative buy value: {_buyValuePerUnit}.";
            return false;
        }

        if (_defaultPickupQuantity < 1)
        {
            error = $"Loot definition '{_id}' has a default pickup quantity less than 1: {_defaultPickupQuantity}.";
            return false;
        }

        if (_worldSprite == null)
        {
            error = $"Loot definition '{_id}' lacks a valid World Sprite reference.";
            return false;
        }

        if (_consumableDefinition != null && !_consumableDefinition.TryValidate(out string consumableError))
        {
            error = $"Loot definition '{_id}' has an invalid ConsumableDefinition: {consumableError}";
            return false;
        }

        if (_quickSlotCompatible && _consumableDefinition == null && !_canBeUsed)
        {
            error = $"Loot definition '{_id}' is Quick Slot compatible but has neither ConsumableDefinition nor CanBeUsed enabled.";
            return false;
        }

        if (_category == LootCategory.Weapon)
        {
            if (_weaponDefinition == null)
            {
                error = $"Loot definition '{_id}' is a Weapon but has no WeaponDefinition.";
                return false;
            }

            if (!_weaponDefinition.TryValidate(out string weaponError))
            {
                error = $"Loot definition '{_id}' has an invalid WeaponDefinition: {weaponError}";
                return false;
            }

            if (_armorDefinition != null)
            {
                error = $"Loot definition '{_id}' is a Weapon but also has an ArmorDefinition.";
                return false;
            }

            if (_shieldDefinition != null)
            {
                error = $"Loot definition '{_id}' is a Weapon but also has a ShieldDefinition.";
                return false;
            }
        }
        else if (_weaponDefinition != null)
        {
            error = $"Loot definition '{_id}' has a WeaponDefinition but its category is {_category}.";
            return false;
        }
        else if (_category == LootCategory.Shield)
        {
            if (_shieldDefinition == null)
            {
                error = $"Loot definition '{_id}' is a Shield but has no ShieldDefinition.";
                return false;
            }

            if (!_shieldDefinition.TryValidate(out string shieldError))
            {
                error = $"Loot definition '{_id}' has an invalid ShieldDefinition: {shieldError}";
                return false;
            }

            if (_armorDefinition != null)
            {
                error = $"Loot definition '{_id}' is a Shield but also has an ArmorDefinition.";
                return false;
            }
        }
        else if (_shieldDefinition != null)
        {
            error = $"Loot definition '{_id}' has a ShieldDefinition but its category is {_category}.";
            return false;
        }
        else if (IsArmorCategory(_category))
        {
            if (_armorDefinition == null)
            {
                error = $"Loot definition '{_id}' is {_category} but has no ArmorDefinition.";
                return false;
            }

            if (!_armorDefinition.TryValidate(out string armorError))
            {
                error = $"Loot definition '{_id}' has an invalid ArmorDefinition: {armorError}";
                return false;
            }
        }
        else if (_armorDefinition != null)
        {
            error = $"Loot definition '{_id}' has an ArmorDefinition but its category is {_category}.";
            return false;
        }

        if (_defenseSprites != null)
        {
            if (_category != LootCategory.Shield)
            {
                error = $"Loot definition '{_id}' has defense sprites but its category is {_category}.";
                return false;
            }

            if (!_defenseSprites.TryValidate(out string defenseSpritesError))
            {
                error = $"Loot definition '{_id}' has invalid defense sprites: {defenseSpritesError}";
                return false;
            }
        }

        if (_equipmentVisualDefinition != null && !_equipmentVisualDefinition.TryValidate(out string equipError))
        {
            error = $"Loot definition '{_id}' has an invalid EquipmentVisualDefinition: {equipError}";
            return false;
        }

        return true;
    }

    private static bool IsArmorCategory(LootCategory category) =>
        category == LootCategory.Helmet ||
        category == LootCategory.Armor ||
        category == LootCategory.Gloves ||
        category == LootCategory.Boots;
}
