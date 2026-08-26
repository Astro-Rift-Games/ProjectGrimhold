/// <summary>Immutable integration result retaining any underlying deterministic failure.</summary>
public readonly struct PlayerExpeditionProgressionFinalizationResult
{
    public PlayerExpeditionProgressionFinalizationStatus Status { get; }
    public ExpeditionExperienceResolutionFailure ResolutionFailure { get; }
    public ConsolidatedExperienceApplicationFailure ApplicationFailure { get; }

    public bool IsCompleted =>
        Status == PlayerExpeditionProgressionFinalizationStatus.Success ||
        Status == PlayerExpeditionProgressionFinalizationStatus.AlreadyCommitted;

    private PlayerExpeditionProgressionFinalizationResult(
        PlayerExpeditionProgressionFinalizationStatus status,
        ExpeditionExperienceResolutionFailure resolutionFailure,
        ConsolidatedExperienceApplicationFailure applicationFailure)
    {
        Status = status;
        ResolutionFailure = resolutionFailure;
        ApplicationFailure = applicationFailure;
    }

    public static PlayerExpeditionProgressionFinalizationResult FromStatus(
        PlayerExpeditionProgressionFinalizationStatus status) =>
        new(status, ExpeditionExperienceResolutionFailure.None,
            ConsolidatedExperienceApplicationFailure.None);

    public static PlayerExpeditionProgressionFinalizationResult FromResolutionFailure(
        ExpeditionExperienceResolutionFailure failure) =>
        new(PlayerExpeditionProgressionFinalizationStatus.ResolutionFailed, failure,
            ConsolidatedExperienceApplicationFailure.None);

    public static PlayerExpeditionProgressionFinalizationResult FromApplicationFailure(
        ConsolidatedExperienceApplicationFailure failure) =>
        new(PlayerExpeditionProgressionFinalizationStatus.ApplicationFailed,
            ExpeditionExperienceResolutionFailure.None, failure);
}
