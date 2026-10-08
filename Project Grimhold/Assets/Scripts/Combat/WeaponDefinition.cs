using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Static functional configuration supplied by an equipped weapon.
/// Loot identity and presentation remain owned by <see cref="LootDefinition"/>.
/// </summary>
[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Grimhold/Combat/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Header("Weapon Statistics")]
    [SerializeField, Min(0f)] private float _baseDamage;
    [SerializeField, Min(0f)] private float _attackIntervalSeconds;
    [SerializeField, Min(0f), Tooltip("Gameplay wind-up from acceptance to the attack release (projectile spawn or melee hit). Independent of Animator and VFX.")]
    [FormerlySerializedAs("_rangedReleaseSeconds")]
    private float _attackReleaseSeconds;
    [SerializeField, Min(0f)] private float _range;
    [SerializeField, Min(0f), Tooltip("Aim stance weapons only. Seconds the stance takes to draw after it is entered. Until it is fully drawn the normal release seconds apply.")]
    private float _aimStanceDrawSeconds = DefaultAimStanceDrawSeconds;
    [SerializeField, Min(0f), Tooltip("Aim stance weapons only. Release delay of a shot fired from a fully drawn stance, instead of the normal release seconds. The attack interval is unchanged.")]
    private float _aimedReleaseSeconds = DefaultAimedReleaseSeconds;
    [SerializeField, Min(0f), Tooltip("Aim stance weapons only. Attack clip time of the drawn pose held while aiming, just before the release frame (the pre-cast frame for a staff). An aimed shot starts its animation here.")]
    private float _aimStanceDrawnClipSeconds = DefaultAimStanceDrawnClipSeconds;
    [SerializeField, Tooltip("Aim stance weapons only. Shoulder or chest point, in the character visual root space, that the left hand, the weapon and the right hand rotate about together as one block while aiming.")]
    private Vector2 _aimStanceTorsoPivot = DefaultAimStanceTorsoPivot;
    [SerializeField, Min(0f), Tooltip("Aim stance weapons only. Extends the aiming arm outward along the aim by this many units once fully drawn, ramped with the draw. 0 is a pure rigid rotation.")]
    private float _aimStanceOutwardOffset;
    [SerializeField, Tooltip("Ranged only. When set, the projectile leaves this far along the aim from the attack origin instead of the shared ranged config's spawn offset, so the shot can start at the weapon tip.")]
    private bool _overrideProjectileSpawnDistance;
    [SerializeField, Min(0f), Tooltip("Distance along the aim from the attack origin to the projectile spawn. Used only when the override is set.")]
    private float _projectileSpawnDistance;
    [SerializeField, Min(0f)] private float _staminaCost;
    [SerializeField] private DamageType _damageType = DamageType.Physical;
    [SerializeField, Min(0f)] private float _knockbackForce;

    [SerializeField]
    private WeaponHandedness _handedness;

    [Header("Attack Behavior")]
    [SerializeField]
    private AttackConfig _primaryAttack;

    [SerializeField]
    private CharacterAttribute _naturalScalingAttribute = CharacterAttribute.Strength;

    [SerializeField]
    [Tooltip("Legacy LootId-based runtime scaling. Instance modifiers are the canonical future contract.")]
    private WeaponOffensiveScaling _offensiveScaling;

    [SerializeField]
    private PresentationConfig _presentation;

    [SerializeField]
    private WeaponAttributeRequirements _attributeRequirements;

    [Header("Audio Configuration")]
    [SerializeField]
    [Tooltip("Optional. Audio configuration defining sound variations (Swing, Hit, etc.) for this weapon.")]
    private WeaponAudioConfig _audioConfig;

    public float BaseDamage => _baseDamage;
    public float AttackIntervalSeconds => _attackIntervalSeconds;
    public float AttackReleaseSeconds => _attackReleaseSeconds;
    public float AimStanceDrawSeconds => _aimStanceDrawSeconds;
    public float AimedReleaseSeconds => _aimedReleaseSeconds;
    public float AimStanceDrawnClipSeconds => _aimStanceDrawnClipSeconds;
    public Vector2 AimStanceTorsoPivot => _aimStanceTorsoPivot;
    public float AimStanceOutwardOffset => _aimStanceOutwardOffset;

    private const float DefaultAimStanceDrawSeconds = 0.45f;
    private const float DefaultAimedReleaseSeconds = 0.1f;
    private const float DefaultAimStanceDrawnClipSeconds = 0.44f;
    private static readonly Vector2 DefaultAimStanceTorsoPivot = new Vector2(0f, 0.1f);
    public bool HasProjectileSpawnDistance => _overrideProjectileSpawnDistance;
    public float ProjectileSpawnDistance => _projectileSpawnDistance;
    public float Range => _range;
    public float StaminaCost => _staminaCost;
    public DamageType DamageType => _damageType;
    public float KnockbackForce => _knockbackForce;
    public WeaponHandedness Handedness => _handedness;
    public AttackConfig PrimaryAttack => _primaryAttack;
    public CharacterAttribute NaturalScalingAttribute => _naturalScalingAttribute;

    /// <summary>Legacy LootId-based runtime scaling retained until instance identity is integrated.</summary>
    public WeaponOffensiveScaling OffensiveScaling => _offensiveScaling;
    public PresentationConfig Presentation => _presentation;
    public WeaponAttributeRequirements AttributeRequirements => _attributeRequirements;
    public WeaponAudioConfig AudioConfig => _audioConfig;

    /// <summary>Uses the shared Equipment eligibility rule for this weapon definition.</summary>
    public bool AreAttributeRequirementsSatisfiedBy(in CharacterAttributeState attributes) =>
        _attributeRequirements.IsSatisfiedBy(attributes);

    public bool TryValidate(out string error)
    {
        if (!IsFinite(_baseDamage) || _baseDamage <= 0f)
        {
            error = $"Weapon definition '{name}' has invalid base damage '{_baseDamage}'.";
            return false;
        }

        if (!IsFinite(_attackIntervalSeconds) || _attackIntervalSeconds < 0f)
        {
            error = $"Weapon definition '{name}' has invalid attack interval '{_attackIntervalSeconds}'.";
            return false;
        }

        if (!IsFinite(_attackReleaseSeconds) || _attackReleaseSeconds < 0f)
        {
            error = $"Weapon definition '{name}' has invalid attack release delay.";
            return false;
        }

        if (!IsFinite(_aimStanceDrawSeconds) || _aimStanceDrawSeconds < 0f ||
            !IsFinite(_aimedReleaseSeconds) || _aimedReleaseSeconds < 0f ||
            !IsFinite(_aimStanceDrawnClipSeconds) || _aimStanceDrawnClipSeconds < 0f ||
            !IsFinite(_aimStanceTorsoPivot.x) || !IsFinite(_aimStanceTorsoPivot.y) ||
            !IsFinite(_aimStanceOutwardOffset) || _aimStanceOutwardOffset < 0f)
        {
            error = $"Weapon definition '{name}' has an invalid aim stance draw time or aimed release delay.";
            return false;
        }

        if (_presentation.AimMode != WeaponAimMode.AimStance &&
            (_aimStanceDrawSeconds != DefaultAimStanceDrawSeconds || _aimedReleaseSeconds != DefaultAimedReleaseSeconds ||
                _aimStanceDrawnClipSeconds != DefaultAimStanceDrawnClipSeconds ||
                _aimStanceTorsoPivot != DefaultAimStanceTorsoPivot || _aimStanceOutwardOffset != 0f))
        {
            error = $"Weapon definition '{name}' sets aim stance timing but does not use the aim stance.";
            return false;
        }

        if (!IsFinite(_projectileSpawnDistance) || _projectileSpawnDistance < 0f)
        {
            error = $"Weapon definition '{name}' has invalid projectile spawn distance '{_projectileSpawnDistance}'.";
            return false;
        }

        if (_overrideProjectileSpawnDistance && _primaryAttack is not RangedAttackConfig)
        {
            error = $"Weapon definition '{name}' overrides the projectile spawn distance but is not ranged.";
            return false;
        }

        if (!IsFinite(_range) || _range <= 0f)
        {
            error = $"Weapon definition '{name}' has invalid range '{_range}'.";
            return false;
        }

        if (!IsFinite(_staminaCost) || _staminaCost < 0f)
        {
            error = $"Weapon definition '{name}' has invalid Stamina cost '{_staminaCost}'.";
            return false;
        }

        if (!IsFinite(_knockbackForce) || _knockbackForce < 0f)
        {
            error = $"Weapon definition '{name}' has invalid knockback force '{_knockbackForce}'.";
            return false;
        }

        if (!System.Enum.IsDefined(typeof(DamageType), _damageType))
        {
            error = $"Weapon definition '{name}' has unsupported damage type '{(int)_damageType}'.";
            return false;
        }

        if (!System.Enum.IsDefined(typeof(WeaponHandedness), _handedness))
        {
            error = $"Weapon definition '{name}' has unsupported handedness '{(int)_handedness}'.";
            return false;
        }

        if (_primaryAttack == null)
        {
            error = $"Weapon definition '{name}' has no primary attack configuration.";
            return false;
        }

        if (_primaryAttack is not MeleeAttackConfig && _primaryAttack is not RangedAttackConfig)
        {
            error = $"Weapon definition '{name}' uses unsupported attack config type '{_primaryAttack.GetType().Name}'.";
            return false;
        }

        if (!_primaryAttack.TryValidate(out string attackError))
        {
            error = $"Weapon definition '{name}' has an invalid primary attack: {attackError}";
            return false;
        }

        if (_primaryAttack is MeleeAttackConfig meleeConfig && _range < meleeConfig.Radius)
        {
            error = $"Weapon definition '{name}' range {_range} must be at least its melee radius {meleeConfig.Radius}.";
            return false;
        }

        if (_naturalScalingAttribute != CharacterAttribute.Strength &&
            _naturalScalingAttribute != CharacterAttribute.Dexterity &&
            _naturalScalingAttribute != CharacterAttribute.Intelligence)
        {
            error = $"Weapon definition '{name}' has unsupported natural scaling attribute '{_naturalScalingAttribute}'.";
            return false;
        }

        if (!_offensiveScaling.TryValidate(out string scalingError))
        {
            error = $"Weapon definition '{name}' has invalid offensive scaling: {scalingError}";
            return false;
        }

        if (_offensiveScaling.HasScaling &&
            _offensiveScaling.Attribute != _naturalScalingAttribute)
        {
            error = $"Weapon definition '{name}' legacy offensive scaling attribute " +
                $"'{_offensiveScaling.Attribute}' must match natural scaling attribute " +
                $"'{_naturalScalingAttribute}'.";
            return false;
        }

        if (!_presentation.TryValidate(out string presentationError))
        {
            error = $"Weapon definition '{name}' has invalid presentation: {presentationError}";
            return false;
        }

        if (_handedness != WeaponHandedness.TwoHanded &&
            _presentation.SecondHand != SecondHandPresentation.HoldsSecondaryGrip)
        {
            error = $"Weapon definition '{name}' selects a second-hand presentation but is not two-handed.";
            return false;
        }

        // A weapon-driven rig places both hands on the weapon, so only a two-handed weapon, which blocks the Off
        // Hand, may use it. Both hands follow authored motion relative to the weapon instead of the second hand
        // reaching for a secondary grip derived from a hand-held weapon.
        if (_presentation.Rig == WeaponRig.WeaponDriven &&
            (_handedness != WeaponHandedness.TwoHanded ||
                _presentation.SecondHand != SecondHandPresentation.FollowsAuthoredMotion))
        {
            error = $"Weapon definition '{name}' weapon-driven rig requires a two-handed weapon whose second hand follows its authored motion.";
            return false;
        }

        // Two-handed generic attacks that hold the weapon are baked with the second hand on this handle point.
        if (_handedness == WeaponHandedness.TwoHanded && _presentation.HasGenericAttack &&
            _presentation.SecondHand == SecondHandPresentation.HoldsSecondaryGrip &&
            _presentation.SecondaryGripPoint == _presentation.GripPoint)
        {
            error = $"Weapon definition '{name}' two-handed generic attack requires a secondary grip point distinct from its grip point.";
            return false;
        }

        if (_presentation.AimMode == WeaponAimMode.FreeAim)
        {
            if (_primaryAttack is not RangedAttackConfig)
            {
                error = $"Weapon definition '{name}' free aim requires a ranged attack.";
                return false;
            }
        }

        if (_presentation.AimMode == WeaponAimMode.AimStance)
        {
            if (_primaryAttack is not RangedAttackConfig)
            {
                error = $"Weapon definition '{name}' aim stance requires a ranged attack.";
                return false;
            }

            // The stance takes the secondary action, so it must never meet a shield: only a two-handed weapon,
            // which blocks the Off Hand, may use it.
            if (_handedness != WeaponHandedness.TwoHanded)
            {
                error = $"Weapon definition '{name}' aim stance requires a two-handed weapon.";
                return false;
            }

            if (_aimStanceDrawnClipSeconds > _attackReleaseSeconds)
            {
                error = $"Weapon definition '{name}' aim stance drawn frame must not be after the attack release.";
                return false;
            }
        }

        if (!_attributeRequirements.TryValidate(out string requirementError))
        {
            error = $"Weapon definition '{name}' has invalid attribute requirements: {requirementError}";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    [System.Serializable]
    public struct PresentationConfig
    {
        [SerializeField]
        private Vector2 _stanceOffset;

        [SerializeField]
        private Vector2 _gripPoint;

        [SerializeField]
        private float _angleCorrection;

        public Vector2 StanceOffset => _stanceOffset;
        public Vector2 GripPoint => _gripPoint;
        public float AngleCorrection => _angleCorrection;

        [SerializeField] private DirectionalAttackAnimationSet _attackAnimationSet;

        public DirectionalAttackAnimationSet AttackAnimationSet => _attackAnimationSet;
        public bool HasGenericAttack => _attackAnimationSet != null && _attackAnimationSet.IsComplete;

        [SerializeField] private AttackVfxDefinition _attackVfx;
        public AttackVfxDefinition AttackVfx => _attackVfx;

        [SerializeField, Tooltip("Blade tip in the weapon sprite's local units, the same space as the grip point.")]
        private Vector2 _bladeTip;
        public Vector2 BladeTip => _bladeTip;

        [SerializeField, Tooltip("Second-hand point inside the handle of a two-handed weapon, in the weapon sprite's local units like the grip point.")]
        private Vector2 _secondaryGripPoint;
        public Vector2 SecondaryGripPoint => _secondaryGripPoint;

        [SerializeField, Tooltip("How a two-handed attack presents the second hand: on the secondary grip point, or along its own authored motion.")]
        private SecondHandPresentation _secondHand;
        public SecondHandPresentation SecondHand => _secondHand;

        [SerializeField, Tooltip("What owns the held visual's pose: the main hand through MainHandGrip, or the weapon itself through WeaponPose.")]
        private WeaponRig _rig;
        public WeaponRig Rig => _rig;

        [SerializeField, Tooltip("How the held visual follows the aim: the baked six-bucket facing, or the continuous 360 degree aim. Free aim pins the second hand to the secondary grip point.")]
        private WeaponAimMode _aimMode;
        public WeaponAimMode AimMode => _aimMode;

        [SerializeField, Tooltip("Optional. Sprite sequence the held weapon visual shows during its attack clip, such as a bow drawing its string. It only swaps the sprite.")]
        private WeaponAttackSpriteAnimation _attackSpriteAnimation;
        public WeaponAttackSpriteAnimation AttackSpriteAnimation => _attackSpriteAnimation;

        /// <summary>Distance from the grip point to the blade tip, in weapon sprite local units.</summary>
        public float BladeReach => Vector2.Distance(_gripPoint, _bladeTip);

        public AnimationClip GetAttackClip(int direction) =>
            _attackAnimationSet != null ? _attackAnimationSet.GetAttackClip(direction) : null;

        public bool TryValidate(out string error)
        {
            if (!IsFinite(_stanceOffset.x) || !IsFinite(_stanceOffset.y) ||
                !IsFinite(_gripPoint.x) || !IsFinite(_gripPoint.y) ||
                !IsFinite(_angleCorrection) ||
                !IsFinite(_bladeTip.x) || !IsFinite(_bladeTip.y) ||
                !IsFinite(_secondaryGripPoint.x) || !IsFinite(_secondaryGripPoint.y))
            {
                error = "stance offset, grip point, angle correction, blade tip and secondary grip point must be finite.";
                return false;
            }

            if (_attackVfx != null)
            {
                if (!HasGenericAttack)
                {
                    error = "attack VFX requires a complete attack animation set.";
                    return false;
                }

                // Only art resolved from the weapon reach needs a blade; a bow shot is placed from its pose alone.
                if (_attackVfx.UsesWeaponReach && BladeReach <= 0f)
                {
                    error = "attack VFX requires a blade tip distinct from the grip point.";
                    return false;
                }

                if (!_attackVfx.TryValidateBladeReach(BladeReach, out error))
                    return false;
            }

            if (_attackSpriteAnimation != null &&
                !_attackSpriteAnimation.TryValidateAttackSet(_attackAnimationSet, out error))
            {
                return false;
            }

            if (!System.Enum.IsDefined(typeof(SecondHandPresentation), _secondHand))
            {
                error = $"second-hand presentation '{(int)_secondHand}' is unsupported.";
                return false;
            }

            if (!System.Enum.IsDefined(typeof(WeaponRig), _rig))
            {
                error = $"weapon rig '{(int)_rig}' is unsupported.";
                return false;
            }

            if (!System.Enum.IsDefined(typeof(WeaponAimMode), _aimMode))
            {
                error = $"weapon aim mode '{(int)_aimMode}' is unsupported.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
