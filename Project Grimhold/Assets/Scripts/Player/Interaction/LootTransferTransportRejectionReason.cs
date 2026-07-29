/// <summary>
/// Internal transport failures which never become domain gameplay rejection reasons.
/// </summary>
public enum LootTransferTransportRejectionReason
{
    Uninitialized = 0,
    BusyWithDifferentSequence = 1,
    StaleSequence = 2,
    DependenciesUnavailable = 3
}
