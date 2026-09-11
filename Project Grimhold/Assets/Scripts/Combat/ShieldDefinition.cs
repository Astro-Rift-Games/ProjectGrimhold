using UnityEngine;

/// <summary>Static defensive behavior supplied by an equipped Off Hand shield.</summary>
[CreateAssetMenu(fileName = "ShieldDefinition", menuName = "Grimhold/Combat/Shield Definition")]
public sealed class ShieldDefinition : ScriptableObject
{
    [SerializeField, Range(0f, 1f)] private float _damageReduction = 0.5f;
    [SerializeField, Range(0.0001f, 360f)] private float _defensiveConeDegrees = 120f;

    public float DamageReduction => _damageReduction;
    public float DefensiveConeDegrees => _defensiveConeDegrees;

    public bool TryValidate(out string error)
    {
        if (!IsFinite(_damageReduction) || _damageReduction <= 0f || _damageReduction >= 1f)
        {
            error = $"Shield definition '{name}' has invalid damage reduction '{_damageReduction}'.";
            return false;
        }

        if (!IsFinite(_defensiveConeDegrees) ||
            _defensiveConeDegrees <= 0f || _defensiveConeDegrees > 360f)
        {
            error = $"Shield definition '{name}' has invalid defensive cone '{_defensiveConeDegrees}'.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
