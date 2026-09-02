using UnityEngine;

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
    [SerializeField, Min(0f)] private float _range;
    [SerializeField, Min(0f)] private float _staminaCost;
    [SerializeField] private DamageType _damageType = DamageType.Physical;
    [SerializeField, Min(0f)] private float _knockbackForce;

    [Header("Attack Behavior")]
    [SerializeField]
    private AttackConfig _primaryAttack;

    [SerializeField]
    private WeaponOffensiveScaling _offensiveScaling;

    [SerializeField]
    private PresentationConfig _presentation;

    [SerializeField]
    private WeaponAttributeRequirements _attributeRequirements;

    public float BaseDamage => _baseDamage;
    public float AttackIntervalSeconds => _attackIntervalSeconds;
    public float Range => _range;
    public float StaminaCost => _staminaCost;
    public DamageType DamageType => _damageType;
    public float KnockbackForce => _knockbackForce;
    public AttackConfig PrimaryAttack => _primaryAttack;
    public WeaponOffensiveScaling OffensiveScaling => _offensiveScaling;
    public PresentationConfig Presentation => _presentation;
    public WeaponAttributeRequirements AttributeRequirements => _attributeRequirements;

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

        if (!_offensiveScaling.TryValidate(out string scalingError))
        {
            error = $"Weapon definition '{name}' has invalid offensive scaling: {scalingError}";
            return false;
        }

        if (!_presentation.TryValidate(out string presentationError))
        {
            error = $"Weapon definition '{name}' has invalid presentation: {presentationError}";
            return false;
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

        [SerializeField]
        [Tooltip("The total angle distance the weapon covers during a melee swing.")]
        private float _swingArc;

        [SerializeField]
        [Tooltip("The duration in seconds of the procedural swing animation.")]
        private float _swingDuration;

        public Vector2 StanceOffset => _stanceOffset;
        public Vector2 GripPoint => _gripPoint;
        public float AngleCorrection => _angleCorrection;
        public float SwingArc => _swingArc == 0f ? 90f : _swingArc;
        public float SwingDuration => _swingDuration == 0f ? 0.15f : _swingDuration;

        public bool TryValidate(out string error)
        {
            if (!IsFinite(_stanceOffset.x) || !IsFinite(_stanceOffset.y) ||
                !IsFinite(_gripPoint.x) || !IsFinite(_gripPoint.y) ||
                !IsFinite(_angleCorrection) || !IsFinite(_swingArc))
            {
                error = "stance offset, grip point and angle correction must be finite.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
