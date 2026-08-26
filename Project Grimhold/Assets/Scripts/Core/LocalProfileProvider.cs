using UnityEngine;

/// <summary>
/// Provides the stable local ProfileId shared by every session and application restart.
/// </summary>
public static class LocalProfileProvider
{
    internal const string PlayerPrefsKey = "grimhold_profile_id";
    private static ProfileId _processProfile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProcessProfile()
    {
        _processProfile = default;
    }

    /// <summary>
    /// Returns the identity shared by every Town and raid runner on this installation.
    /// The first call creates and durably stores it.
    /// </summary>
    public static ProfileId GetOrCreateLocalProfile()
    {
        if (!_processProfile.IsValid)
        {
            string stored = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                try
                {
                    _processProfile = new ProfileId(stored);
                }
                catch (System.ArgumentException)
                {
                    _processProfile = default;
                }
            }

            if (!_processProfile.IsValid)
            {
                _processProfile = new ProfileId(System.Guid.NewGuid().ToString("N"));
                PlayerPrefs.SetString(PlayerPrefsKey, _processProfile.Value);
                PlayerPrefs.Save();
                Debug.Log($"[{nameof(LocalProfileProvider)}] Generated persistent ProfileId: {_processProfile.Value}");
            }
        }

        return _processProfile;
    }
}
