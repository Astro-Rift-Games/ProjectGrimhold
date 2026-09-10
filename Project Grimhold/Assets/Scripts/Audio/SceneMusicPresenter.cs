using UnityEngine;

/// <summary>
/// Componente de escena que solicita al AudioManager la reproducción en loop de una pista
/// musical específica al cargar o iniciar la escena (ej: "Town" en Lobby-Town o "Raid" en Gameplay).
/// </summary>
[DisallowMultipleComponent]
public class SceneMusicPresenter : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField]
    private MusicAudioConfig _musicConfig;

    [SerializeField]
    [Tooltip("Clave de la pista musical en el MusicAudioConfig (ej: 'Town', 'Raid').")]
    private string _trackKey = "Town";

    [SerializeField, Min(0f)]
    [Tooltip("Duración de transición o crossfade si AudioManager lo soporta.")]
    private float _crossfadeDuration = 0.5f;

    private void Start()
    {
        PlaySceneMusic();
    }

    public void PlaySceneMusic()
    {
        if (AudioManager.Instance == null || _musicConfig == null)
        {
            return;
        }

        if (_musicConfig.TryGetClip(_trackKey, out CustomClip clip))
        {
            AudioManager.Instance.PlayMusic(clip, _crossfadeDuration);
        }
        else
        {
            Debug.LogWarning($"[{nameof(SceneMusicPresenter)}] No se encontró el track con clave '{_trackKey}' en el MusicAudioConfig asignado.", this);
        }
    }
}
