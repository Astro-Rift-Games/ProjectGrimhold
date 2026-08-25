/// <summary>
/// Canonical entity capability that owns one configurable Kill Experience reward.
/// </summary>
public interface IKillExperienceSource : IEntity
{
    long KillExperience { get; }
    bool IsAvailable { get; }

    /// <summary>
    /// Requests ledger application first and consumes this source only after acceptance.
    /// Must execute synchronously under State Authority.
    /// </summary>
    bool TryGrantTo(PlayerExpeditionExperienceLedger ledger);
}
