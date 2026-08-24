using UnityEngine;

/// <summary>
/// Static functional configuration supplied by an equipped weapon.
/// Loot identity and presentation remain owned by <see cref="LootDefinition"/>.
/// </summary>
[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Grimhold/Combat/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [SerializeField]
    private AttackConfig _primaryAttack;

    [SerializeField]
    private PresentationConfig _presentation;

    public AttackConfig PrimaryAttack => _primaryAttack;
    public PresentationConfig Presentation => _presentation;

    public bool TryValidate(out string error)
    {
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

        if (!_presentation.TryValidate(out string presentationError))
        {
            error = $"Weapon definition '{name}' has invalid presentation: {presentationError}";
            return false;
        }

        error = null;
        return true;
    }

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
