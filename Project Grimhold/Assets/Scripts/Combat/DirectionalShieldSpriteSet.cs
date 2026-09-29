using UnityEngine;

/// <summary>Held shield presentation sprites for the six visual directions; not gameplay configuration.</summary>
[CreateAssetMenu(fileName = "ShieldSpriteSet", menuName = "Grimhold/Combat/Directional Shield Sprite Set")]
public sealed class DirectionalShieldSpriteSet : ScriptableObject
{
    [SerializeField] private Sprite _spriteN;
    [SerializeField] private Sprite _spriteNE;
    [SerializeField] private Sprite _spriteNW;
    [SerializeField] private Sprite _spriteS;
    [SerializeField] private Sprite _spriteSE;
    [SerializeField] private Sprite _spriteSW;

    public bool IsComplete => _spriteN != null && _spriteNE != null &&
        _spriteNW != null && _spriteS != null && _spriteSE != null && _spriteSW != null;

    public bool TryValidate(out string error)
    {
        if (!IsComplete)
        {
            error = $"Directional shield sprite set '{name}' requires all six sprites.";
            return false;
        }

        error = null;
        return true;
    }

    public Sprite GetSprite(CharacterVisualDirection direction) => direction switch
    {
        CharacterVisualDirection.North => _spriteN,
        CharacterVisualDirection.NorthEast => _spriteNE,
        CharacterVisualDirection.NorthWest => _spriteNW,
        CharacterVisualDirection.South => _spriteS,
        CharacterVisualDirection.SouthEast => _spriteSE,
        CharacterVisualDirection.SouthWest => _spriteSW,
        _ => null
    };
}
