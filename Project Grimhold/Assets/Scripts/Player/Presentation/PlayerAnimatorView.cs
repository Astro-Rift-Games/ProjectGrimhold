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
    private static readonly int WeaponAnimationCategoryHash =
        Animator.StringToHash("WeaponAnimationCategory");
    private static readonly int HasGenericAttackHash = Animator.StringToHash("HasGenericAttack");
    private static readonly string[] AttackDirections = { "N", "NE", "NW", "S", "SE", "SW" };
    private const string MainHandCombatLayerName = "RightHand";

    protected override bool StopsLocomotionDuringTemporalFacing => false;

    [Header("Combat Presentation")]
    [SerializeField] private PlayerCombatNetworkController _combatController;
    [SerializeField] private PlayerWeaponEquipmentNetworkController _equipmentSource;

    [SerializeField, Min(0f)]
    private float _referenceMovementSpeed = 4f;

    private Vector2 _previousVisualPosition;
    private bool _hasPreviousVisualPosition;
    private PlayerCombatNetworkController _subscribedCombatController;
    private int _mainHandCombatLayerIndex = -1;
    private bool _hasObservedAttackState;
    private WeaponDefinition _activeWeapon;
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
        _hasObservedAttackState = false;
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
        RefreshAttackFacingLifetime();
    }

    protected override void CacheDependencies()
    {
        base.CacheDependencies();
        _combatController ??= GetComponentInParent<PlayerCombatNetworkController>();
        _equipmentSource ??= GetComponentInParent<PlayerWeaponEquipmentNetworkController>();
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
        }
    }

    private void UnsubscribeFromCombat()
    {
        if (_subscribedCombatController != null)
        {
            _subscribedCombatController.AttackPerformed -= OnAttackPerformed;
            _subscribedCombatController = null;
        }
    }

    private void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        if (AnimatorInstance == null)
        {
            return;
        }

        RefreshWeaponAnimationCategory();
        ApplyTemporalFacingDirection(attackEvent.Direction);
        _hasObservedAttackState = false;
        TriggerAttack();
    }

    private void RefreshCombatParameters()
    {
        if (AnimatorInstance == null)
        {
            return;
        }

        RefreshWeaponAnimationCategory();
    }

    private void RefreshWeaponAnimationCategory()
    {
        WeaponDefinition weapon = null;
        if (CanReadEquipmentState() &&
            _equipmentSource.TryGetEquippedDefinition(out LootDefinition definition) &&
            definition != null)
        {
            weapon = definition.WeaponDefinition;
        }

        if (_activeWeapon != weapon || (_baseController == null && AnimatorInstance.runtimeAnimatorController != null))
        {
            RefreshAttackOverrides(weapon);
        }

        AnimatorInstance.SetBool(HasGenericAttackHash, weapon != null &&
            weapon.Presentation.HasGenericAttack && _attackOverrides != null);
        AnimatorInstance.SetInteger(WeaponAnimationCategoryHash,
            (int)(weapon != null ? weapon.Presentation.AnimationCategory : WeaponAnimationCategory.None));
    }

    private void RefreshAttackOverrides(WeaponDefinition weapon)
    {
        _activeWeapon = weapon;
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
