/// <summary>Deterministic reason why provisional expedition experience was not applied.</summary>
public enum ExpeditionExperienceApplicationFailure : byte
{
    None = 0,
    InvalidState = 1,
    InvalidCategory = 2,
    InvalidAmount = 3,
    ExtractedLootRequiresExtractionResolution = 4,
    CategoryOverflow = 5,
    TotalOverflow = 6
}
