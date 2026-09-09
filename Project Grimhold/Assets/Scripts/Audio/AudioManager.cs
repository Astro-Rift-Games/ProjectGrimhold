using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Gestor central de audio. Persiste entre escenas y maneja un pool de AudioSources para SFX,
/// además de un AudioSource dedicado para música.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer Configuration")]
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private AudioMixerGroup _sfxGroup;
    [SerializeField] private AudioMixerGroup _musicGroup;

    [Header("Pool Configuration")]
    [SerializeField, Min(1)] private int _sfxPoolSize = 16;
    
    private AudioSource[] _sfxSources;
    private int _currentSfxIndex;

    private AudioSource _musicSource;

    private void Awake()
    {
        // Singleton pattern con persistencia
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null); // Debe estar en el root para DontDestroyOnLoad
        DontDestroyOnLoad(gameObject);

        InitializeSfxPool();
        InitializeMusicSource();
    }

    private void InitializeSfxPool()
    {
        _sfxSources = new AudioSource[_sfxPoolSize];
        GameObject sfxParent = new GameObject("SFX_Pool");
        sfxParent.transform.SetParent(transform);

        for (int i = 0; i < _sfxPoolSize; i++)
        {
            GameObject go = new GameObject($"SFX_Source_{i}");
            go.transform.SetParent(sfxParent.transform);
            
            AudioSource source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = _sfxGroup;
            source.playOnAwake = false;
            
            _sfxSources[i] = source;
        }
    }

    private void InitializeMusicSource()
    {
        GameObject go = new GameObject("Music_Source");
        go.transform.SetParent(transform);
        
        _musicSource = go.AddComponent<AudioSource>();
        _musicSource.outputAudioMixerGroup = _musicGroup;
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
    }

    /// <summary>
    /// Reproduce un SFX utilizando un AudioSource del pool.
    /// Soporta posicionamiento espacial (3D) si el config lo indica.
    /// </summary>
    public void PlaySfx(in CustomClip config, Vector3 worldPosition = default)
    {
        if (!config.IsValid) return;

        AudioSource source = GetNextSfxSource();
        
        source.transform.position = worldPosition;
        ApplyConfigToSource(config, source);
        
        source.Play();
    }

    /// <summary>
    /// Reproduce un SFX inyectando la configuración en un AudioSource externo.
    /// Útil para objetos que controlan su propio AudioSource (ej: un motor loopeando).
    /// </summary>
    public void PlaySfxAttached(in CustomClip config, AudioSource source)
    {
        if (!config.IsValid || source == null) return;

        ApplyConfigToSource(config, source);
        source.Play();
    }

    private AudioSource GetNextSfxSource()
    {
        // Primero intenta encontrar uno libre
        for (int i = 0; i < _sfxPoolSize; i++)
        {
            if (!_sfxSources[i].isPlaying)
            {
                return _sfxSources[i];
            }
        }

        // Si están todos ocupados, "roba" el más antiguo rotando el índice
        AudioSource stolenSource = _sfxSources[_currentSfxIndex];
        _currentSfxIndex = (_currentSfxIndex + 1) % _sfxPoolSize;
        return stolenSource;
    }

    private void ApplyConfigToSource(in CustomClip config, AudioSource source)
    {
        source.clip = config.GetRandomClip();
        source.volume = config.Volume;
        source.pitch = config.Pitch;
        source.spatialBlend = config.SpatialBlend;
        source.loop = config.Loop;
    }

    /// <summary>
    /// Reproduce música de fondo en el canal de música.
    /// (El crossfade se puede implementar aquí a futuro).
    /// </summary>
    public void PlayMusic(in CustomClip config, float crossfadeDuration = 0f)
    {
        if (!config.IsValid) return;

        ApplyConfigToSource(config, _musicSource);
        
        if (!_musicSource.isPlaying)
        {
            _musicSource.Play();
        }
    }

    public void StopMusic(float fadeDuration = 0f)
    {
        _musicSource.Stop();
    }

    // =========================================================================
    // Control de Volumen Global
    // Mapea valores normalizados (0-1) a decibeles (-80dB a 0dB aprox)
    // =========================================================================

    public void SetMasterVolume(float normalizedVolume)
    {
        SetVolumeParam("MasterVolume", normalizedVolume);
    }

    public void SetSfxVolume(float normalizedVolume)
    {
        SetVolumeParam("SFXVolume", normalizedVolume);
    }

    public void SetMusicVolume(float normalizedVolume)
    {
        SetVolumeParam("MusicVolume", normalizedVolume);
    }

    private void SetVolumeParam(string paramName, float normalizedVolume)
    {
        if (_mixer == null) return;

        // Evitar log10 de 0
        float clampedVol = Mathf.Clamp(normalizedVolume, 0.0001f, 1f);
        float db = Mathf.Log10(clampedVol) * 20f;
        _mixer.SetFloat(paramName, db);
    }
}
