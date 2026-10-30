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
    // Same boundary tolerance as PlayerAttackVfxPresenter, so a release lead's float rounding keeps Off on Main's frames.
    private const float VfxBoundaryToleranceSeconds = 0.0001f;
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
    private Vector2 _aimStanceNockPoint;
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
    private OneHandPoseSampler _offHandSampler;
    private SpriteRenderer _offHandAttackVfx;
    private SpriteRenderer _mainHandHoldingRenderer;
    private bool _mainHandShield;
    private DirectionalShieldSpriteSet _mainHandDefenseSprites;
    private PlayerShieldDefenseNetworkController _shieldDefense;
    private Vector3 _offHandBaseScale;
    private LootDefinition _reportedMissingOffHandSet;
    private bool _secondaryGripPinned;
    private Vector3 _secondaryHandLocalPosition;
    private int _secondaryHandSortingOrder;

    private PlayerConsumableNetworkController _consumables;
    private LootDefinition _presentedConsumable;
    private IPlayerPresentationState _presentationState;
    private NetworkPlayerPresentationState _networkPresentationState;

    private void Awake()
    {
        CacheDependencies();
        CaptureBaseState();
    }

    private void OnEnable() => InitializePresentation();

    /// <summary>Binds externally owned state for preview rendering without Fusion.</summary>
    internal void BindPresentationState(IPlayerPresentationState state)
    {
        if (state == null) throw new System.ArgumentNullException(nameof(state));
        _networkPresentationState?.Dispose();
        _networkPresentationState = null;
        _presentationState = state;
    }

    /// <summary>Performs the same dependency validation and initial pose as OnEnable.</summary>
    internal void InitializePresentation()
    {
        CacheDependencies();
        CaptureBaseState();
        if (_presentationState == null)
        {
            Transform root = _equipmentSource != null ? _equipmentSource.transform : transform.root;
            _networkPresentationState = new NetworkPlayerPresentationState(
                root.GetComponent<PlayerCombatNetworkController>(), _equipmentSource,
                root.GetComponent<PlayerMovementNetworkController>(), _shieldDefense,
                _downedState, _character, _consumables);
            _presentationState = _networkPresentationState;
        }
        if (!ValidateDependencies())
        {
            enabled = false;
            return;
        }

        RefreshEquipment(force: true);
        RefreshPose();
    }

    private void OnDisable() => ReleasePresentation();

    /// <summary>Releases the sampled overlay and owned preview resources without Unity callbacks.</summary>
    internal void ReleasePresentation()
    {
        RestoreSecondaryGripPin();
        if (_networkPresentationState != null)
        {
            _networkPresentationState.Dispose();
            _networkPresentationState = null;
            _presentationState = null;
        }
        _offHandSampler?.Restore();
        _reportedMissingOffHandSet = null;
        _presentedConsumable = null;
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
        _offHandSampler?.Dispose();
        _offHandSampler = null;
        if (_offHandAttackVfx != null)
        {
            if (Application.isPlaying) Destroy(_offHandAttackVfx.gameObject);
            else DestroyImmediate(_offHandAttackVfx.gameObject);
        }
        _offHandAttackVfx = null;
        SetWeaponDriven(false);
        SetRendererSprite(_mainHandRenderer, null);
        SetRendererSprite(_offHandRenderer, null);
    }

    private void Update() => BeginFrame();

    /// <summary>Restores the previous off-hand overlay before the Animator evaluates.</summary>
    internal void BeginFrame()
    {
        RestoreSecondaryGripPin();
        _offHandSampler?.Restore();
    }

    private void LateUpdate() => PresentFrame();

    /// <summary>Runs held-item presentation after the Animator view's post-Animator step.</summary>
    internal void PresentFrame()
    {
        RefreshDownedVisibility();
        RefreshEquipment(force: false);
        RefreshConsumptionPresentation();
        RefreshMainHandSprite();
        RefreshPose();
        // Main/shield consumers read the evaluated base, never the offensive left overlay.
        RefreshOffHandAttackPose();
        Vector2 offFacing = _animatorView.VisualFacingDirection;
        if (_offHandDefinition?.WeaponDefinition?.Presentation.AimMode == WeaponAimMode.BakedFacing &&
            _animatorView.TryGetPresentedOffHandAttack(out AttackPerformedEvent offAttack, out _, out _))
            offFacing = offAttack.Direction;
        RefreshOffHandDepth(CharacterVisualDirectionResolver.Resolve(offFacing));
    }

    // Presentation only: observes the networked Downed state. Visibility combines with the equipment
    // sprite (an empty slot stays hidden) instead of forcing the renderers on.
    private void RefreshDownedVisibility()
    {
        bool isDowned = _presentationState != null && _presentationState.IsDowned;
        bool hidden = _hiddenByDowned;
        if (isDowned)
        {
            hidden = true;
        }
        else if (_hiddenByDowned && _presentationState != null && _presentationState.IsAlive)
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
                _presentationState.TryGetEquippedDefinition(out mainHand);
            }

            if (_animatorView.TryGetPresentedOffHandAttack(out _, out LootDefinition pinnedOffHand, out _))
                offHand = pinnedOffHand;
            WeaponSetSlot activeSet = _presentationState.ActiveWeaponSetSlot;
            EquipmentSlot offHandSlot = EquipmentSlotRules.GetOffHandSlot(activeSet);
            if (offHand == null && offHandSlot != EquipmentSlot.None)
            {
                _presentationState.TryGetSlotDefinition(offHandSlot, out offHand);
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
        _mainHandShield = definition != null && definition.Category == LootCategory.Shield;
        _mainHandDefenseSprites = _mainHandShield ? definition.DefenseSprites : null;
        _mainHandWorldSprite = weapon != null || _mainHandShield ? definition.WorldSprite ?? definition.Icon : null;
        _mainHandAttackSprites = weapon != null ? weapon.Presentation.AttackSpriteAnimation : null;
        SetRendererSprite(_mainHandRenderer, _mainHandWorldSprite);
        WeaponRig rig = weapon != null ? weapon.Presentation.Rig : WeaponRig.HandHeld;
        AttachMainHandWeapon(rig);
        SetWeaponDriven(rig == WeaponRig.WeaponDriven);
        _hasMainHandWeapon = weapon != null;
        _mainHandAimMode = weapon != null ? weapon.Presentation.AimMode : WeaponAimMode.BakedFacing;
        _aimStanceTorsoPivot = weapon != null ? weapon.AimStanceTorsoPivot : Vector2.zero;
        _aimStanceOutwardOffset = weapon != null ? weapon.AimStanceOutwardOffset : 0f;
        _aimStanceNockPoint = weapon != null ? weapon.AimStanceNockPoint : Vector2.zero;
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
        _offHandWorldSprite = isShield || definition?.WeaponDefinition != null ? definition.WorldSprite ?? definition.Icon : null;
        _offHandDirectionalSprites = isShield ? definition.DefenseSprites : null;
        SetRendererSprite(_offHandRenderer, _offHandWorldSprite);

        _offHandVisual.localPosition = Vector3.zero;
        _offHandVisual.localRotation = Quaternion.identity;
    }

    // Swaps only the sprite, so the pose, grip, angle correction, facing and mirror stay untouched.
    private void RefreshMainHandSprite()
    {
        if (_presentedConsumable != null)
        {
            Sprite consumableSprite = _presentedConsumable.WorldSprite;
            SetRendererSprite(_mainHandRenderer, consumableSprite);
            return;
        }
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
            _offHandWorldSprite, _offHandDirectionalSprites, direction);
        if (_offHandDefinition?.WeaponDefinition != null &&
            _animatorView.TryGetPresentedOffHandAttack(out _, out _, out float seconds))
            sprite = PlayerWeaponPresentationMath.ResolveMainHandSprite(sprite,
                _offHandDefinition.WeaponDefinition.Presentation.AttackSpriteAnimation, true, seconds);
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
            ApplyStringPin(mirrored);
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

        if (_mainHandShield)
        {
            _mainHandWeaponPivot.localRotation = Quaternion.identity;
            _mainHandWeaponPivot.localScale = _mainHandWeaponPivotBaseScale;
            _mainHandRenderer.sprite = PlayerWeaponPresentationMath.ResolveOffHandSprite(
                _mainHandWorldSprite, _mainHandDefenseSprites, direction);
            // Defense clips animate LeftHand's motion about its drawn anchor. Reuse that motion,
            // not its grip coordinates: the anatomical right sprite has a different drawn anchor.
            // MainHandGrip retains the right-hand idle anchor evaluated by the Animator, so the
            // shield follows the right fist throughout the transferred translation and rotation.
            if (_presentationState != null && _presentationState.IsDefending)
            {
                _mainHandGrip.parent.SetLocalPositionAndRotation(_offHandGrip.parent.localPosition, _offHandGrip.parent.localRotation);
            }
        }
        PoseOffHandWeapon(facing, mirrored);
        _mainHandRenderer.sortingOrder = _mainHandShield
            ? PlayerWeaponPresentationMath.ResolveShieldSortingOrder(order,
                (_mainHandHoldingRenderer ??= _mainHandGrip.parent.GetComponent<SpriteRenderer>()).sortingOrder)
            : order;
        // The weapon-pose hand is the LeftHand renderer that also carries OffHandGrip.
        RefreshOffHandDepth(direction);
        RefreshOffHandSprite(direction);
        if (_weaponDriven)
        {
            _weaponPoseHandRenderer.sortingOrder = CharacterVisualDirectionResolver.CalculateSortingOrder(
                direction,
                WeaponPoseHandSortingOrderFront,
                -2);
        }

        ApplySecondaryGripPin(direction);
        RefreshAttackVfxPivot(followsAim, mirrored, CharacterVisualDirectionResolver.IsFrontFacing(direction));
    }

    // After the Animator: the hand pivots carry no curves, so they are set absolutely; WeaponPose is animated, so its
    // freshly evaluated pose is read and rewritten. Everything turns about the weapon's torso pivot by the residual.
    private void RefreshOffHandAttackPose()
    {
        if (_offHandAttackVfx != null) _offHandAttackVfx.enabled = false;
        WeaponDefinition equipped = _offHandDefinition != null ? _offHandDefinition.WeaponDefinition : null;
        if (equipped != null && equipped.Presentation.HasGenericAttack &&
            (equipped.Presentation.OffHandAttackAnimationSet == null ||
             !equipped.Presentation.OffHandAttackAnimationSet.IsComplete ||
             !equipped.Presentation.OffHandAttackAnimationSet.HasGripOriginOffsets ||
             (equipped.Presentation.OffHandAttackAnimationSet.OffHandStroke == OffHandStroke.Mirrored &&
              !equipped.Presentation.OffHandAttackAnimationSet.HasMirrorMetadata)))
        {
            if (_reportedMissingOffHandSet != _offHandDefinition)
            {
                _reportedMissingOffHandSet = _offHandDefinition;
                Debug.LogError($"{equipped.name}: missing generated Off Hand presentation. Regenerate Off Hand attacks in the Editor.", this);
            }
            return;
        }
        _reportedMissingOffHandSet = null;
        if (_hiddenByDowned || !_animatorView.TryGetPresentedOffHandAttack(
            out AttackPerformedEvent attack, out LootDefinition loot, out float seconds)) return;
        WeaponDefinition.PresentationConfig presentation = loot.WeaponDefinition.Presentation;
        int direction = PlayerAnimatorView.DirectionIndex(attack.Direction);
        AnimationClip clip = presentation.GetOffHandAttackClip(direction);
        _offHandSampler ??= new OneHandPoseSampler();
        _offHandSampler.Sample(clip, seconds, _offHandGrip.parent, _offHandGrip, _weaponPoseHandRenderer);
        AttackVfxDefinition vfx = presentation.AttackVfx;
        if (vfx == null || _attackVfx == null || !_attackVfx.TryGetWeaponTint(loot.WeaponDefinition.DamageType, out Color tint) ||
            !_animatorView.TryGetOffHandAttackElapsed(out float elapsed)) return;
        float vfxSeconds = AttackTiming.ReleaseVfxSeconds(elapsed, attack.ScheduledWindupSeconds, vfx.ReleaseLeadSeconds);
        if (vfxSeconds < -VfxBoundaryToleranceSeconds || vfxSeconds + VfxBoundaryToleranceSeconds >= vfx.Clip.length) return;
        vfxSeconds = Mathf.Max(0f, vfxSeconds + VfxBoundaryToleranceSeconds);
        if (_offHandAttackVfx == null)
        {
            var visual = new GameObject("OffHandAttackVfx");
            visual.transform.SetParent(_animatorView.transform, false);
            _offHandAttackVfx = visual.AddComponent<SpriteRenderer>();
            _offHandAttackVfx.sortingLayerID = _offHandRenderer.sortingLayerID;
        }
        if (!vfx.TryResolvePose(PlayerAnimatorView.DirectionIndex(attack.Direction), presentation.BladeReach,
            out AttackVfxDefinition.ResolvedPose pose)) return;
        Vector3 position = pose.Position + (Vector3)presentation.OffHandAttackAnimationSet.GetGripOriginOffset(direction);
        Quaternion rotation = pose.Rotation;
        Vector3 scale = pose.Scale;
        var offSet = presentation.OffHandAttackAnimationSet;
        if (offSet.OffHandStroke == OffHandStroke.Mirrored)
        {
            Vector3 mainNeutral = offSet.GetMainGripNeutralPosition(direction);
            Vector3 displacement = pose.Position - mainNeutral;
            displacement = PlayerWeaponPresentationMath.ReflectAcrossFacing(displacement, direction);
            position = offSet.CanonicalSlash
                ? PlayerWeaponPresentationMath.ReflectAcrossFacing(pose.Position, direction)
                : mainNeutral + (Vector3)offSet.GetGripOriginOffset(direction) + displacement;
            // Reflect the axis about the body's facing, and reverse sprite-sequence handedness.
            rotation = PlayerWeaponPresentationMath.ReflectedRotation(rotation, direction);
            scale.y = -scale.y;
        }
        if (presentation.AimMode == WeaponAimMode.FreeAim)
        {
            Vector2 aim = _animatorView.HasPresentedAim ? _animatorView.PresentedAimDirection : attack.Direction;
            var source = FreeAimAttackVfxPose.ResolveAtGrip(_animatorView.transform, _offHandGrip, aim,
                PlayerWeaponPresentationMath.ShouldMirror(_animatorView.VisualFacingDirection), vfx.GetPose(direction), pose.SortingOrder);
            if (!vfx.TryResolvePose(source, presentation.BladeReach, out pose)) return;
            position = pose.Position;
            rotation = pose.Rotation;
            scale = pose.Scale;
        }
        _offHandAttackVfx.transform.SetLocalPositionAndRotation(position, rotation);
        _offHandAttackVfx.transform.localScale = scale;
        _offHandAttackVfx.sortingOrder = pose.SortingOrder;
        _offHandAttackVfx.sprite = _offHandSampler.SampleVfx(vfx.Clip, vfxSeconds);
        _offHandAttackVfx.color = tint;
        _offHandAttackVfx.enabled = _offHandAttackVfx.sprite != null;
    }

    private void RefreshOffHandDepth(CharacterVisualDirection direction)
    {
        _offHandRenderer.sortingOrder = PlayerWeaponPresentationMath.ResolveOffHandSortingOrder(
            CharacterVisualDirectionResolver.IsFrontFacing(direction), SortingOrderFront, SortingOrderBack,
            _weaponPoseHandRenderer.sortingOrder);
        // Shields intentionally cover their holding hand and glove, in either hemisphere.
        if (_offHandDefinition != null && _offHandDefinition.Category == LootCategory.Shield)
            _offHandRenderer.sortingOrder = PlayerWeaponPresentationMath.ResolveShieldSortingOrder(
                _offHandRenderer.sortingOrder, _weaponPoseHandRenderer.sortingOrder);
    }

    private void PoseOffHandWeapon(Vector2 facing, bool mirrored)
    {
        WeaponDefinition weapon = _offHandDefinition != null ? _offHandDefinition.WeaponDefinition : null;
        if (weapon == null) return;
        bool attacking = _animatorView.TryGetPresentedOffHandAttack(out AttackPerformedEvent attack, out _, out _);
        if (weapon.Presentation.AimMode == WeaponAimMode.BakedFacing && attacking)
        {
            facing = attack.Direction;
            mirrored = PlayerWeaponPresentationMath.ShouldMirror(facing);
        }
        float angle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing);
        if (weapon.Presentation.AimMode == WeaponAimMode.FreeAim && _animatorView.HasPresentedAim)
            angle += FreeAimResidual.AngleDegrees(facing, _animatorView.PresentedAimDirection);
        float correction = PlayerWeaponPresentationMath.ResolveAngleCorrection(weapon.Presentation.AngleCorrection, mirrored);
        // Main Hand applies correction below a mirrored pivot. Flattening that hierarchy must
        // reverse the correction across the reflection; rotation and reflection do not commute.
        angle += mirrored ? -correction : correction;
        var offSet = weapon.Presentation.OffHandAttackAnimationSet;
        if (attacking && offSet != null && offSet.OffHandStroke == OffHandStroke.Mirrored && offSet.HasMirrorMetadata)
        {
            int direction = PlayerAnimatorView.DirectionIndex(attack.Direction);
            Quaternion rotation = offSet.GetMirroredWeaponBasisRotation(direction) * Quaternion.AngleAxis(-angle, Vector3.forward);
            Vector3 axis = rotation * Vector3.right;
            angle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
            mirrored = !mirrored;
        }
        Vector2 scale = new(_offHandBaseScale.x, Mathf.Abs(_offHandBaseScale.y) * (mirrored ? -1f : 1f));
        Vector2 offset = PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(weapon.Presentation.GripPoint, scale, angle);
        _offHandVisual.localPosition = new Vector3(offset.x, offset.y, 0f);
        _offHandVisual.localRotation = Quaternion.Euler(0f, 0f, angle);
        _offHandVisual.localScale = new Vector3(scale.x, scale.y, _offHandBaseScale.z);
    }

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

    // The one exception to the Animator owning the hands: the drawn string hand sits on the weapon's nock point. It runs
    // after the Animator and the rigid block, so it follows the posed weapon, and it fades with the draw and ends at the
    // release frame, when the authored animation takes over again. Only weapon-driven weapons (bows, crossbow) pin.
    private void RestoreSecondaryGripPin()
    {
        if (!_secondaryGripPinned) return;
        _offHandGrip.parent.localPosition = _secondaryHandLocalPosition;
        _weaponPoseHandRenderer.sortingOrder = _secondaryHandSortingOrder;
        _secondaryGripPinned = false;
    }

    // Shared locomotion clips key OffHandGrip to the opaque hand centroid. Reconcile that
    // anchor after every pose, including baked attacks: authored non-planar hand rotation
    // and attack/locomotion transitions must not detach a hand that always holds the handle.
    // Preserve authored rotation and attack sorting; restore before the next Animator pass.
    private void ApplySecondaryGripPin(CharacterVisualDirection direction)
    {
        var weapon = _mainHandDefinition != null ? _mainHandDefinition.WeaponDefinition : null;
        if (weapon == null || weapon.Handedness != WeaponHandedness.TwoHanded ||
            weapon.Presentation.Rig != WeaponRig.HandHeld ||
            weapon.Presentation.SecondHand != SecondHandPresentation.HoldsSecondaryGrip ||
            _offHandDefinition != null) return;
        if (!_secondaryGripPinned)
        {
            _secondaryHandLocalPosition = _offHandGrip.parent.localPosition;
            _secondaryHandSortingOrder = _weaponPoseHandRenderer.sortingOrder;
            _secondaryGripPinned = true;
        }
        Vector3 target = _mainHandWeaponVisual.TransformPoint(weapon.Presentation.SecondaryGripPoint);
        _offHandGrip.parent.position += target - _offHandGrip.position;
        if (!_animatorView.TryGetPresentedAttackSeconds(out _))
            _weaponPoseHandRenderer.sortingOrder = CharacterVisualDirectionResolver.IsFrontFacing(direction) ? 25 : -2;
    }

    private void ApplyStringPin(bool mirrored)
    {
        float weight = _animatorView.StringHandPinWeight;
        if (!_weaponDriven || weight <= 0f)
        {
            return;
        }

        Vector3 nock = _mainHandWeaponVisual.TransformPoint(StringHandPin.NockForFacing(_aimStanceNockPoint, mirrored));
        Vector3 grip = _mainHandGrip.position;
        Vector2 offset = StringHandPin.Offset(new Vector2(grip.x, grip.y), new Vector2(nock.x, nock.y), weight);
        _mainHandGrip.parent.position += new Vector3(offset.x, offset.y, 0f);
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
        _shieldDefense ??= GetComponentInParent<PlayerShieldDefenseNetworkController>();
        Transform root = _equipmentSource != null ? _equipmentSource.transform : transform.root;
        _downedState ??= root.GetComponent<PlayerDownedStateNetworkController>();
        _character ??= root.GetComponent<CharacterBase>();
        _consumables ??= root.GetComponent<PlayerConsumableNetworkController>();
        _attackVfx ??= root.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
    }

    private void RefreshConsumptionPresentation()
    {
        LootDefinition definition = null;
        if (_presentationState != null) _presentationState.TryGetConsumingDefinition(out definition);
        if (_presentedConsumable == definition) return;
        _presentedConsumable = definition;
        if (definition == null)
        {
            RefreshEquipment(force: true);
            return;
        }

        ReleaseAimBlock();
        AttachMainHandWeapon(WeaponRig.HandHeld);
        SetWeaponDriven(false);
        _hasMainHandWeapon = false;
        _mainHandWeaponVisual.localPosition = Vector3.zero;
        _mainHandWeaponVisual.localRotation = Quaternion.identity;
        _mainHandWeaponVisual.localScale = _mainHandWeaponVisualBaseScale;
        _attackVfx?.CancelAndRestore();
    }

    private void CaptureBaseState()
    {
        if (_hasCapturedBaseState ||
            _mainHandWeaponPivot == null ||
            _mainHandWeaponVisual == null)
        {
            return;
        }

        _offHandBaseScale = _offHandVisual != null ? _offHandVisual.localScale : Vector3.one;
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
        return _presentationState != null && _presentationState.IsEquipmentReadable;
    }

    private bool ValidateDependencies()
    {
        if (_animatorView != null &&
            _presentationState != null && (_networkPresentationState == null || _equipmentSource != null) &&
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
