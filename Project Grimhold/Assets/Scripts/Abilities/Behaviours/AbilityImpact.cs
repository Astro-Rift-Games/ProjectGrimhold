using UnityEngine;

/// <summary>
/// The damage-plus-knockback application shared by concrete ability behaviors. It owns no state: the pipeline
/// (damage resolver, knockback receiver) stays the single owner of the actual effect.
/// </summary>
internal static class AbilityImpact
{
    /// <summary>
    /// The shared damage resolver rejects a non-positive amount (no damage applied, no knockback), so an ability
    /// configured with zero damage pushes the target directly instead of going through the damage pipeline.
    /// </summary>
    public static bool UsesDamagePipeline(float damage) => damage > 0f;

    /// <summary>
    /// Damages and pushes one target along <paramref name="direction"/>. Returns false only when damage is
    /// required but the caster has no <see cref="IDamageResolver"/>; the error is logged on <paramref name="owner"/>.
    /// </summary>
    public static bool TryApply(IDamageResolver resolver, EntityId casterId, EntityId targetId, IDamageable target,
        float damage, DamageType damageType, float knockbackForce, Vector2 direction, Vector2 hitPoint,
        int tick, Object owner)
    {
        if (UsesDamagePipeline(damage))
        {
            if (resolver == null)
            {
                Debug.LogError($"{owner.GetType().Name} requires an {nameof(IDamageResolver)} on the caster to apply damage.", owner);
                return false;
            }
            resolver.Resolve(new DamageRequest(casterId, targetId, damage, damageType, direction, hitPoint, tick,
                knockbackForce));
        }
        else if (knockbackForce > 0f && target is IKnockbackReceiver receiver)
        {
            receiver.ReceiveKnockback(direction, knockbackForce);
        }
        return true;
    }
}
