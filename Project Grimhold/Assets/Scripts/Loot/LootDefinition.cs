using UnityEngine;

/// <summary>
/// Representa la definición estática y compartida de un tipo de loot.
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

    [SerializeField]
    private int _extractionValuePerUnit;

    [SerializeField]
    private int _sellValuePerUnit;

    [SerializeField]
    private int _defaultPickupQuantity = 1;

    [SerializeField]
    [Tooltip("Opcional. Configuración del consumible si este loot puede ser consumido.")]
    private ConsumableDefinition _consumableDefinition;

    public string Id => _id;
    public LootId LootId => new LootId(_id);
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public Sprite WorldSprite => _worldSprite;
    public LootCategory Category => _category;
    public LootRarity Rarity => _rarity;
    public int ExtractionValuePerUnit => _extractionValuePerUnit;
    public int SellValuePerUnit => _sellValuePerUnit;
    public int DefaultPickupQuantity => _defaultPickupQuantity;
    public ConsumableDefinition ConsumableDefinition => _consumableDefinition;

    private void OnValidate()
    {
        _extractionValuePerUnit = Mathf.Max(0, _extractionValuePerUnit);
        _sellValuePerUnit = Mathf.Max(0, _sellValuePerUnit);
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

        // Validar formato del ID (solo a-z, 0-9, y _)
        for (int i = 0; i < _id.Length; i++)
        {
            char c = _id[i];
            bool isValidChar = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
            if (!isValidChar)
            {
                error = $"Loot definition '{name}' has an invalid ID '{_id}'. Only lowercase letters, numbers, and underscores are allowed.";
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

        return true;
    }
}
