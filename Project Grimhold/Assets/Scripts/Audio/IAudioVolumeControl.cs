public interface IAudioVolumeControl
{
    float MasterVolume { get; }
    float MusicVolume { get; }
    float SfxVolume { get; }

    void SetMasterVolume(float normalizedVolume);
    void SetMusicVolume(float normalizedVolume);
    void SetSfxVolume(float normalizedVolume);
}
