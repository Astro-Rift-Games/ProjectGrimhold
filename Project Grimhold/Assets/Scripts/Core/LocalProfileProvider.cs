using UnityEngine;

/// <summary>
/// Provides one local ProfileId for the lifetime of the current application process.
/// Separate processes receive separate identities, including multiple builds launched
/// under the same operating-system account.
/// </summary>
public static class LocalProfileProvider
{
    private static ProfileId _processProfile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProcessProfile()
    {
        _processProfile = default;
    }

    /// <summary>
    /// Returns the identity shared by every Town and raid runner in this process.
    /// The first call creates it; application restart discards it.
    /// </summary>
    public static ProfileId GetOrCreateLocalProfile()
    {
        if (!_processProfile.IsValid)
        {
            _processProfile = new ProfileId(System.Guid.NewGuid().ToString("N"));
            Debug.Log($"[{nameof(LocalProfileProvider)}] Generated process-local ProfileId: {_processProfile.Value}");
        }

        return _processProfile;
    }
}
