using Fusion;
using UnityEngine;

/// <summary>
/// Continuously composes the local hand and weapon visuals around
/// the player's synchronized facing direction.
///
/// This presentation component owns no gameplay or networked state. It reads
/// <see cref="IMovementState.FacingDirection"/> and derives a local visual pose
/// for the player and all proxies without modifying the body Animator.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponPresenter : MonoBehaviour
{
    private const int SortingOrderFront = 10;
    private const int SortingOrderBack = -10;

    [Header("References")]
    [SerializeField]
    private MonoBehaviour _movementStateSource;

    [SerializeField]
    private Transform _handPivot;

    [SerializeField]
    private Transform _handOrbitAnchor;

    [SerializeField]
    private Transform _handVisual;

    [SerializeField]
    private SpriteRenderer _handSpriteRenderer;

    [SerializeField]
    private Transform _weaponVisual;

    [SerializeField]
    private SpriteRenderer _weaponSpriteRenderer;

    [Header("Hand Orbit")]
    [SerializeField]
    private Vector2 _handOrbit = new Vector2(0.3f, 0.2f);

    [SerializeField]
    private Vector2 _weaponStanceOffset;

    [SerializeField]
    private Vector2 _handVisualOffset;

    [Header("Grip and Orientation")]
    [SerializeField]
    private Vector2 _weaponGripPoint;

    [SerializeField]
    private float _handAngleCorrection;

    [SerializeField]
    private float _weaponAngleCorrection;

    private IMovementState _movementState;
    private NetworkBehaviour _movementNetworkBehaviour;
    private Vector2 _safeFacing = Vector2.down;
    private Vector3 _handPivotBaseScale;
    private Vector3 _handVisualBaseScale;
    private Vector3 _weaponVisualBaseScale;
    private bool _hasCapturedBaseState;

    private void Awake()
    {
        CacheDependencies();
        CaptureBaseState();
    }

    private void OnEnable()
    {
        CacheDependencies();
        CaptureBaseState();

        if (!ValidateDependencies())
        {
            enabled = false;
            return;
        }

        _handSpriteRenderer.enabled = true;
        _weaponSpriteRenderer.enabled = true;
        ApplyPose();
    }

    private void LateUpdate()
    {
        _handSpriteRenderer.enabled = _weaponSpriteRenderer.enabled;
        if (!_weaponSpriteRenderer.enabled)
        {
            return;
        }

        ApplyPose();
    }

    private void ApplyPose()
    {
        Vector2 facing = CanReadMovementState()
            ? _movementState.FacingDirection
            : _safeFacing;
        _safeFacing = PlayerWeaponPresentationMath.ResolveSafeFacing(
            facing,
            _safeFacing);

        Vector2 anchorLocalPosition =
            PlayerWeaponPresentationMath.CalculateAnchorLocalPosition(
                _handOrbitAnchor,
                _handPivot.parent);
        Vector2 handPosition = PlayerWeaponPresentationMath.CalculateHandPosition(
            anchorLocalPosition,
            _safeFacing,
            _handOrbit,
            _weaponStanceOffset);
        float facingAngle =
            PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(_safeFacing);
        bool mirrored = PlayerWeaponPresentationMath.ShouldMirror(_safeFacing);

        _handPivot.localPosition = new Vector3(
            handPosition.x,
            handPosition.y,
            _handPivot.localPosition.z);
        _handPivot.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
        _handPivot.localScale = new Vector3(
            _handPivotBaseScale.x,
            Mathf.Abs(_handPivotBaseScale.y) * (mirrored ? -1f : 1f),
            _handPivotBaseScale.z);

        _handVisual.localPosition = new Vector3(
            _handVisualOffset.x,
            _handVisualOffset.y,
            _handVisual.localPosition.z);
        _handVisual.localRotation = Quaternion.Euler(0f, 0f, _handAngleCorrection);
        _handVisual.localScale = _handVisualBaseScale;

        Vector2 weaponPosition =
            PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                _weaponGripPoint,
                new Vector2(_weaponVisualBaseScale.x, _weaponVisualBaseScale.y),
                _weaponAngleCorrection);
        _weaponVisual.localPosition = new Vector3(
            weaponPosition.x,
            weaponPosition.y,
            _weaponVisual.localPosition.z);
        _weaponVisual.localRotation = Quaternion.Euler(0f, 0f, _weaponAngleCorrection);
        _weaponVisual.localScale = _weaponVisualBaseScale;

        int sortingOrder = PlayerWeaponPresentationMath.CalculateWeaponSortingOrder(
            _safeFacing,
            SortingOrderFront,
            SortingOrderBack);
        _handSpriteRenderer.sortingOrder = sortingOrder + 1;
        _weaponSpriteRenderer.sortingOrder = sortingOrder;
    }

    private void CacheDependencies()
    {
        _movementState = _movementStateSource as IMovementState;
        _movementNetworkBehaviour = _movementStateSource as NetworkBehaviour;
    }

    private bool CanReadMovementState()
    {
        return _movementNetworkBehaviour == null
            || (_movementNetworkBehaviour.Object != null
                && _movementNetworkBehaviour.Object.IsValid);
    }

    private void CaptureBaseState()
    {
        if (_hasCapturedBaseState
            || _handPivot == null
            || _handVisual == null
            || _weaponVisual == null)
        {
            return;
        }

        _handPivotBaseScale = _handPivot.localScale;
        _handVisualBaseScale = _handVisual.localScale;
        _weaponVisualBaseScale = _weaponVisual.localScale;
        _hasCapturedBaseState = true;
    }

    private bool ValidateDependencies()
    {
        if (_movementState != null
            && _handPivot != null
            && _handPivot.parent != null
            && _handOrbitAnchor != null
            && _handVisual != null
            && _handSpriteRenderer != null
            && _weaponVisual != null
            && _weaponSpriteRenderer != null
            && _hasCapturedBaseState)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(PlayerWeaponPresenter)} on '{name}' requires a movement state, "
            + "hand pivot with a parent, hand orbit anchor, hand visual, weapon visual, "
            + "and both sprite renderers.",
            this);
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
