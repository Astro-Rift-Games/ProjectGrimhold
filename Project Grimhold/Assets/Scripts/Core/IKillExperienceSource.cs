/// <summary>
/// Canonical entity capability that owns one configurable Kill Experience reward.
/// </summary>
public interface IKillExperienceSource : IEntity
{
    long KillExperience { get; }
    bool IsAvailable { get; }
    bool IsAssistResolutionCompleted { get; }

    /// <summary>
    /// Requests ledger application first and consumes this source only after acceptance.
    /// Must execute synchronously under State Authority.
    /// </summary>
    bool TryGrantTo(PlayerExpeditionExperienceLedger ledger);

    /// <summary>
    /// Freezes the eligible contributors so that subsequent attempts are idempotent.
    /// </summary>
    void InitializeAssistCandidates(int eligibleMask);

    /// <summary>
    /// Grants the assist reward to the specified participant if they are eligible and have not received it yet.
    /// </summary>
    bool TryGrantAssistTo(RaidParticipantId id, PlayerExpeditionExperienceLedger ledger);
}
