using UnityEngine;

/// <summary>
/// Provee feedback de partículas cuando el character recibe daño.
/// Observa la salud replicada en el presentation layer y emite las partículas configuradas.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterDamageParticlePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private CharacterBase _characterBase;

    [Header("Configuration")]
    [SerializeField]
    [Tooltip("Sistema de partículas a reproducir cuando el character recibe daño. Debe estar hijo de este GameObject y no destruir en stop.")]
    private ParticleSystem _damageParticles;

    [SerializeField, Min(0f)]
    private float _healthEpsilon = 0.001f;

    // Runtime state
    private float _lastObservedHealth;
    private bool _isInitialized;

    private void Awake()
    {
        if (_characterBase == null)
        {
            _characterBase = GetComponentInParent<CharacterBase>();
        }
    }

    private void OnDisable()
    {
        _isInitialized = false;
        
        if (_damageParticles != null)
        {
            _damageParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void LateUpdate()
    {
        if (_characterBase == null || _damageParticles == null)
        {
            return;
        }

        // Defer tracking initialization until the character's network object is spawned and valid
        if (!_isInitialized)
        {
            if (_characterBase.Object != null && _characterBase.Object.IsValid)
            {
                _lastObservedHealth = _characterBase.Health;
                _isInitialized = true;
            }
            return;
        }

        float currentHealth = _characterBase.Health;

        // Feedback should not trigger if the character is already dead
        if (!_characterBase.IsAlive)
        {
            _lastObservedHealth = currentHealth;
            return;
        }

        if (currentHealth < _lastObservedHealth - _healthEpsilon)
        {
            ParticleEffectPlayer.PlayInPlace(_damageParticles, transform.position);
        }

        _lastObservedHealth = currentHealth;
    }
}
