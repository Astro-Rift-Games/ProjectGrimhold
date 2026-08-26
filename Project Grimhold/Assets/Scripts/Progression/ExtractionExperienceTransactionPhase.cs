/// <summary>Authoritative retry phase for extraction Experience and Progression.</summary>
public enum ExtractionExperienceTransactionPhase : byte
{
    None = 0,
    AwaitingExperiencePreparation = 1,
    AwaitingPersistenceAck = 2,
    ExtractedLootPending = 3,
    ProgressionPending = 4,
    Complete = 5
}
