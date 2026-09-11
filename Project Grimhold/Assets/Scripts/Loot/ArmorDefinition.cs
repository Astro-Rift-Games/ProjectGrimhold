using UnityEngine;

/// <summary>Static functional configuration referenced by an armor loot template.</summary>
[CreateAssetMenu(fileName = "ArmorDefinition", menuName = "Grimhold/Loot/Armor Definition")]
public sealed class ArmorDefinition : ScriptableObject
{
    [SerializeField, Min(0)] private int _physicalDefense;
    [SerializeField, Min(0)] private int _magicalDefense;
    [SerializeField] private MaximumResourceModifier _maximumResourceModifier;

    public int PhysicalDefense => _physicalDefense;
    public int MagicalDefense => _magicalDefense;
    public MaximumResourceModifier MaximumResourceModifier => _maximumResourceModifier;

    public bool TryValidate(out string error)
    {
        if (_physicalDefense < 0 || _magicalDefense < 0)
        {
            error = $"Armor definition '{name}' cannot have negative defense values.";
            return false;
        }

        if (!_maximumResourceModifier.TryValidate(out string modifierError))
        {
            error = $"Armor definition '{name}' has an invalid maximum resource modifier: {modifierError}";
            return false;
        }

        if (_physicalDefense == 0 && _magicalDefense == 0 && !_maximumResourceModifier.IsPresent)
        {
            error = $"Armor definition '{name}' has no functional statistics.";
            return false;
        }

        if (!_maximumResourceModifier.IsPresent)
        {
            error = $"Armor definition '{name}' must define a maximum resource modifier.";
            return false;
        }

        error = null;
        return true;
    }
}
