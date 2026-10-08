using UnityEngine;

/// <summary>
/// Presents the active Weapon Set without owning attack motion.
/// The Animator moves the hands; held visuals inherit those transforms through their grips.
/// The single Main Hand weapon visual follows MainHandGrip, or WeaponPose when the weapon drives its own pose.
/// During the weapon's confirmed attack clip, an optional attack sprite animation swaps only its sprite.
/// The Off Hand shield shows its sprite for the visual direction in every pose.
/// While the player is Downed the held visuals are hidden and any attack VFX is cancelled. They return
/// when Downed ends only if the player is still alive, so a definitive defeat keeps them hidden.
/// A weapon whose aim mode is free aim instead orbits an anchor toward the continuous networked aim and places
/// both hands on its grips from LateUpdate, after the Animator. It runs after PlayerAnimatorView, whose attack
/// timeline re-evaluates the Animator in its own LateUpdate and would otherwise overwrite that pose.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(FreeAimExecutionOrder)]
public sealed class PlayerWeaponPresenter : MonoBehaviour
{
    // Later than PlayerAnimatorView (order 0), which may call Animator.Update inside its LateUpdate.
    internal const int FreeAimExecutionOrder = 100;
    // Remote aim samples arrive at the network tick rate; the render aim turns toward them at this speed.
    private const float RemoteAimTurnRateDegreesPerSecond = 1080f;
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
    private bool _mainHandFreeAim;
    private bool _mainHandTwoHanded;
    private Vector2 _mainHandSecondaryGripPoint;
    private Vector2 _mainHandStanceOffset;
    private Vector2 _renderAim;
    private bool _hasRenderAim;
    private bool _freeAimPoseApplied;
    private PlayerMovementNetworkController _aimSource;
    private Transform _visualRoot;
    private Transform _mainHand;
    private Transform _offHand;
    private Vector3 _weaponPoseBasePosition;
    private Quaternion _weaponPoseBaseRotation;
    private Vector3 _mainHandBasePosition;
    private Quaternion _mainHandBaseRotation;
    private Vector3 _mainHandGripBasePosition;
    private Vector3 _offHandBasePosition;
    private Quaternion _offHandBaseRotation;
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
        ReleaseFreeAimPose();
        _mainHandFreeAim = false;
        _hasRenderAim = false;
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

        ReleaseFreeAimPose();
        _hasRenderAim = false;
        _mainHandFreeAim = weapon != null && weapon.Presentation.AimMode == WeaponAimMode.FreeAim;
        if (weapon == null)
        {
            _mainHandWeaponVisual.localPosition = Vector3.zero;
            _mainHandWeaponVisual.localRotation = Quaternion.identity;
            _mainHandWeaponVisual.localScale = _mainHandWeaponVisualBaseScale;
            return;
        }

        _mainHandGripPoint = weapon.Presentation.GripPoint;
        _mainHandAngleCorrection = weapon.Presentation.AngleCorrection;
        _mainHandSecondaryGripPoint = weapon.Presentation.SecondaryGripPoint;
        _mainHandStanceOffset = weapon.Presentation.StanceOffset;
        _mainHandTwoHanded = weapon.Handedness == WeaponHandedness.TwoHanded;
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
        CharacterVisualDirection direction = CharacterVisualDirectionResolver.Resolve(facing);
        bool frontFacing = CharacterVisualDirectionResolver.IsFrontFacing(direction);

        if (_mainHandFreeAim && _hasMainHandWeapon)
        {
            frontFacing = ApplyFreeAimPose(facing);
        }
        else
        {
            ReleaseFreeAimPose();
            float facingAngle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing);
            bool mirrored = PlayerWeaponPresentationMath.ShouldMirror(facing);

