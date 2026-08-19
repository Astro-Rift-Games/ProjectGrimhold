/// <summary>
/// Minimal contract returned by <see cref="SessionConnectionCoordinator.TryStoreRaidLaunchContext"/>
/// so the Town preparation can distinguish a transient retry from a permanent local rejection.
/// </summary>
public enum RaidLaunchPreparationResult
{
    /// <summary>A launch ticket is stored for this launch revision.</summary>
    Success,

    /// <summary>Nothing is wrong yet; the peer may retry on a later replicated snapshot.</summary>
    NotReady,

    /// <summary>This peer cannot prepare the expedition for this launch revision.</summary>
    Rejected,

    /// <summary>Required infrastructure or content configuration is missing.</summary>
    ConfigurationError
}
