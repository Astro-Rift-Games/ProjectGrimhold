using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Concrete animator view component for player entities, inheriting core animation logic from <see cref="CharacterAnimatorView"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimatorView : CharacterAnimatorView
{
    private const float DefaultLocomotionPlaybackRate = 1f;

    private static readonly int LocomotionPlaybackRateHash =
        Animator.StringToHash("LocomotionPlaybackRate");
    private static readonly int HasGenericAttackHash = Animator.StringToHash("HasGenericAttack");
    private static readonly int IsDefendingHash = Animator.StringToHash("IsDefending");
    private static readonly string[] AttackDirections = { "N", "NE", "NW", "S", "SE", "SW" };
    private const string MainHandCombatLayerName = "RightHand";

    protected override bool StopsLocomotionDuringTemporalFacing => false;

    [Header("Combat Presentation")]
    [SerializeField] private PlayerCombatNetworkController _combatController;
    [SerializeField] private PlayerWeaponEquipmentNetworkController _equipmentSource;
    [SerializeField] private PlayerShieldDefenseNetworkController _shieldDefense;

    [SerializeField, Min(0f)]
    private float _referenceMovementSpeed = 4f;

    [SerializeField, Range(0f, 180f), Tooltip("Free-aim weapons only. The body keeps its movement facing bucket while the aim stays within this many degrees of that bucket's direction, and shows the aim's own bucket beyond it. At 22.5 degrees, the narrowest bucket half-width, the body always shows the aim bucket.")]
    private float _freeAimArcHalfWidthDegrees = 45f;

    private Vector2 _previousVisualPosition;
    private bool _hasPreviousVisualPosition;
    // The aim a free-aim weapon presents, smoothed for proxies. Only read while such a weapon is equipped.
    private const float RemoteAimTurnRateDegreesPerSecond = 1080f;
    private const float FacingArcHysteresisDegrees = 5f;
    private bool _showingAimBucket;
    private PlayerMovementNetworkController _aimSource;
    private float _aimStanceBlend;
    private float _stringHandPinWeight;
    private bool _stancePoseActive;
    private float _stanceClipSeconds;
    private float _drawnClipSeconds;
    private Vector2 _presentedAim;
    private bool _hasPresentedAim;
    private PlayerCombatNetworkController _subscribedCombatController;
    private int _mainHandCombatLayerIndex = -1;
    private bool _hasObservedAttackState;
    private WeaponDefinition _activeWeapon;
    private DirectionalAttackAnimationSet _activeAttackSet;
    private LootDefinition _confirmedAttackWeapon;
    private bool _attackWeaponPinned;
    private bool _hasTimedAttack;
    private AttackPerformedEvent _timedAttack;
    private AnimationClip _timedAttackClip;
    private float _authoredReleaseSeconds;
    private static readonly int AttackStateHash = Animator.StringToHash("RightHand.Attack");
    private static readonly int IdleStateHash = Animator.StringToHash("RightHand.RightHand-Idle");
    private readonly List<AnimatorClipInfo> _attackClipBuffer = new List<AnimatorClipInfo>(2);

    /// <summary>
    /// The smoothed aim shared by the body facing and the weapon presenter while a free-aim weapon is equipped.
    /// </summary>
    public Vector2 PresentedAimDirection => _presentedAim;

    public bool HasPresentedAim => _hasPresentedAim;

    /// <summary>
    /// How drawn the aiming arm is, in [0, 1]: the draw progress while the stance is held, and fully drawn during an
    /// aimed shot. It ramps the optional outward offset and, later, the string hand pin. Derived from networked state.
    /// </summary>
    public float AimStanceBlend => _aimStanceBlend;

    /// <summary>
    /// How much the drawn string hand is pinned to the nock, in [0, 1]: the draw progress while the stance is held, full
    /// during an aimed shot before the release frame, and 0 from the release frame on, when the authored animation
    /// takes over again.
    /// </summary>
    public float StringHandPinWeight => _stringHandPinWeight;

    /// <summary>
    /// Whether the facing is aim-driven: an attack faces the aim, or the replicated aim stance is held. Proxies get
    /// the same answer from networked state.
    /// </summary>
    public bool IsAimDriven =>
        HasTemporalFacing ||
        (_aimSource != null && _aimSource.Object != null && _aimSource.Object.IsValid && _aimSource.IsAimStance);

    protected override void Update()
    {
        // Sampled every frame, including during an attack, so the weapon keeps following the aim.
        RefreshPresentedAim();
        base.Update();
    }

    protected override bool TryGetPresentedFacing(Vector2 movementFacing, out Vector2 facing)
    {
        return FreeAimFacingSelection.TrySelect(
            _activeWeapon,
            _hasPresentedAim,
            _presentedAim,
            movementFacing,
            _freeAimArcHalfWidthDegrees,
            FacingArcHysteresisDegrees,
            ref _showingAimBucket,
            out facing);
    }

    private void RefreshPresentedAim()
    {
        if (_activeWeapon == null || _activeWeapon.Presentation.AimMode == WeaponAimMode.BakedFacing ||
            _aimSource == null || _aimSource.Object == null || !_aimSource.Object.IsValid)
        {
            _hasPresentedAim = false;
            return;
        }

        _presentedAim = AimDirectionSmoothing.Resolve(
            _aimSource.AimDirection,
            _aimSource.FacingDirection,
            _presentedAim,
            _hasPresentedAim,
            Time.deltaTime,
            RemoteAimTurnRateDegreesPerSecond,
            // The owning player renders its own cursor immediately; only proxies smooth the sampled aim.
            !_aimSource.Object.HasInputAuthority);
        _hasPresentedAim = true;
    }

    public bool TryGetPresentedAttackWeapon(out LootDefinition definition)
    {
        definition = _confirmedAttackWeapon;
        return _attackWeaponPinned && definition != null;
    }

    /// <summary>
    /// Playback time, in seconds, of the confirmed attack's clip while the main-hand layer plays one of the
    /// presented attack weapon's own directional clips.
    /// </summary>
    public bool TryGetPresentedAttackSeconds(out float seconds)
    {
        seconds = 0f;
        if (_hasTimedAttack)
        {
            if (!_combatController.TryGetAttackPresentationSeconds(_timedAttack, out float elapsed)) return false;
            seconds = _timedAttack.IsAimed
                ? AttackTiming.AimedClipSeconds(
                    elapsed, _timedAttack.ScheduledWindupSeconds, _authoredReleaseSeconds, _drawnClipSeconds)
                : AttackTiming.ClipSeconds(elapsed, _timedAttack.ScheduledWindupSeconds, _authoredReleaseSeconds);
            return _timedAttackClip != null && seconds < _timedAttackClip.length;
        }
        if (_stancePoseActive)
        {
            // The held drawn pose shows the same string frame as the attack clip at that time.
            seconds = _stanceClipSeconds;
            return true;
        }
        WeaponDefinition weapon = _attackWeaponPinned && _confirmedAttackWeapon != null
            ? _confirmedAttackWeapon.WeaponDefinition : null;
        DirectionalAttackAnimationSet attackSet = weapon != null ? weapon.Presentation.AttackAnimationSet : null;
        if (attackSet == null || AnimatorInstance == null || _mainHandCombatLayerIndex < 0)
        {
            return false;
        }

        AnimatorStateInfo state = AnimatorInstance.GetCurrentAnimatorStateInfo(_mainHandCombatLayerIndex);
        if (!state.IsTag("Attack"))
        {
            return false;
        }

        AnimatorInstance.GetCurrentAnimatorClipInfo(_mainHandCombatLayerIndex, _attackClipBuffer);
        for (int i = 0; i < _attackClipBuffer.Count; i++)
        {
            AnimationClip clip = _attackClipBuffer[i].clip;
            for (int direction = 0; direction < 6; direction++)
            {
                if (clip != null && clip == attackSet.GetAttackClip(direction))
                {
                    seconds = state.normalizedTime * clip.length;
                    return true;
                }
            }
        }

        return false;
    }

    private RuntimeAnimatorController _baseController;
    private AnimatorOverrideController _attackOverrides;
    private AnimationClip[] _placeholderClips;

    private void OnEnable()
    {
        CacheDependencies();
        SubscribeToCombat();
        CacheCombatLayerIndex();
        RefreshCombatParameters();
    }

    protected override void OnDisable()
    {
        UnsubscribeFromCombat();
        if (AnimatorInstance != null)
        {
            AnimatorInstance.SetBool(HasGenericAttackHash, false);
            AnimatorInstance.SetBool(IsDefendingHash, false);
            if (_attackOverrides != null && AnimatorInstance.runtimeAnimatorController == _attackOverrides)
            {
                AnimatorInstance.runtimeAnimatorController = _baseController;
            }
        }
        if (_attackOverrides != null)
        {
            Destroy(_attackOverrides);
            _attackOverrides = null;
        }
        _baseController = null;
        _placeholderClips = null;
        _activeWeapon = null;
        _activeAttackSet = null;
        _confirmedAttackWeapon = null;
        _attackWeaponPinned = false;
        _hasObservedAttackState = false;
        _hasTimedAttack = false;
        _timedAttackClip = null;
        _hasPresentedAim = false;
        _showingAimBucket = false;
        _stancePoseActive = false;
        _aimStanceBlend = 0f;
        _stringHandPinWeight = 0f;
        base.OnDisable();
        ResetVisualPositionSample();
    }

    private void LateUpdate()
    {
        if (AnimatorInstance == null)
        {
            return;
        }

        float playbackRate = SampleLocomotionPlaybackRate(
            transform.position,
            Time.deltaTime);

        AnimatorInstance.SetFloat(
            LocomotionPlaybackRateHash,
            playbackRate);

        RefreshCombatParameters();
        if (_hasTimedAttack)
        {
            RefreshTimedAttack();
            if (!_hasTimedAttack)
            {
                RefreshAimStancePose();
            }
        }
        else if (!RefreshAimStancePose())
        {
            RefreshAttackFacingLifetime();
        }

        RefreshAimStanceBlend();
    }

    protected override void CacheDependencies()
    {
        base.CacheDependencies();
        _combatController ??= GetComponentInParent<PlayerCombatNetworkController>();
        _equipmentSource ??= GetComponentInParent<PlayerWeaponEquipmentNetworkController>();
        _shieldDefense ??= GetComponentInParent<PlayerShieldDefenseNetworkController>();
        _aimSource ??= GetComponentInParent<PlayerMovementNetworkController>();
    }

    private void SubscribeToCombat()
    {
        if (_subscribedCombatController == _combatController)
        {
            return;
        }

        UnsubscribeFromCombat();
        _subscribedCombatController = _combatController;
        if (_subscribedCombatController != null)
        {
            _subscribedCombatController.AttackPerformed += OnAttackPerformed;
            _subscribedCombatController.AttackPresentationResumed += OnAttackPerformed;
        }
    }

    private void UnsubscribeFromCombat()
    {
        if (_subscribedCombatController != null)
        {
            _subscribedCombatController.AttackPerformed -= OnAttackPerformed;
            _subscribedCombatController.AttackPresentationResumed -= OnAttackPerformed;
            _subscribedCombatController = null;
        }
    }

    private void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        if (AnimatorInstance == null)
        {
            return;
        }

        _hasTimedAttack = false;
        _timedAttackClip = null;
        _attackWeaponPinned = _equipmentSource != null &&
            _equipmentSource.TryGetWeaponByCatalogIndexPlusOne(
                attackEvent.WeaponCatalogIndexPlusOne, out _confirmedAttackWeapon);
        if (!_attackWeaponPinned)
        {
            _confirmedAttackWeapon = null;
        }
        RefreshAttackAnimation();
        ApplyTemporalFacingDirection(attackEvent.Direction);
        _hasObservedAttackState = false;
        if (attackEvent.HasReleaseTimeline && _attackWeaponPinned && _activeWeapon.Presentation.HasGenericAttack)
        {
            CharacterVisualDirection facing = CharacterVisualDirectionResolver.Resolve(attackEvent.Direction);
            int index = facing switch
            {
                CharacterVisualDirection.North => 0, CharacterVisualDirection.NorthEast => 1,
                CharacterVisualDirection.NorthWest => 2, CharacterVisualDirection.South => 3,
                CharacterVisualDirection.SouthEast => 4, _ => 5
            };
            _timedAttack = attackEvent;
            _timedAttackClip = _activeWeapon.Presentation.GetAttackClip(index);
            _authoredReleaseSeconds = _activeWeapon.AttackReleaseSeconds;
            _drawnClipSeconds = _activeWeapon.AimStanceDrawnClipSeconds;
            _hasTimedAttack = true;
            AnimatorInstance.ResetTrigger("OnAttack");
            RefreshTimedAttack();
        }
        else
        {
            TriggerAttack(); // Attacks without a release timeline (legacy or enemy consumers) keep the trigger route.
        }
    }

    // While the aim stance is held with a stance weapon, the main-hand layer plays the weapon's own attack clip in
    // the aim bucket: from its start to the drawn frame over the draw time, then holding that frame. The same
    // absolute-phase mechanism as a timed attack, driven from the replicated stance, so proxies show it too.
    // Returns whether the drawn pose is being presented.
    private bool RefreshAimStancePose()
    {
        if (TryGetAimStancePose(out AnimationClip clip, out float clipSeconds))
        {
            _stancePoseActive = true;
            _stanceClipSeconds = clipSeconds;
            AnimatorInstance.Play(AttackStateHash, _mainHandCombatLayerIndex, clipSeconds / clip.length);
            AnimatorInstance.Update(0f);
            return true;
        }

        if (_stancePoseActive)
        {
            _stancePoseActive = false;
            if (_mainHandCombatLayerIndex >= 0)
            {
                AnimatorInstance.Play(IdleStateHash, _mainHandCombatLayerIndex, 0f);
            }
        }

        return false;
    }

    private void RefreshAimStanceBlend()
    {
        float blend = 0f;
        float pin = 0f;
        if (PlayerAimStanceRules.WeaponAllows(_activeWeapon))
        {
            if (_hasTimedAttack && _timedAttack.IsAimed)
            {
                blend = 1f;
                pin = TryGetPresentedAttackSeconds(out float seconds) && seconds < _authoredReleaseSeconds ? 1f : 0f;
            }
            else if (_aimSource != null && _aimSource.TryGetAimStanceElapsedSeconds(out float elapsed))
            {
                blend = AimStanceDraw.Progress(elapsed, _activeWeapon.AimStanceDrawSeconds);
                pin = _hasTimedAttack ? 0f : blend;
            }
        }

        _aimStanceBlend = blend;
        _stringHandPinWeight = pin;
    }

    private bool TryGetAimStancePose(out AnimationClip clip, out float clipSeconds)
    {
        clip = null;
        clipSeconds = 0f;
        if (_mainHandCombatLayerIndex < 0)
        {
            CacheCombatLayerIndex();
        }

        if (_mainHandCombatLayerIndex < 0 || _hasTimedAttack || !PlayerAimStanceRules.WeaponAllows(_activeWeapon) ||
            !_activeWeapon.Presentation.HasGenericAttack || _attackOverrides == null ||
            _aimSource == null || !_aimSource.TryGetAimStanceElapsedSeconds(out float elapsed))
        {
            return false;
        }

        CharacterVisualDirection facing = CharacterVisualDirectionResolver.Resolve(VisualFacingDirection);
        int index = facing switch
        {
            CharacterVisualDirection.North => 0, CharacterVisualDirection.NorthEast => 1,
            CharacterVisualDirection.NorthWest => 2, CharacterVisualDirection.South => 3,
            CharacterVisualDirection.SouthEast => 4, _ => 5
        };
        clip = _activeWeapon.Presentation.GetAttackClip(index);
        if (clip == null || clip.length <= 0f)
        {
            return false;
        }

        clipSeconds = AimStanceDraw.DrawClipSeconds(
            elapsed, _activeWeapon.AimStanceDrawSeconds, _activeWeapon.AimStanceDrawnClipSeconds);
        return true;
    }

    private void RefreshTimedAttack()
    {
        if (_mainHandCombatLayerIndex < 0) CacheCombatLayerIndex();
        if (_mainHandCombatLayerIndex < 0) return;
        if (!TryGetPresentedAttackSeconds(out float seconds))
        {
            _hasTimedAttack = false;
            _timedAttackClip = null;
            _confirmedAttackWeapon = null;
            _attackWeaponPinned = false;
            ClearTemporalFacingDirection();
            AnimatorInstance.Play(IdleStateHash, _mainHandCombatLayerIndex, 0f);
            RefreshAttackAnimation();
            return;
        }
        // Absolute phase on every observation, including late joins and migrated wind-ups.
        // The main-hand layer alone is sought; locomotion and off-hand defense keep running.
        AnimatorInstance.Play(AttackStateHash, _mainHandCombatLayerIndex, seconds / _timedAttackClip.length);
        AnimatorInstance.Update(0f);
    }

    private void RefreshCombatParameters()
    {
        if (AnimatorInstance == null)
        {
            return;
        }

        RefreshAttackAnimation();
        AnimatorInstance.SetBool(IsDefendingHash, IsDefending());
    }

    // Mirrors the replicated defense state; gameplay alone decides when it holds.
    private bool IsDefending()
    {
        return _shieldDefense != null &&
            _shieldDefense.Object != null &&
            _shieldDefense.Object.IsValid &&
            _shieldDefense.IsDefending;
    }

    private void RefreshAttackAnimation()
    {
        WeaponDefinition weapon = _attackWeaponPinned
            ? _confirmedAttackWeapon.WeaponDefinition : null;
        if (!_attackWeaponPinned && CanReadEquipmentState() &&
            _equipmentSource.TryGetEquippedDefinition(out LootDefinition definition) &&
            definition != null)
        {
            weapon = definition.WeaponDefinition;
        }

        if (_activeWeapon != weapon ||
            _activeAttackSet != (weapon != null ? weapon.Presentation.AttackAnimationSet : null) ||
            (_baseController == null && AnimatorInstance.runtimeAnimatorController != null))
        {
            RefreshAttackOverrides(weapon);
        }

        AnimatorInstance.SetBool(HasGenericAttackHash, weapon != null &&
            weapon.Presentation.HasGenericAttack && _attackOverrides != null);
    }

    private void RefreshAttackOverrides(WeaponDefinition weapon)
    {
        _activeWeapon = weapon;
        _activeAttackSet = weapon != null ? weapon.Presentation.AttackAnimationSet : null;
        RuntimeAnimatorController currentController = AnimatorInstance.runtimeAnimatorController;
        if (_baseController == null)
        {
            _baseController = currentController;
        }

        if (weapon == null || !weapon.Presentation.HasGenericAttack || _baseController == null)
        {
            if (_attackOverrides != null)
            {
                for (int index = 0; index < AttackDirections.Length; index++)
                {
                    _attackOverrides[_placeholderClips[index]] = _placeholderClips[index];
                }
            }
            return;
        }

        if (_attackOverrides == null)
        {
            AnimationClip[] clips = _baseController.animationClips;
            _placeholderClips = new AnimationClip[AttackDirections.Length];
            for (int index = 0; index < AttackDirections.Length; index++)
            {
                string name = "GenericAttack_" + AttackDirections[index];
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    if (clips[clipIndex].name == name)
                    {
                        _placeholderClips[index] = clips[clipIndex];
                        break;
                    }
                }
                if (_placeholderClips[index] == null)
                {
                    Debug.LogError($"Missing generic attack placeholder {name}.", this);
                    _placeholderClips = null;
                    return;
                }
            }
            _attackOverrides = new AnimatorOverrideController(_baseController);
        }

        for (int index = 0; index < AttackDirections.Length; index++)
        {
            _attackOverrides[_placeholderClips[index]] = weapon.Presentation.GetAttackClip(index);
        }
        if (AnimatorInstance.runtimeAnimatorController != _attackOverrides)
        {
            AnimatorInstance.runtimeAnimatorController = _attackOverrides;
        }
    }

    private bool CanReadEquipmentState()
    {
        return _equipmentSource != null &&
            _equipmentSource.Object != null &&
            _equipmentSource.Object.IsValid;
    }

    private void CacheCombatLayerIndex()
    {
        _mainHandCombatLayerIndex = AnimatorInstance != null
            ? AnimatorInstance.GetLayerIndex(MainHandCombatLayerName)
            : -1;
    }

    private void RefreshAttackFacingLifetime()
    {
        if (_mainHandCombatLayerIndex < 0)
        {
            CacheCombatLayerIndex();
        }

        if (_mainHandCombatLayerIndex < 0)
        {
            return;
        }

        bool isInAttack =
            AnimatorInstance.GetCurrentAnimatorStateInfo(_mainHandCombatLayerIndex).IsTag("Attack") ||
            AnimatorInstance.IsInTransition(_mainHandCombatLayerIndex) &&
            AnimatorInstance.GetNextAnimatorStateInfo(_mainHandCombatLayerIndex).IsTag("Attack");
        if (isInAttack)
        {
            _hasObservedAttackState = true;
            return;
        }

        if (_hasObservedAttackState)
        {
            _hasObservedAttackState = false;
            ClearTemporalFacingDirection();
            _confirmedAttackWeapon = null;
            _attackWeaponPinned = false;
            RefreshAttackAnimation();
        }
    }

    private float SampleLocomotionPlaybackRate(
        Vector2 currentVisualPosition,
        float deltaTime)
    {
        if (!IsFinite(currentVisualPosition))
        {
            ResetVisualPositionSample();
            return DefaultLocomotionPlaybackRate;
        }

        if (!_hasPreviousVisualPosition)
        {
            _previousVisualPosition = currentVisualPosition;
            _hasPreviousVisualPosition = true;
            return DefaultLocomotionPlaybackRate;
        }

        Vector2 previousVisualPosition = _previousVisualPosition;
        _previousVisualPosition = currentVisualPosition;

        return CalculateLocomotionPlaybackRate(
            previousVisualPosition,
            currentVisualPosition,
            deltaTime,
            _referenceMovementSpeed);
    }

    private static float CalculateLocomotionPlaybackRate(
        Vector2 previousVisualPosition,
        Vector2 currentVisualPosition,
        float deltaTime,
        float referenceMovementSpeed)
    {
        if (!IsFinite(previousVisualPosition) ||
            !IsFinite(currentVisualPosition) ||
            !IsFinite(deltaTime) ||
            deltaTime <= 0f ||
            !IsFinite(referenceMovementSpeed) ||
            referenceMovementSpeed <= 0f)
        {
            return DefaultLocomotionPlaybackRate;
        }

        float distance = Vector2.Distance(
            previousVisualPosition,
            currentVisualPosition);

        if (!IsFinite(distance))
        {
            return DefaultLocomotionPlaybackRate;
        }

        float visualMovementSpeed = distance / deltaTime;
        if (!IsFinite(visualMovementSpeed))
        {
            return DefaultLocomotionPlaybackRate;
        }

        float playbackRate = visualMovementSpeed / referenceMovementSpeed;
        return IsFinite(playbackRate)
            ? playbackRate
            : DefaultLocomotionPlaybackRate;
    }

    private void ResetVisualPositionSample()
    {
        _previousVisualPosition = default;
        _hasPreviousVisualPosition = false;
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
