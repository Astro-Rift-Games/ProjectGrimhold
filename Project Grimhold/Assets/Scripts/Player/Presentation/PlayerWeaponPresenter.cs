using UnityEngine;

/// <summary>
/// Presents the active Weapon Set without owning attack motion.
/// The Animator moves the hands; held visuals inherit those transforms through their grips.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponPresenter : MonoBehaviour
{
    private const int SortingOrderFront = 20;
    private const int SortingOrderBack = -10;

    [Header("References")]
    [SerializeField] private PlayerAnimatorView _animatorView;
    [SerializeField] private PlayerWeaponEquipmentNetworkController _equipmentSource;
    [SerializeField] private Transform _mainHandGrip;
    [SerializeField] private Transform _mainHandWeaponPivot;
    [SerializeField] private Transform _mainHandWeaponVisual;
    [SerializeField] private SpriteRenderer _mainHandRenderer;
    [SerializeField] private Transform _offHandGrip;
    [SerializeField] private Transform _offHandVisual;
    [SerializeField] private SpriteRenderer _offHandRenderer;

    private LootDefinition _mainHandDefinition;
    private LootDefinition _offHandDefinition;
    private Vector3 _mainHandWeaponPivotBaseScale;
    private Vector3 _mainHandWeaponVisualBaseScale;
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

        RefreshEquipment(force: true);
        RefreshPose();
    }

    private void OnDisable()
    {
        _mainHandDefinition = null;
        _offHandDefinition = null;
        SetRendererSprite(_mainHandRenderer, null);
        SetRendererSprite(_offHandRenderer, null);
    }

    private void LateUpdate()
    {
        RefreshEquipment(force: false);
        RefreshPose();
    }

    private void RefreshEquipment(bool force)
    {
        LootDefinition mainHand = null;
        LootDefinition offHand = null;

        if (CanReadEquipmentState())
        {
            _equipmentSource.TryGetEquippedDefinition(out mainHand);

            WeaponSetSlot activeSet = _equipmentSource.ActiveWeaponSetSlot;
            EquipmentSlot offHandSlot = EquipmentSlotRules.GetOffHandSlot(activeSet);
            if (offHandSlot != EquipmentSlot.None)
            {
                _equipmentSource.TryGetSlotDefinition(offHandSlot, out offHand);
            }
        }

        if (force || !ReferenceEquals(_mainHandDefinition, mainHand))
        {
            _mainHandDefinition = mainHand;
            ApplyMainHandDefinition(mainHand);
        }

        if (force || !ReferenceEquals(_offHandDefinition, offHand))
        {
            _offHandDefinition = offHand;
            ApplyOffHandDefinition(offHand);
        }
    }

    private void ApplyMainHandDefinition(LootDefinition definition)
    {
        WeaponDefinition weapon = definition != null ? definition.WeaponDefinition : null;
        SetRendererSprite(
            _mainHandRenderer,
            weapon != null ? definition.WorldSprite ?? definition.Icon : null);

        if (weapon == null)
        {
            _mainHandWeaponVisual.localPosition = Vector3.zero;
            _mainHandWeaponVisual.localRotation = Quaternion.identity;
            _mainHandWeaponVisual.localScale = _mainHandWeaponVisualBaseScale;
            return;
        }

        WeaponDefinition.PresentationConfig presentation = weapon.Presentation;
        Vector2 gripAlignedPosition =
            PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                presentation.GripPoint,
                new Vector2(
                    _mainHandWeaponVisualBaseScale.x,
                    _mainHandWeaponVisualBaseScale.y),
                presentation.AngleCorrection);

        _mainHandWeaponVisual.localPosition = new Vector3(
            gripAlignedPosition.x,
            gripAlignedPosition.y,
            _mainHandWeaponVisual.localPosition.z);
        _mainHandWeaponVisual.localRotation = Quaternion.Euler(
            0f,
            0f,
            presentation.AngleCorrection);
    }

    private void ApplyOffHandDefinition(LootDefinition definition)
    {
        bool isShield = definition != null && definition.Category == LootCategory.Shield;
        SetRendererSprite(
            _offHandRenderer,
            isShield ? definition.WorldSprite ?? definition.Icon : null);

        _offHandVisual.localPosition = Vector3.zero;
        _offHandVisual.localRotation = Quaternion.identity;
    }

    private void RefreshPose()
    {
        Vector2 facing = _animatorView.VisualFacingDirection;
        float facingAngle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing);
        bool mirrored = PlayerWeaponPresentationMath.ShouldMirror(facing);

        _mainHandWeaponPivot.localPosition = Vector3.zero;
        _mainHandWeaponPivot.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
        _mainHandWeaponPivot.localScale = new Vector3(
            _mainHandWeaponPivotBaseScale.x,
            Mathf.Abs(_mainHandWeaponPivotBaseScale.y) * (mirrored ? -1f : 1f),
            _mainHandWeaponPivotBaseScale.z);

        CharacterVisualDirection direction = CharacterVisualDirectionResolver.Resolve(facing);
        int order = CharacterVisualDirectionResolver.CalculateSortingOrder(
            direction,
            SortingOrderFront,
            SortingOrderBack);

        _mainHandRenderer.sortingOrder = order;
        _offHandRenderer.sortingOrder = order;
    }

    private void CacheDependencies()
    {
        if (_animatorView == null)
        {
            Transform parent = transform.parent;
            _animatorView = parent != null
                ? parent.GetComponentInChildren<PlayerAnimatorView>(true)
                : GetComponent<PlayerAnimatorView>();
        }

        _equipmentSource ??= GetComponentInParent<PlayerWeaponEquipmentNetworkController>();
    }

    private void CaptureBaseState()
    {
        if (_hasCapturedBaseState ||
            _mainHandWeaponPivot == null ||
            _mainHandWeaponVisual == null)
        {
            return;
        }

        _mainHandWeaponPivotBaseScale = _mainHandWeaponPivot.localScale;
        _mainHandWeaponVisualBaseScale = _mainHandWeaponVisual.localScale;
        _hasCapturedBaseState = true;
    }

    private bool CanReadEquipmentState()
    {
        return _equipmentSource != null &&
            _equipmentSource.Object != null &&
            _equipmentSource.Object.IsValid;
    }

    private bool ValidateDependencies()
    {
        if (_animatorView != null &&
            _equipmentSource != null &&
            _mainHandGrip != null &&
            _mainHandWeaponPivot != null &&
            _mainHandWeaponVisual != null &&
            _mainHandRenderer != null &&
            _offHandGrip != null &&
            _offHandVisual != null &&
            _offHandRenderer != null &&
            _hasCapturedBaseState &&
            _mainHandWeaponPivot.parent == _mainHandGrip &&
            _mainHandWeaponVisual.parent == _mainHandWeaponPivot &&
            _mainHandRenderer.transform == _mainHandWeaponVisual &&
            _offHandVisual.IsChildOf(_offHandGrip))
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(PlayerWeaponPresenter)} on '{name}' requires animator, Equipment, " +
            "and held visuals parented beneath their matching hand grips.",
            this);
        return false;
    }

    private static void SetRendererSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sprite = sprite;
        renderer.enabled = sprite != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
