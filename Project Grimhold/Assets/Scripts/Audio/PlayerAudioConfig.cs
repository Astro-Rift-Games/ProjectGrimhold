using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAudioConfig", menuName = "Grimhold/Audio/Player Audio Config")]
public class PlayerAudioConfig : EntityAudioConfig
{
    [SerializeField] private AudioClipConfig _footstep;
    [SerializeField] private AudioClipConfig _takeDamage;
    [SerializeField] private AudioClipConfig _death;
    [SerializeField] private AudioClipConfig _attackSwing;

    public AudioClipConfig Footstep => _footstep;
    public AudioClipConfig TakeDamage => _takeDamage;
    public AudioClipConfig Death => _death;
    public AudioClipConfig AttackSwing => _attackSwing;
}
