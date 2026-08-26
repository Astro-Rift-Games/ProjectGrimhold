/// <summary>Reason why a definitive experience resolution was rejected.</summary>
public enum ExpeditionExperienceResolutionFailure : byte
{
    None = 0,
    MissingPolicy = 1,
    InvalidOutcome = 2,
    InvalidSnapshot = 3,
    AlreadyResolved = 4
}
