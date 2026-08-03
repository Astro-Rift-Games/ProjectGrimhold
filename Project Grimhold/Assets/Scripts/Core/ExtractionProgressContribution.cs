/// <summary>
/// Immutable authoritative contribution to one player's individual extraction quota.
/// The simulation tick is metadata and is never used as a deduplication key.
/// </summary>
public readonly struct ExtractionProgressContribution
{
    public ExtractionProgressSourceType SourceType { get; }
    public EntityId SourceId { get; }
    public long Amount { get; }
    public int SimulationTick { get; }

    public ExtractionProgressContribution(
        ExtractionProgressSourceType sourceType,
        EntityId sourceId,
        long amount,
        int simulationTick)
    {
        SourceType = sourceType;
        SourceId = sourceId;
        Amount = amount;
        SimulationTick = simulationTick;
    }
}
