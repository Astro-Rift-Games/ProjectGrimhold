using UnityEngine;

/// <summary>
/// Provides one local ProfileId for the lifetime of the current application process.
/// Separate processes receive separate identities, including multiple builds launched
/// under the same operating-system account.
/// </summary>
public static class LocalProfileProvider
{
    private static ProfileId _remoteCharacterId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProcessProfile()
    {
        _remoteCharacterId = default;
    }

    /// <summary>
    /// Returns the identity shared by every Town and raid runner in this process.
    /// Default if not authenticated.
    /// </summary>
    public static ProfileId GetOrCreateLocalProfile()
    {
        return _remoteCharacterId;
    }

    public static void SetRemoteCharacterId(ProfileId characterId)
    {
        _remoteCharacterId = characterId;
        Debug.Log($"[{nameof(LocalProfileProvider)}] Remote CharacterId set: {characterId.Value}");
    }

    public static void ClearRemoteCharacterId()
    {
        _remoteCharacterId = default;
        Debug.Log($"[{nameof(LocalProfileProvider)}] Remote CharacterId cleared.");
    }
}
