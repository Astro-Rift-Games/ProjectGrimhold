using Fusion;

/// <summary>
/// Replicated causal exposure register of one target (Doc 18 §6.5, Doc 19 §8). Entity ids are raw
/// <see cref="EntityId"/> values; 0 means none. Mutated only through <see cref="HazardExposureCausalityRules"/>.
/// </summary>
public struct HazardExposureCausalityState : INetworkStruct
{
    /// <summary>Player whose accepted knockback is still displacing, or just displaced, the target.</summary>
    public int PendingPusherEntityId;
    public int PendingKnockbackSequence;
    /// <summary>Latest tick the pending knockback was observed active.</summary>
    public int PendingActiveTick;
    /// <summary>Hazard the target already occupied when the pending push was accepted.</summary>
    public int PendingExcludedHazardId;

    /// <summary>Latest hazard presence report, linked or not; detects pushes inside a hazard.</summary>
    public int LastPresenceHazardId;
    public int LastPresenceTick;

    /// <summary>Concrete hazard, pusher and knockback action sustaining the causal chain.</summary>
    public int LinkedHazardId;
    public int LinkedPusherEntityId;
    public int LinkedKnockbackSequence;
    public int LinkedPresenceTick;

    /// <summary>Audit of the last broken causal chain and the event that broke it.</summary>
    public HazardCausalityBreakReason LastBreakReason;
    public int LastBrokenHazardId;
    public int LastBrokenPusherEntityId;
    public int LastBrokenKnockbackSequence;
    public int LastBreakTick;
}
