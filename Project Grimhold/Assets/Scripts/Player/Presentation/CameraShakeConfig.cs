using UnityEngine;

/// <summary>
/// Immutable ScriptableObject that stores screen-shake parameters for
/// the two trigger sources: damage received and damage dealt (to characters only).
///
/// Belongs to the local presentation layer. Not replicated.
/// </summary>
[CreateAssetMenu(fileName = "CameraShakeConfig", menuName = "Grimhold/Presentation/CameraShakeConfig")]
public sealed class CameraShakeConfig : ScriptableObject
{
    [Header("Damage Received")]
    [Tooltip("Screen-shake intensity when the local player takes damage from any source.")]
    [SerializeField, Min(0f)]
    private float _receiveDamageIntensity = 0.4f;

    [Tooltip("Duration in seconds for the receive-damage shake.")]
    [SerializeField, Min(0f)]
    private float _receiveDamageDuration = 0.25f;

    [Header("Damage Dealt")]
    [Tooltip("Screen-shake intensity when the local player deals damage to a character.")]
    [SerializeField, Min(0f)]
    private float _dealDamageIntensity = 0.15f;

    [Tooltip("Duration in seconds for the deal-damage shake.")]
    [SerializeField, Min(0f)]
    private float _dealDamageDuration = 0.12f;

    [Header("Decay")]
    [Tooltip("Exponent applied to the trauma decay curve. Higher values decay faster.")]
    [SerializeField, Min(1f)]
    private float _decayExponent = 2f;

    [Header("Amplitude")]
    [Tooltip("Maximum positional offset in world units at full trauma.")]
    [SerializeField, Min(0f)]
    private float _maxOffset = 0.18f;

    public float ReceiveDamageIntensity => _receiveDamageIntensity;
    public float ReceiveDamageDuration   => _receiveDamageDuration;
    public float DealDamageIntensity     => _dealDamageIntensity;
    public float DealDamageDuration      => _dealDamageDuration;
    public float DecayExponent           => _decayExponent;
    public float MaxOffset               => _maxOffset;

#if UNITY_EDITOR
    private void OnValidate()
    {
        _receiveDamageIntensity = Mathf.Max(0f, _receiveDamageIntensity);
        _receiveDamageDuration  = Mathf.Max(0f, _receiveDamageDuration);
        _dealDamageIntensity    = Mathf.Max(0f, _dealDamageIntensity);
        _dealDamageDuration     = Mathf.Max(0f, _dealDamageDuration);
        _decayExponent          = Mathf.Max(1f, _decayExponent);
        _maxOffset              = Mathf.Max(0f, _maxOffset);
    }
#endif
}
