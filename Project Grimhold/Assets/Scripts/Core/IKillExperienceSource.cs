/// <summary>
/// Canonical entity capability that owns one configurable Kill Experience reward.
/// </summary>
public interface IKillExperienceSource : IEntity
{
    long KillExperience { get; }
    bool IsAvailable { get; }
    bool IsAssistGranted { get; }

    /// <summary>
    /// Requests ledger application first and consumes this source only after acceptance.
    /// Must execute synchronously under State Authority.
    /// </summary>
    bool TryGrantTo(PlayerExpeditionExperienceLedger ledger);

    /// <summary>
    /// Grants the assist reward (typically 50% of the kill experience).
    /// Does not consume the source, as multiple participants may assist.
    /// </summary>
    bool TryGrantAssistTo(PlayerExpeditionExperienceLedger ledger);

    void MarkAssistGranted();
}
