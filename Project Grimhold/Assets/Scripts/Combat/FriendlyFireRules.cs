using UnityEngine;

/// <summary>Friendly-fire policy on resolved facts. HP remains owned by the target.</summary>
public static class FriendlyFireRules
{
    public static bool IsBlocked(bool isAlly, bool friendlyFireEnabled) => isAlly && !friendlyFireEnabled;

    public static float ScaleDamage(float amount, bool isAlly, float allyMultiplier) =>
        isAlly ? amount * Mathf.Max(0f, allyMultiplier) : amount;

    /// <summary>Called after mitigation; never heals an already sub-floor target.</summary>
    public static float ClampDamage(float health, float mitigatedDamage, bool isAlly) =>
        isAlly ? Mathf.Min(Mathf.Max(0f, mitigatedDamage), Mathf.Max(0f, health - 1f)) : mitigatedDamage;
}
