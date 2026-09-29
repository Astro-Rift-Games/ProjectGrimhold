using UnityEngine;

/// <summary>
/// Local-only presentation for a Town NPC's six static facing sprites.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownNpcDirectionalView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private CharacterVisualDirection _initialFacing = CharacterVisualDirection.South;
    [SerializeField] private Sprite _north;
    [SerializeField] private Sprite _northEast;
    [SerializeField] private Sprite _northWest;
    [SerializeField] private Sprite _south;
    [SerializeField] private Sprite _southEast;
    [SerializeField] private Sprite _southWest;

    public CharacterVisualDirection InitialFacing => _initialFacing;
    public CharacterVisualDirection CurrentFacing { get; private set; }

    private void Awake()
    {
        RestoreInitialFacing();
    }

    private void OnDisable()
    {
        RestoreInitialFacing();
    }

    public void FaceTarget(Vector3 targetPosition)
    {
        Vector2 direction = targetPosition - transform.position;
        CharacterVisualDirection facing = CharacterVisualDirectionResolver.Resolve(
            direction,
            CharacterVisualDirectionResolver.GetCanonicalVector(_initialFacing));
        SetFacing(facing);
    }

    public void RestoreInitialFacing()
    {
        SetFacing(_initialFacing);
    }

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

    private void SetFacing(CharacterVisualDirection facing)
    {
        CurrentFacing = facing;
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = GetSprite(facing);
        }
    }
}
