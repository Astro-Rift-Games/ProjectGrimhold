using UnityEngine;

/// <summary>
/// Provides a persistent local ProfileId. 
/// Generates a random GUID on first launch and saves it to PlayerPrefs.
/// </summary>
public static class LocalProfileProvider
{
    private const string ProfilePrefsKey = "grimhold_profile_id";

    public static ProfileId GetOrCreateLocalProfile()
    {
        string profileValue = PlayerPrefs.GetString(ProfilePrefsKey, string.Empty);
        
        if (string.IsNullOrEmpty(profileValue))
        {
            profileValue = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(ProfilePrefsKey, profileValue);
            PlayerPrefs.Save();
            Debug.Log($"[LocalProfileProvider] Generated new persistent local ProfileId: {profileValue}");
        }

        return new ProfileId(profileValue);
    }
}
