using UnityEngine;

/// <summary>
/// Static effect granted by a gem installed in a weapon gem socket.
/// Provisional MVP contract (TASK-283): a flat damage bonus added to the weapon's base damage
/// before attribute scaling, independent of damage type, until GD-25 defines the gem catalog.
/// </summary>
[CreateAssetMenu(fileName = "GemDefinition", menuName = "Grimhold/Equipment/Gem Definition")]
public sealed class GemDefinition : ScriptableObject
{
    [SerializeField, Min(1), Tooltip("Flat damage added to the weapon base damage before scaling. Provisional Balance value.")]
    private int _damageBonus = 5;

    public int DamageBonus => _damageBonus;

    public bool TryValidate(out string error)
    {
        if (_damageBonus <= 0)
        {
            error = $"Gem damage bonus must be positive, not '{_damageBonus}'.";
            return false;
        }

        error = null;
        return true;
    }
}
