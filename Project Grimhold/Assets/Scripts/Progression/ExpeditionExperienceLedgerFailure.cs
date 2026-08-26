/// <summary>Reason why the authoritative participant ledger rejected a reward.</summary>
public enum ExpeditionExperienceLedgerFailure : byte
{
    None = 0,
    MissingStateAuthority = 1,
    MissingParticipant = 2,
    ParticipantNotRaiding = 3,
    InvalidState = 4,
    InvalidCategory = 5,
    InvalidAmount = 6,
    ExtractedLootRequiresExtractionResolution = 7,
    CategoryOverflow = 8,
    TotalOverflow = 9,
    ParticipantNotExtracted = 10,
    ExtractionNotConfirmed = 11,
    ResultSequenceMismatch = 12,
    MissingLedger = 13,
    LedgerFrozen = 14
}
