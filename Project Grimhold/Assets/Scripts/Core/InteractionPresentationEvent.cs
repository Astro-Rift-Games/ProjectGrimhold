/// <summary>
/// Minimal event payload emitted to local presentation layers during Render.
/// </summary>
public readonly struct InteractionPresentationEvent
{
    public int Sequence { get; }
    public EntityId InteractorId { get; }
    public EntityId TargetId { get; }
    public int SimulationTick { get; }
    public bool Success { get; }
    public bool IsConsumed { get; }
    public InteractionFailureReason FailureReason { get; }
    public LootTransferFailureReason LootFailureReason { get; }

    public InteractionPresentationEvent(
        int sequence,
        EntityId interactorId,
        EntityId targetId,
        int simulationTick,
        bool success,
        bool isConsumed,
        InteractionFailureReason failureReason,
        LootTransferFailureReason lootFailureReason = LootTransferFailureReason.None)
    {
        Sequence = sequence;
        InteractorId = interactorId;
        TargetId = targetId;
        SimulationTick = simulationTick;
        Success = success;
        IsConsumed = isConsumed;
        FailureReason = failureReason;
        LootFailureReason = lootFailureReason;
    }
}
