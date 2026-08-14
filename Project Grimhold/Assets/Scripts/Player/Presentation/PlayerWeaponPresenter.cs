using Fusion;
using UnityEngine;

/// <summary>
/// Continuously composes the local weapon visual around
/// the player's synchronized facing direction.
///
/// This presentation component owns no gameplay or networked state. It reads
/// <see cref="IMovementState.FacingDirection"/> and derives a local visual pose
/// for the weapon for the player and all proxies without modifying the body Animator.
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
    private Transform _weaponPivot;

    [SerializeField]
    private Transform _weaponOrbitAnchor;

    [SerializeField]
    private Transform _weaponVisual;

    [SerializeField]
    private SpriteRenderer _weaponSpriteRenderer;

    [Header("Weapon Orbit Presets")]
    [SerializeField]
    private WeaponDirectionPresetTable _directionPresets;

    [SerializeField]
    private Vector2 _weaponStanceOffset;

    [Header("Grip and Orientation")]
    [SerializeField]
    private Vector2 _weaponGripPoint;

    [SerializeField]
    private float _weaponAngleCorrection;

    private IMovementState _movementState;
    private NetworkBehaviour _movementNetworkBehaviour;
    private Vector2 _safeFacing = Vector2.down;
    private Vector3 _weaponPivotBaseScale;
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

        _weaponSpriteRenderer.enabled = true;
        ApplyPose();
    }

    private void OnDisable()
    {
        _safeFacing = Vector2.down;
    }

    private void LateUpdate()
    {
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
        _safeFacing = CharacterVisualDirectionResolver.SanitizeFacing(
            facing,
            _safeFacing);

        CharacterVisualDirection visualDirection =
            CharacterVisualDirectionResolver.Resolve(_safeFacing);
        Vector2 canonicalFacing =
            CharacterVisualDirectionResolver.GetCanonicalVector(visualDirection);
        WeaponDirectionPreset preset =
            _directionPresets.GetPreset(visualDirection);

        Vector2 anchorLocalPosition =
            PlayerWeaponPresentationMath.CalculateAnchorLocalPosition(
                _weaponOrbitAnchor,
                _weaponPivot.parent);
        Vector2 finalStanceOffset = preset.StanceOffset + _weaponStanceOffset;
        Vector2 weaponPivotPosition = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(
            anchorLocalPosition,
            canonicalFacing,
            preset.Orbit,
            finalStanceOffset);
        float facingAngle =
            PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(_safeFacing);
        bool mirrored = PlayerWeaponPresentationMath.ShouldMirror(_safeFacing);

        _weaponPivot.localPosition = new Vector3(
            weaponPivotPosition.x,
            weaponPivotPosition.y,
            _weaponPivot.localPosition.z);
        _weaponPivot.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
        _weaponPivot.localScale = new Vector3(
            _weaponPivotBaseScale.x,
            Mathf.Abs(_weaponPivotBaseScale.y) * (mirrored ? -1f : 1f),
            _weaponPivotBaseScale.z);

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

        int sortingOrder = CharacterVisualDirectionResolver.CalculateSortingOrder(
            visualDirection,
            SortingOrderFront,
            SortingOrderBack);
        _weaponSpriteRenderer.sortingOrder = sortingOrder;
    }

    private void CacheDependencies()
    {
        if (_movementStateSource == null)
        {
            _movementStateSource = GetComponentInParent<PlayerMovementNetworkController>();
        }

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
            || _weaponPivot == null
            || _weaponVisual == null)
        {
            return;
        }

        _weaponPivotBaseScale = _weaponPivot.localScale;
        _weaponVisualBaseScale = _weaponVisual.localScale;
        _hasCapturedBaseState = true;
    }

    private bool ValidateDependencies()
    {
        if (_movementState != null
            && _weaponPivot != null
            && _weaponPivot.parent != null
            && _weaponOrbitAnchor != null
            && _weaponVisual != null
            && _weaponSpriteRenderer != null
            && _hasCapturedBaseState)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(PlayerWeaponPresenter)} on '{name}' requires a movement state, "
            + "weapon pivot with a parent, weapon orbit anchor, weapon visual, "
            + "and weapon sprite renderer.",
            this);
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif

    [System.Serializable]
    internal struct WeaponDirectionPreset
    {
        [SerializeField] private Vector2 _orbit;
        [SerializeField] private Vector2 _stanceOffset;

        public Vector2 Orbit
        {
            get => _orbit;
            set => _orbit = value;
        }

        public Vector2 StanceOffset
        {
            get => _stanceOffset;
            set => _stanceOffset = value;
        }

        public WeaponDirectionPreset(Vector2 orbit, Vector2 stanceOffset)
        {
            _orbit = orbit;
            _stanceOffset = stanceOffset;
        }
    }

    [System.Serializable]
    internal struct WeaponDirectionPresetTable
    {
        [SerializeField] private WeaponDirectionPreset _south;
        [SerializeField] private WeaponDirectionPreset _southEast;
        [SerializeField] private WeaponDirectionPreset _northEast;
        [SerializeField] private WeaponDirectionPreset _north;
        [SerializeField] private WeaponDirectionPreset _northWest;
        [SerializeField] private WeaponDirectionPreset _southWest;

        public WeaponDirectionPreset South { get => _south; set => _south = value; }
        public WeaponDirectionPreset SouthEast { get => _southEast; set => _southEast = value; }
        public WeaponDirectionPreset NorthEast { get => _northEast; set => _northEast = value; }
        public WeaponDirectionPreset North { get => _north; set => _north = value; }
        public WeaponDirectionPreset NorthWest { get => _northWest; set => _northWest = value; }
        public WeaponDirectionPreset SouthWest { get => _southWest; set => _southWest = value; }

        public WeaponDirectionPreset GetPreset(CharacterVisualDirection direction)
        {
            switch (direction)
            {
                case CharacterVisualDirection.South: return _south;
                case CharacterVisualDirection.SouthEast: return _southEast;
                case CharacterVisualDirection.NorthEast: return _northEast;
                case CharacterVisualDirection.North: return _north;
                case CharacterVisualDirection.NorthWest: return _northWest;
                case CharacterVisualDirection.SouthWest: return _southWest;
                default: return _south;
            }
        }
    }
}
