/// <summary>Receives original knockback power (PE, Doc 18) independently of damage mitigation.</summary>
public interface IKnockbackReceiver
{
    /// <summary>Current eligibility; callers must snapshot this BEFORE applying impact damage.</summary>
    bool IsKnockbackEligible { get; }

    /// <summary>
    /// Mitigates original PE by RE and requests displacement using the impact eligibility snapshot.
    /// A hit authorized while Active may continue through Downed; already Downed targets reject impacts.
    /// </summary>
    KnockbackDecision ReceiveKnockback(in KnockbackRequest request);
}
