using UnityEngine;

[CreateAssetMenu(fileName = "EnemyAudioConfig", menuName = "Grimhold/Audio/Enemy Audio Config")]
public class EnemyAudioConfig : EntityAudioConfig
{
    [SerializeField] private AudioClipConfig _alert;
    [SerializeField] private AudioClipConfig _attack;
    [SerializeField] private AudioClipConfig _takeDamage;
    [SerializeField] private AudioClipConfig _death;

    public AudioClipConfig Alert => _alert;
    public AudioClipConfig Attack => _attack;
    public AudioClipConfig TakeDamage => _takeDamage;
    public AudioClipConfig Death => _death;
}
