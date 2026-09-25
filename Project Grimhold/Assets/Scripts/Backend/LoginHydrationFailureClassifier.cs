using Grimhold.Backend;

/// <summary>
/// Pure static classifier that maps a backend hydration error to the correct LoginFlowStatus
/// and a user-facing message. Extracted to allow unit testing without MonoBehaviour or HTTP.
/// </summary>
public static class LoginHydrationFailureClassifier
{
    public static LoginFlowResult ClassifyHydrationFailure(BackendError error, string component)
    {
        var isNetwork = BackendErrorUtility.IsTransportFailure(error.error);
        return LoginFlowResult.Failure(
            isNetwork ? LoginFlowStatus.NetworkError : LoginFlowStatus.HydrationFailed,
            $"Could not load your {component}. Please try again.");
    }

    public static LoginFlowResult ClassifyRevisionMismatch(int inventoryRevision, int progressionRevision)
    {
        return LoginFlowResult.Failure(
            LoginFlowStatus.HydrationFailed,
            "Inconsistent server state (Hydration Revision Mismatch). Please try again.");
    }
}
