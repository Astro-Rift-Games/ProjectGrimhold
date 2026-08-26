/// <summary>Outcome of applying one confirmed extracted-Loot candidate to its ledger.</summary>
public enum ExtractedLootExperienceRegistrationStatus : byte
{
    Applied = 0,
    AlreadyResolved = 1,
    Failed = 2
}
