using UnityEngine;

/// <summary>Player-local normalized audio preferences, independent of scene lifetime.</summary>
public sealed class AudioVolumeSettingsStore
{
    private readonly string _keyPrefix;

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }

    public AudioVolumeSettingsStore(string keyPrefix = "Grimhold.Audio.")
    {
        _keyPrefix = keyPrefix;
        MasterVolume = Load(nameof(MasterVolume));
        MusicVolume = Load(nameof(MusicVolume));
        SfxVolume = Load(nameof(SfxVolume));
    }

    public void SetMasterVolume(float normalizedVolume)
    {
        MasterVolume = Save(nameof(MasterVolume), normalizedVolume);
    }

    public void SetMusicVolume(float normalizedVolume)
    {
        MusicVolume = Save(nameof(MusicVolume), normalizedVolume);
    }

    public void SetSfxVolume(float normalizedVolume)
    {
        SfxVolume = Save(nameof(SfxVolume), normalizedVolume);
    }

    private float Load(string channel)
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(_keyPrefix + channel, 1f));
    }

    private float Save(string channel, float normalizedVolume)
    {
        float volume = Mathf.Clamp01(normalizedVolume);
        PlayerPrefs.SetFloat(_keyPrefix + channel, volume);
        PlayerPrefs.Save();
        return volume;
    }
}
