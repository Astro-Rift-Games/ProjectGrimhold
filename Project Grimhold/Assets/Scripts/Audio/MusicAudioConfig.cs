using UnityEngine;

[CreateAssetMenu(fileName = "MusicAudioConfig", menuName = "Grimhold/Audio/Music Audio Config")]
public class MusicAudioConfig : EntityAudioConfig
{
    [SerializeField] private AudioClipConfig _track;

    public AudioClipConfig Track => _track;
}
