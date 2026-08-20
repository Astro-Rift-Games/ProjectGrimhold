using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct EquipmentSpriteMapping
{
    [Tooltip("El sprite base (por ejemplo, el frame actual del Body)")]
    public Sprite BaseSprite;

    [Tooltip("El sprite correspondiente para esta pieza de armadura")]
    public Sprite EquipmentSprite;
}

[System.Serializable]
public class EquipmentVisualDefinition
{
    [SerializeField]
    [Tooltip("Si es true, se usará el mismo sprite base como placeholder y se le aplicará un tinte.")]
    private bool _usesBaseSpritesAsPlaceholder = true;

    [SerializeField]
    [Tooltip("El color/tinte que se aplicará si se usa como placeholder. Para arte final, dejar en blanco (blanco sin transparencia).")]
    private Color _tint = Color.white;

    [SerializeField]
    [Tooltip("Mapeo explícito de los sprites base a los sprites reales de esta pieza de equipo.")]
    private List<EquipmentSpriteMapping> _spriteMappings = new List<EquipmentSpriteMapping>();

    public Color Tint => _tint;
    public bool UsesBaseSpritesAsPlaceholder => _usesBaseSpritesAsPlaceholder;

    public EquipmentVisualDefinition()
    {
    }

    public EquipmentVisualDefinition(Color tint)
    {
        _tint = tint;
        _usesBaseSpritesAsPlaceholder = true;
    }

    /// <summary>
    /// Resuelve el sprite que debe mostrar la armadura en función del sprite actual de la parte base.
    /// </summary>
    public Sprite ResolveSprite(Sprite baseSprite)
    {
        if (_usesBaseSpritesAsPlaceholder)
        {
            return baseSprite;
        }

        if (_spriteMappings != null && baseSprite != null)
        {
            for (int i = 0; i < _spriteMappings.Count; i++)
            {
                if (ReferenceEquals(_spriteMappings[i].BaseSprite, baseSprite))
                {
                    return _spriteMappings[i].EquipmentSprite;
                }
            }
        }

        // Si no es placeholder y no se encontró el mapeo, se retorna nulo para que la armadura no muestre un frame erróneo
        return null;
    }

    public bool TryValidate(out string error)
    {
        error = null;

        if (!_usesBaseSpritesAsPlaceholder)
        {
            if (_spriteMappings == null || _spriteMappings.Count == 0)
            {
                error = "EquipmentVisualDefinition no está marcado como placeholder, pero no tiene mapeos de sprites configurados.";
                return false;
            }

            for (int i = 0; i < _spriteMappings.Count; i++)
            {
                if (_spriteMappings[i].BaseSprite == null || _spriteMappings[i].EquipmentSprite == null)
                {
                    error = $"EquipmentVisualDefinition tiene un mapeo inválido en el índice {i}. Faltan sprites.";
                    return false;
                }
            }
        }

        return true;
    }
}
