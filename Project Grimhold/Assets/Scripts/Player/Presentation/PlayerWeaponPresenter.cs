using UnityEngine;

/// <summary>
/// Presents the active Weapon Set without owning attack motion.
/// The Animator moves the hands; held visuals inherit those transforms through their grips.
/// The single Main Hand weapon visual follows MainHandGrip, or WeaponPose when the weapon drives its own pose.
/// During the weapon's confirmed attack clip, an optional attack sprite animation swaps only its sprite.
/// The Off Hand shield shows its sprite for the visual direction in every pose.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponPresenter : MonoBehaviour
{
    private const int SortingOrderFront = 20;
    private const int SortingOrderBack = -10;
    // A weapon-driven weapon is held by the left hand, which draws over it in front facings: over the weapon (20)
    // and its attack VFX (21), under the main hand (30). The hand's glove follows one slot above.
    internal const int WeaponPoseHandSortingOrderFront = 25;

    [Header("References")]
    [SerializeField] private PlayerAnimatorView _animatorView;
    [SerializeField] private PlayerWeaponEquipmentNetworkController _equipmentSource;
    [SerializeField] private Transform _mainHandGrip;
    [SerializeField] private Transform _weaponPose;
    [SerializeField] private SpriteRenderer _weaponPoseHandRenderer;
    [SerializeField] private Transform _mainHandWeaponPivot;
    [SerializeField] private Transform _mainHandWeaponVisual;
    [SerializeField] private SpriteRenderer _mainHandRenderer;
    [SerializeField] private Transform _offHandGrip;
    [SerializeField] private Transform _offHandVisual;
    [SerializeField] private SpriteRenderer _offHandRenderer;

    private LootDefinition _mainHandDefinition;
    private LootDefinition _offHandDefinition;
    private Sprite _mainHandWorldSprite;
    private Sprite _offHandWorldSprite;
    private DirectionalShieldSpriteSet _offHandDirectionalSprites;
    private WeaponAttackSpriteAnimation _mainHandAttackSprites;
    private bool _hasMainHandWeapon;
    private Vector2 _mainHandGripPoint;
    private float _mainHandAngleCorrection;
    private Vector3 _mainHandWeaponPivotBaseScale;
    private Vector3 _mainHandWeaponVisualBaseScale;
    private int _weaponPoseHandBaseSortingOrder;
    private bool _weaponDriven;
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
        _mainHandWorldSprite = null;
        _offHandWorldSprite = null;
        _offHandDirectionalSprites = null;
        _mainHandAttackSprites = null;
        _hasMainHandWeapon = false;
        SetWeaponDriven(false);
        SetRendererSprite(_mainHandRenderer, null);
        SetRendererSprite(_offHandRenderer, null);
    }

    private void LateUpdate()
    {
        RefreshEquipment(force: false);
        RefreshMainHandSprite();
        RefreshPose();
    }

    private void RefreshEquipment(bool force)
    {
        LootDefinition mainHand = null;
        LootDefinition offHand = null;

        if (CanReadEquipmentState())
        {
            if (!_animatorView.TryGetPresentedAttackWeapon(out mainHand))
            {
                _equipmentSource.TryGetEquippedDefinition(out mainHand);
            }

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
        _mainHandWorldSprite = weapon != null ? definition.WorldSprite ?? definition.Icon : null;
        _mainHandAttackSprites = weapon != null ? weapon.Presentation.AttackSpriteAnimation : null;
        SetRendererSprite(_mainHandRenderer, _mainHandWorldSprite);
        WeaponRig rig = weapon != null ? weapon.Presentation.Rig : WeaponRig.HandHeld;
        AttachMainHandWeapon(rig);
        SetWeaponDriven(rig == WeaponRig.WeaponDriven);
        _hasMainHandWeapon = weapon != null;

        if (weapon == null)
        {
            _mainHandWeaponVisual.localPosition = Vector3.zero;
            _mainHandWeaponVisual.localRotation = Quaternion.identity;
            _mainHandWeaponVisual.localScale = _mainHandWeaponVisualBaseScale;
            return;
        }

        _mainHandGripPoint = weapon.Presentation.GripPoint;
        _mainHandAngleCorrection = weapon.Presentation.AngleCorrection;
        ApplyMainHandVisualPose(_mainHandWeaponPivot.localScale.y < 0f);
    }

    // The grip stays on the pivot for either facing; a mirrored pivot takes the correction resolved for the
    // weapon's own art axis.
    private void ApplyMainHandVisualPose(bool mirrored)
    {
        if (!_hasMainHandWeapon)
        {
            return;
        }

        float angleCorrection = PlayerWeaponPresentationMath.ResolveAngleCorrection(
            _mainHandAngleCorrection,
            mirrored);
        Vector2 gripAlignedPosition =
            PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                _mainHandGripPoint,
                new Vector2(
                    _mainHandWeaponVisualBaseScale.x,
                    _mainHandWeaponVisualBaseScale.y),
                angleCorrection);

        _mainHandWeaponVisual.localPosition = new Vector3(
            gripAlignedPosition.x,
            gripAlignedPosition.y,
            _mainHandWeaponVisual.localPosition.z);
        _mainHandWeaponVisual.localRotation = Quaternion.Euler(
            0f,
            0f,
            angleCorrection);
    }

    // Moves the one weapon visual hierarchy under the transform that owns its pose; RefreshPose then resets
    // its local pose.
    private void AttachMainHandWeapon(WeaponRig rig)
    {
        Transform owner = rig == WeaponRig.WeaponDriven ? _weaponPose : _mainHandGrip;
        if (_mainHandWeaponPivot.parent != owner)
        {
            _mainHandWeaponPivot.SetParent(owner, false);
        }
    }

    // Only while a weapon-driven weapon is presented does the presenter own the holding hand's sorting; leaving
    // that rig restores the hand's authored order once, so clips of hand-held weapons keep animating it.
    private void SetWeaponDriven(bool weaponDriven)
    {
        if (_weaponDriven == weaponDriven)
        {
            return;
        }

        _weaponDriven = weaponDriven;
        if (!weaponDriven && _weaponPoseHandRenderer != null)
        {
            _weaponPoseHandRenderer.sortingOrder = _weaponPoseHandBaseSortingOrder;
        }
    }

    private void ApplyOffHandDefinition(LootDefinition definition)
    {
        bool isShield = definition != null && definition.Category == LootCategory.Shield;
        _offHandWorldSprite = isShield ? definition.WorldSprite ?? definition.Icon : null;
        _offHandDirectionalSprites = isShield ? definition.DefenseSprites : null;
        SetRendererSprite(_offHandRenderer, _offHandWorldSprite);

        _offHandVisual.localPosition = Vector3.zero;
        _offHandVisual.localRotation = Quaternion.identity;
    }

    // Swaps only the sprite, so the pose, grip, angle correction, facing and mirror stay untouched.
    private void RefreshMainHandSprite()
    {
        float attackSeconds = 0f;
        bool isAttacking = _mainHandAttackSprites != null &&
            _animatorView.TryGetPresentedAttackSeconds(out attackSeconds);
        Sprite sprite = PlayerWeaponPresentationMath.ResolveMainHandSprite(
            _mainHandWorldSprite,
            _mainHandAttackSprites,
            isAttacking,
            attackSeconds);
        if (_mainHandRenderer.sprite != sprite)
        {
            _mainHandRenderer.sprite = sprite;
        }
    }

    // Swaps only the sprite; the Animator poses the hand and OffHandGrip carries the shield.
    private void RefreshOffHandSprite(CharacterVisualDirection direction)
    {
        Sprite sprite = PlayerWeaponPresentationMath.ResolveOffHandSprite(
            _offHandWorldSprite,
            _offHandDirectionalSprites,
            direction);
        if (_offHandRenderer.sprite != sprite)
        {
            _offHandRenderer.sprite = sprite;
        }
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
        ApplyMainHandVisualPose(mirrored);

        CharacterVisualDirection direction = CharacterVisualDirectionResolver.Resolve(facing);
        int order = CharacterVisualDirectionResolver.CalculateSortingOrder(
            direction,
            SortingOrderFront,
            SortingOrderBack);

        _mainHandRenderer.sortingOrder = order;
        // The weapon-pose hand is the LeftHand renderer that also carries OffHandGrip.
        _offHandRenderer.sortingOrder = PlayerWeaponPresentationMath.ResolveOffHandSortingOrder(
            CharacterVisualDirectionResolver.IsFrontFacing(direction),
            SortingOrderFront,
            SortingOrderBack,
            _weaponPoseHandRenderer.sortingOrder);
        RefreshOffHandSprite(direction);
        if (_weaponDriven)
        {
            _weaponPoseHandRenderer.sortingOrder = CharacterVisualDirectionResolver.CalculateSortingOrder(
                direction,
                WeaponPoseHandSortingOrderFront,
                _weaponPoseHandBaseSortingOrder);
        }
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
        _weaponPoseHandBaseSortingOrder = _weaponPoseHandRenderer != null ? _weaponPoseHandRenderer.sortingOrder : 0;
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
            _weaponPose != null &&
            _weaponPoseHandRenderer != null &&
            !_weaponPose.IsChildOf(_mainHandGrip) &&
            !_weaponPose.IsChildOf(_offHandGrip) &&
            _mainHandWeaponPivot != null &&
            _mainHandWeaponVisual != null &&
            _mainHandRenderer != null &&
            _offHandGrip != null &&
            _offHandVisual != null &&
            _offHandRenderer != null &&
            _hasCapturedBaseState &&
            (_mainHandWeaponPivot.parent == _mainHandGrip ||
                _mainHandWeaponPivot.parent == _weaponPose) &&
            _mainHandWeaponVisual.parent == _mainHandWeaponPivot &&
            _mainHandRenderer.transform == _mainHandWeaponVisual &&
            _offHandVisual.IsChildOf(_offHandGrip))
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(PlayerWeaponPresenter)} on '{name}' requires animator, Equipment, " +
            "a weapon pose outside the hand grips, " +
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