            _mainHandWeaponPivot.localPosition = Vector3.zero;
            _mainHandWeaponPivot.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
            _mainHandWeaponPivot.localScale = new Vector3(
                _mainHandWeaponPivotBaseScale.x,
                Mathf.Abs(_mainHandWeaponPivotBaseScale.y) * (mirrored ? -1f : 1f),
                _mainHandWeaponPivotBaseScale.z);
            ApplyMainHandVisualPose(mirrored);
        }

        int order = frontFacing ? SortingOrderFront : SortingOrderBack;

        _mainHandRenderer.sortingOrder = order;
        // The weapon-pose hand is the LeftHand renderer that also carries OffHandGrip.
        _offHandRenderer.sortingOrder = PlayerWeaponPresentationMath.ResolveOffHandSortingOrder(
            frontFacing,
            SortingOrderFront,
            SortingOrderBack,
            _weaponPoseHandRenderer.sortingOrder);
        RefreshOffHandSprite(direction);
        if (_weaponDriven)
        {
            _weaponPoseHandRenderer.sortingOrder = frontFacing
                ? WeaponPoseHandSortingOrderFront
                : _weaponPoseHandBaseSortingOrder;
        }
    }

    // Free aim overrides what the Animator just wrote: the weapon orbits the anchor toward the aim and the hands
    // follow its grips. The body keeps its bucketed animation, and the hand sprites keep the body bucket.
    // Returns whether the weapon draws in front of the body.
    private bool ApplyFreeAimPose(Vector2 bodyFacing)
    {
        Vector2 aim = ResolveRenderAim(bodyFacing);
        // The stance offset lives in the aim frame, so the weapon orbits the body at its radius.
        Vector2 anchor = FreeAimAnchor.Resolve(aim, _mainHandStanceOffset);
        RangedWeaponAimPose pose = RangedWeaponAimPoseMath.Resolve(
            aim,
            anchor,
            _mainHandGripPoint,
            _mainHandSecondaryGripPoint,
            _mainHandAngleCorrection,
            new Vector2(_mainHandWeaponVisualBaseScale.x, _mainHandWeaponVisualBaseScale.y));

        if (_weaponDriven)
        {
            SetPose(_weaponPose, anchor, Quaternion.identity);
        }

        _mainHandWeaponPivot.localPosition = Vector3.zero;
        _mainHandWeaponPivot.localRotation = Quaternion.Euler(0f, 0f, pose.PivotAngleDegrees);
        _mainHandWeaponPivot.localScale = new Vector3(
            _mainHandWeaponPivotBaseScale.x,
            Mathf.Abs(_mainHandWeaponPivotBaseScale.y) * (pose.Mirrored ? -1f : 1f),
            _mainHandWeaponPivotBaseScale.z);
        ApplyMainHandVisualPose(pose.Mirrored);

        _mainHandGrip.localPosition = _mainHandGripBasePosition;
        // A weapon-driven weapon is held by the left hand; a one-handed hand-held weapon leaves the left hand to
        // the Animator, which keeps carrying its shield.
        FreeAimHandTargets hands = FreeAimHandAssignment.Resolve(_weaponDriven, _mainHandTwoHanded, pose);
        if (hands.DrivesRightHand)
        {
            SetPose(_mainHand, hands.RightHand, Quaternion.identity);
        }

        if (hands.DrivesLeftHand)
        {
            SetPose(_offHand, hands.LeftHand, Quaternion.identity);
        }

        _freeAimPoseApplied = true;
        if (_attackVfx != null)
        {
            _attackVfx.SetFreeAimPivot(
                anchor,
                pose.PivotAngleDegrees,
                pose.Mirrored,
                pose.FrontFacing);
        }

        return pose.FrontFacing;
    }

    private Vector2 ResolveRenderAim(Vector2 bodyFacing)
    {
        Vector2 aim = Vector2.zero;
        Vector2 fallback = bodyFacing;
        bool smooth = false;
        if (_aimSource != null && _aimSource.Object != null && _aimSource.Object.IsValid)
        {
            aim = _aimSource.AimDirection;
            fallback = _aimSource.FacingDirection;
            // The owning player renders its own cursor immediately; only proxies smooth the sampled aim.
            smooth = !_aimSource.Object.HasInputAuthority;
        }

        _renderAim = AimDirectionSmoothing.Resolve(
            aim,
            fallback,
            _renderAim,
            _hasRenderAim,
            Time.deltaTime,
            RemoteAimTurnRateDegreesPerSecond,
            smooth);
        _hasRenderAim = true;
        return _renderAim;
    }

    // Writes a point expressed in the visual root's space onto a transform that sits beneath it.
    private void SetPose(Transform target, Vector2 visualRootPoint, Quaternion localRotation)
    {
        target.localPosition = target.parent.InverseTransformPoint(
            _visualRoot.TransformPoint(new Vector3(visualRootPoint.x, visualRootPoint.y, 0f)));
        target.localRotation = localRotation;
    }

    // Leaving free aim hands the transforms back at their authored rest, so a clip without a curve for one of
    // them does not inherit the last free-aim pose.
    private void ReleaseFreeAimPose()
    {
        if (!_freeAimPoseApplied)
        {
            return;
        }

        _freeAimPoseApplied = false;
        if (_attackVfx != null)
        {
            _attackVfx.ClearFreeAimPivot();
        }

        _weaponPose.SetLocalPositionAndRotation(_weaponPoseBasePosition, _weaponPoseBaseRotation);
        _mainHand.SetLocalPositionAndRotation(_mainHandBasePosition, _mainHandBaseRotation);
        _offHand.SetLocalPositionAndRotation(_offHandBasePosition, _offHandBaseRotation);
        _mainHandGrip.localPosition = _mainHandGripBasePosition;
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
        _aimSource ??= root.GetComponent<PlayerMovementNetworkController>();
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
        CaptureFreeAimRestPose();
        _weaponPoseHandBaseSortingOrder = _weaponPoseHandRenderer != null ? _weaponPoseHandRenderer.sortingOrder : 0;
        _hasCapturedBaseState = true;
    }

    private void CaptureFreeAimRestPose()
    {
        if (_weaponPose == null || _mainHandGrip == null || _offHandGrip == null ||
            _mainHandGrip.parent == null || _offHandGrip.parent == null)
        {
            return;
        }

        _visualRoot = _weaponPose.parent;
        _mainHand = _mainHandGrip.parent;
        _offHand = _offHandGrip.parent;
        _weaponPoseBasePosition = _weaponPose.localPosition;
        _weaponPoseBaseRotation = _weaponPose.localRotation;
        _mainHandBasePosition = _mainHand.localPosition;
        _mainHandBaseRotation = _mainHand.localRotation;
        _mainHandGripBasePosition = _mainHandGrip.localPosition;
        _offHandBasePosition = _offHand.localPosition;
        _offHandBaseRotation = _offHand.localRotation;
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
