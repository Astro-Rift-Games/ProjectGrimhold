using UnityEngine;

/// <summary>Owns active knockback and strict effective-PE replacement decisions.</summary>
public interface IKnockbackMotor
{
    /// <summary>
    /// Starts or strictly replaces displacement along positive direction using PE after RE mitigation.
    /// Eligibility is the pre-damage impact snapshot, not current Downed state. Requires State Authority;
    /// a non-authoritative call returns KeptActive as a no-mutation result, not an absorption decision.
    /// </summary>
    KnockbackDecision TryApplyKnockback(Vector2 direction, float effectivePower, bool wasEligibleAtImpact);
}
