using UnityEngine;

[CreateAssetMenu(fileName = "WeaponAudioConfig", menuName = "Grimhold/Audio/Weapon Audio Config")]
public class WeaponAudioConfig : EntityAudioConfig
{
    [SerializeField] private AudioClipConfig _swing;
    [SerializeField] private AudioClipConfig _hit;
    [SerializeField] private AudioClipConfig _block;

    public AudioClipConfig Swing => _swing;
    public AudioClipConfig Hit => _hit;
    public AudioClipConfig Block => _block;
}
