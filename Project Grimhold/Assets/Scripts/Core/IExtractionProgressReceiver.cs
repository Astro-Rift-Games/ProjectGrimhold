/// <summary>
/// Player capability that accepts authoritative contributions to an individual extraction quota.
/// </summary>
public interface IExtractionProgressReceiver : IEntity
{
    /// <summary>
    /// Applies a contribution during the current authoritative simulation tick.
    /// Producers own all one-shot and deduplication guarantees.
    /// </summary>
    bool TryApplyContribution(in ExtractionProgressContribution contribution);
}
