using UnityEngine;

/// <summary>
/// Local-only presentation for a Town NPC's five-part, six-direction base body.
/// </summary>
[DisallowMultipleComponent]
public sealed class TownNpcDirectionalView : MonoBehaviour
{
    [SerializeField] private CharacterVisualDirection _initialFacing = CharacterVisualDirection.South;
    [SerializeField] private SpriteRenderer _legsRenderer;
    [SerializeField] private SpriteRenderer _bodyRenderer;
    [SerializeField] private SpriteRenderer _headRenderer;
    [SerializeField] private SpriteRenderer _leftHandRenderer;
    [SerializeField] private SpriteRenderer _rightHandRenderer;
    [SerializeField] private DirectionalSpriteSet _legsSprites = new DirectionalSpriteSet();
    [SerializeField] private DirectionalSpriteSet _bodySprites = new DirectionalSpriteSet();
    [SerializeField] private DirectionalSpriteSet _headSprites = new DirectionalSpriteSet();
    [SerializeField] private DirectionalSpriteSet _leftHandSprites = new DirectionalSpriteSet();
    [SerializeField] private DirectionalSpriteSet _rightHandSprites = new DirectionalSpriteSet();

    // Match the player's authored base order and RightHand_Idle_NW sorting curve.
    private const int RightHandDefaultSortingOrder = 30;
    private const int RightHandNorthWestSortingOrder = -2;

    public CharacterVisualDirection InitialFacing => _initialFacing;
    public CharacterVisualDirection CurrentFacing { get; private set; }

    private void Awake()
    {
        if (_legsRenderer == null || _bodyRenderer == null || _headRenderer == null ||
            _leftHandRenderer == null || _rightHandRenderer == null ||
            _legsSprites == null || !_legsSprites.IsComplete ||
            _bodySprites == null || !_bodySprites.IsComplete ||
            _headSprites == null || !_headSprites.IsComplete ||
            _leftHandSprites == null || !_leftHandSprites.IsComplete ||
            _rightHandSprites == null || !_rightHandSprites.IsComplete)
        {
            Debug.LogError($"[{nameof(TownNpcDirectionalView)}] Missing required body renderer or directional sprites.", this);
            return;
        }

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

    private void SetFacing(CharacterVisualDirection facing)
    {
        CurrentFacing = facing;
        if (_legsRenderer != null && _legsSprites != null)
        {
            _legsRenderer.sprite = _legsSprites.GetSprite(facing);
        }
        if (_bodyRenderer != null && _bodySprites != null)
        {
            _bodyRenderer.sprite = _bodySprites.GetSprite(facing);
        }
        if (_headRenderer != null && _headSprites != null)
        {
            _headRenderer.sprite = _headSprites.GetSprite(facing);
        }
        if (_leftHandRenderer != null && _leftHandSprites != null)
        {
            _leftHandRenderer.sprite = _leftHandSprites.GetSprite(facing);
        }
        if (_rightHandRenderer != null && _rightHandSprites != null)
        {
            _rightHandRenderer.sprite = _rightHandSprites.GetSprite(facing);
            _rightHandRenderer.sortingOrder = facing == CharacterVisualDirection.NorthWest
                ? RightHandNorthWestSortingOrder
                : RightHandDefaultSortingOrder;
        }
    }
}
