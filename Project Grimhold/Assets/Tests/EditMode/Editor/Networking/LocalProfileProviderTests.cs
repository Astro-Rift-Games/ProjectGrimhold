#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

[Category("TASK143")]
public sealed class LocalProfileProviderTests
{
    [Test]
    public void GetOrCreateLocalProfile_ReturnsStableValidIdentityWithinCurrentRun()
    {
        ProfileId first = LocalProfileProvider.GetOrCreateLocalProfile();
        ProfileId second = LocalProfileProvider.GetOrCreateLocalProfile();

        Assert.That(first.IsValid, Is.True);
        Assert.That(second, Is.EqualTo(first));
        Assert.That(first.Value, Has.Length.EqualTo(32));
    }

    [Test]
    public void GetOrCreateLocalProfile_RestoresPersistedIdentityAfterCacheReset()
    {
        string previous = PlayerPrefs.GetString(LocalProfileProvider.PlayerPrefsKey, string.Empty);
        const string persisted = "abababababababababababababababab";
        try
        {
            PlayerPrefs.SetString(LocalProfileProvider.PlayerPrefsKey, persisted);
            PlayerPrefs.Save();
            ResetCachedIdentity();

            ProfileId restored = LocalProfileProvider.GetOrCreateLocalProfile();

            Assert.That(restored.Value, Is.EqualTo(persisted));
        }
        finally
        {
            if (string.IsNullOrEmpty(previous))
            {
                PlayerPrefs.DeleteKey(LocalProfileProvider.PlayerPrefsKey);
            }
            else
            {
                PlayerPrefs.SetString(LocalProfileProvider.PlayerPrefsKey, previous);
            }
            PlayerPrefs.Save();
            ResetCachedIdentity();
        }
    }

    private static void ResetCachedIdentity()
    {
        typeof(LocalProfileProvider)
            .GetMethod("ResetProcessProfile", BindingFlags.NonPublic | BindingFlags.Static)
            ?.Invoke(null, null);
    }
}
#endif
