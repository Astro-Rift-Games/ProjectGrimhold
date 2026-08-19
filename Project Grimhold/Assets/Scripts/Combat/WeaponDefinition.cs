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

    public AttackConfig PrimaryAttack => _primaryAttack;

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

        error = null;
        return true;
    }
}
