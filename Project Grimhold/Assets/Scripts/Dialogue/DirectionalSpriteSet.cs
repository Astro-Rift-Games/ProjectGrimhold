using System;
using UnityEngine;

/// <summary>
/// Six static sprites for one modular character body part.
/// </summary>
[Serializable]
public sealed class DirectionalSpriteSet
{
    [SerializeField] private Sprite _north;
    [SerializeField] private Sprite _northEast;
    [SerializeField] private Sprite _northWest;
    [SerializeField] private Sprite _south;
    [SerializeField] private Sprite _southEast;
    [SerializeField] private Sprite _southWest;

    public bool IsComplete =>
        _north != null && _northEast != null && _northWest != null &&
        _south != null && _southEast != null && _southWest != null;

    public Sprite GetSprite(CharacterVisualDirection facing)
    {
        switch (facing)
        {
            case CharacterVisualDirection.North: return _north;
            case CharacterVisualDirection.NorthEast: return _northEast;
            case CharacterVisualDirection.NorthWest: return _northWest;
            case CharacterVisualDirection.South: return _south;
            case CharacterVisualDirection.SouthEast: return _southEast;
            case CharacterVisualDirection.SouthWest: return _southWest;
            default: return null;
        }
    }
}
