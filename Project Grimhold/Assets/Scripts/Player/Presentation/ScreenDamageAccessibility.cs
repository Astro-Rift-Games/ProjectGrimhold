using UnityEngine;

/// <summary>Local scalar preference only; no mutable global presentation state.</summary>
public static class ScreenDamageAccessibility
{
    private const string Key = "Grimhold.Presentation.ScreenDamageIntensity";
    public static float Intensity => Mathf.Clamp01(PlayerPrefs.GetFloat(Key, 1f));
    public static void SetIntensity(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        PlayerPrefs.SetFloat(Key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }
}
