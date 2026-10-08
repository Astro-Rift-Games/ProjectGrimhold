using UnityEngine;

/// <summary>
/// Presents the active Weapon Set without owning attack motion.
/// The Animator moves the hands; held visuals inherit those transforms through their grips.
/// The single Main Hand weapon visual follows MainHandGrip, or WeaponPose when the weapon drives its own pose.
/// During the weapon's confirmed attack clip, an optional attack sprite animation swaps only its sprite.
/// The Off Hand shield shows its sprite for the visual direction in every pose.
/// While the player is Downed the held visuals are hidden and any attack VFX is cancelled. They return
/// when Downed ends only if the player is still alive, so a definitive defeat keeps them hidden.
/// A free-aim weapon keeps that rig and adds only a residual turn: the Animator plays the authored hands for the
/// aim's six-direction bucket, and the weapon pivot, which stays attached to the holding hand's grip, rotates about
/// that grip by the angle between the aim and the bucket. It runs after PlayerAnimatorView, whose attack timeline
/// re-evaluates the Animator in its own LateUpdate.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(FreeAimExecutionOrder)]
public sealed class PlayerWeaponPresenter : MonoBehaviour
{
    // Later than PlayerAnimatorView (order 0), which may call Animator.Update inside its LateUpdate.
    internal const int FreeAimExecutionOrder = 100;
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
    private WeaponAimMode _mainHandAimMode;
    private Vector2 _aimStanceTorsoPivot;
    private float _aimStanceOutwardOffset;
    private Transform _leftHandPivot;
    private Transform _rightHandPivot;
    private Vector3 _leftHandPivotBasePosition;
    private Quaternion _leftHandPivotBaseRotation;
    private Vector3 _rightHandPivotBasePosition;
    private Quaternion _rightHandPivotBaseRotation;
    private bool _aimBlockApplied;
    private bool _hasCapturedBaseState;
    private PlayerDownedStateNetworkController _downedState;
    private CharacterBase _character;
    private PlayerAttackVfxPresenter _attackVfx;
    private bool _hiddenByDowned;

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
        _mainHandAimMode = WeaponAimMode.BakedFacing;
        ReleaseAimBlock();
        _hiddenByDowned = false;
        SetWeaponDriven(false);
        SetRendererSprite(_mainHandRenderer, null);
        SetRendererSprite(_offHandRenderer, null);
    }

    private void LateUpdate()
    {
        RefreshDownedVisibility();
        RefreshEquipment(force: false);
        RefreshMainHandSprite();
        RefreshPose();
    }

    // Presentation only: observes the networked Downed state. Visibility combines with the equipment
    // sprite (an empty slot stays hidden) instead of forcing the renderers on.
    private void RefreshDownedVisibility()
    {
        bool isDowned = _downedState != null &&
            _downedState.Object != null &&
            _downedState.Object.IsValid &&
            _downedState.IsDowned;
        bool hidden = _hiddenByDowned;
        if (isDowned)
        {
            hidden = true;
        }
        else if (_hiddenByDowned && _character != null && _character.IsAlive)
        {
            hidden = false;
        }

        if (hidden == _hiddenByDowned)
        {
            return;
        }

        _hiddenByDowned = hidden;
        if (hidden && _attackVfx != null)
        {
            _attackVfx.CancelAndRestore();
        }

        ApplyRendererVisibility(_mainHandRenderer, _mainHandWorldSprite);
        ApplyRendererVisibility(_offHandRenderer, _offHandWorldSprite);
    }

    private void ApplyRendererVisibility(SpriteRenderer renderer, Sprite worldSprite)
    {
        if (renderer != null)
        {
            renderer.enabled = worldSprite != null && !_hiddenByDowned;
        }
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
        _mainHandAimMode = weapon != null ? weapon.Presentation.AimMode : WeaponAimMode.BakedFacing;
        _aimStanceTorsoPivot = weapon != null ? weapon.AimStanceTorsoPivot : Vector2.zero;
        _aimStanceOutwardOffset = weapon != null ? weapon.AimStanceOutwardOffset : 0f;
        ReleaseAimBlock();

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
        // The aim is the smoothed one the view also uses for the body facing; the residual is measured from the
        // bucket actually shown, however it was chosen.
        bool followsAim = _hasMainHandWeapon && FreeAimResidualPolicy.Applies(
            _mainHandAimMode, _animatorView.HasPresentedAim, _animatorView.IsAimDriven);
        PoseWeapon(facing, followsAim, followsAim ? _animatorView.PresentedAimDirection : facing);
    }

    private void PoseWeapon(Vector2 facing, bool followsAim, Vector2 aim)
    {
        float facingAngle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing);
        bool mirrored = PlayerWeaponPresentationMath.ShouldMirror(facing);
        // The bucket shown owns the authored hands; the residual, measured from that bucket whichever way it was
        // chosen, only turns the weapon about the holding hand's grip.
        // The wand turns about its holding hand by the residual (the free arc). An aim-stance weapon keeps its baked
        // local pose and the whole arm turns rigidly about the torso pivot instead.
        bool rigid = followsAim && _mainHandAimMode == WeaponAimMode.AimStance;
        float pivotAngle = followsAim && !rigid
            ? facingAngle + FreeAimResidual.AngleDegrees(facing, aim)
            : facingAngle;

        _mainHandWeaponPivot.localPosition = Vector3.zero;
        _mainHandWeaponPivot.localRotation = Quaternion.Euler(0f, 0f, pivotAngle);
        _mainHandWeaponPivot.localScale = new Vector3(
            _mainHandWeaponPivotBaseScale.x,
            Mathf.Abs(_mainHandWeaponPivotBaseScale.y) * (mirrored ? -1f : 1f),
            _mainHandWeaponPivotBaseScale.z);
        ApplyMainHandVisualPose(mirrored);
        if (rigid)
        {
            ApplyAimBlock(FreeAimResidual.AngleDegrees(facing, aim), aim);
        }
        else
        {
            ReleaseAimBlock();
        }

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

        RefreshAttackVfxPivot(followsAim, mirrored, CharacterVisualDirectionResolver.IsFrontFacing(direction));
    }

    // After the Animator: the hand pivots carry no curves, so they are set absolutely; WeaponPose is animated, so its
    // freshly evaluated pose is read and rewritten. Everything turns about the weapon's torso pivot by the residual.
    private void ApplyAimBlock(float residual, Vector2 aim)
    {
        if (_leftHandPivot == null || _rightHandPivot == null)
        {
            return;
        }

        Vector2 outward = AimBlockRotation.Outward(aim, _aimStanceOutwardOffset, _animatorView.AimStanceBlend);
        SetBlockMember(_leftHandPivot, _leftHandPivotBasePosition, _leftHandPivotBaseRotation, residual, outward);
        SetBlockMember(_rightHandPivot, _rightHandPivotBasePosition, _rightHandPivotBaseRotation, residual, outward);
        if (_weaponDriven)
        {
            SetBlockMember(_weaponPose, _weaponPose.localPosition, _weaponPose.localRotation, residual, outward);
        }

        _aimBlockApplied = true;
    }

    private void SetBlockMember(
        Transform member, Vector3 basePosition, Quaternion baseRotation, float residual, Vector2 outward)
    {
        AimBlockRotation.Apply(
            basePosition,
            baseRotation.eulerAngles.z,
            _aimStanceTorsoPivot,
            residual,
            outward,
            out Vector2 position,
            out float rotation);
        member.localPosition = new Vector3(position.x, position.y, basePosition.z);
        member.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    // Leaving the stance hands the hand pivots back at their rest pose; the Animator rewrites WeaponPose itself.
    private void ReleaseAimBlock()
    {
        if (!_aimBlockApplied)
        {
            return;
        }

        _aimBlockApplied = false;
        if (_leftHandPivot != null)
        {
            _leftHandPivot.SetLocalPositionAndRotation(_leftHandPivotBasePosition, _leftHandPivotBaseRotation);
        }

        if (_rightHandPivot != null)
        {
            _rightHandPivot.SetLocalPositionAndRotation(_rightHandPivotBasePosition, _rightHandPivotBaseRotation);
        }
    }

    // The attack VFX of a free-aim weapon anchors to the hand-held pivot, in the Animator root space it lives in.
    private void RefreshAttackVfxPivot(bool followsAim, bool mirrored, bool frontFacing)
    {
        if (_attackVfx == null)
        {
            return;
        }

        if (!followsAim)
        {
            _attackVfx.ClearFreeAimPivot();
            return;
        }

        Transform visualRoot = _animatorView.transform;
        Vector3 anchor = visualRoot.InverseTransformPoint(_mainHandWeaponPivot.position);
        Vector3 axis = visualRoot.InverseTransformDirection(_mainHandWeaponPivot.right);
        _attackVfx.SetFreeAimPivot(
            new Vector2(anchor.x, anchor.y),
            Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg,
            mirrored,
            frontFacing);
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
        Transform root = _equipmentSource != null ? _equipmentSource.transform : transform.root;
        _downedState ??= root.GetComponent<PlayerDownedStateNetworkController>();
        _character ??= root.GetComponent<CharacterBase>();
        _attackVfx ??= root.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
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
        CaptureHandPivots();
        _hasCapturedBaseState = true;
    }

    private void CaptureHandPivots()
    {
        _leftHandPivot = _offHandGrip != null && _offHandGrip.parent != null ? _offHandGrip.parent.parent : null;
        _rightHandPivot = _mainHandGrip != null && _mainHandGrip.parent != null ? _mainHandGrip.parent.parent : null;
        if (_leftHandPivot != null)
        {
            _leftHandPivot.GetLocalPositionAndRotation(out _leftHandPivotBasePosition, out _leftHandPivotBaseRotation);
        }

        if (_rightHandPivot != null)
        {
            _rightHandPivot.GetLocalPositionAndRotation(out _rightHandPivotBasePosition, out _rightHandPivotBaseRotation);
        }
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

    private void SetRendererSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sprite = sprite;
        renderer.enabled = sprite != null && !_hiddenByDowned;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
