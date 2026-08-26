/// <summary>Integration result for one authoritative Progression finalization attempt.</summary>
public enum PlayerExpeditionProgressionFinalizationStatus : byte
{
    Success = 0,
    MissingStateAuthority = 1,
    MissingOrInvalidBaseline = 2,
    IncompatibleLifecycle = 3,
    AlreadyCommitted = 4,
    ResolutionFailed = 5,
    ApplicationFailed = 6
}
