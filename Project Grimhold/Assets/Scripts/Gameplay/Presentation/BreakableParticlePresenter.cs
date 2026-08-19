using UnityEngine;

/// <summary>
/// Provee feedback de partículas cuando un BreakableObject recibe daño o es destruido.
/// Observa las propiedades sincronizadas en la capa de presentación (LateUpdate).
/// </summary>
[DisallowMultipleComponent]
public sealed class BreakableParticlePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private BreakableObject _breakableObject;

    [Header("Configuration")]
    [SerializeField]
    [Tooltip("Partículas que se emiten al recibir daño (sin ser destruido). Debe estar hijo de este GO y no destruir en stop.")]
    private ParticleSystem _damageParticles;

    [SerializeField]
    [Tooltip("Partículas que se emiten al destruirse. Debe estar hijo de este GO y no destruir en stop.")]
    private ParticleSystem _destroyParticles;

    [SerializeField, Min(0f)]
    private float _healthEpsilon = 0.001f;

    // Runtime state
    private float _lastObservedHealth;
    private bool _wasDestroyedLastFrame;
    private bool _isInitialized;

    private void Awake()
    {
        if (_breakableObject == null)
        {
            _breakableObject = GetComponentInParent<BreakableObject>();
        }
    }

    private void OnDisable()
    {
        _isInitialized = false;

        if (_damageParticles != null)
        {
            _damageParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (_destroyParticles != null)
        {
            _destroyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void LateUpdate()
    {
        if (_breakableObject == null)
        {
            return;
        }

        // Wait until the network object is valid to start tracking
        if (!_isInitialized)
        {
            if (_breakableObject.Object != null && _breakableObject.Object.IsValid)
            {
                _lastObservedHealth = _breakableObject.Health;
                _wasDestroyedLastFrame = _breakableObject.IsDestroyed;
                _isInitialized = true;
            }
            return;
        }

        float currentHealth = _breakableObject.Health;
        bool isDestroyed = _breakableObject.IsDestroyed;

        // Si fue destruido en este frame, emitimos partículas de destrucción
        if (isDestroyed && !_wasDestroyedLastFrame)
        {
            ParticleEffectPlayer.PlayInPlace(_destroyParticles, transform.position);
        }
        // Si no está destruido pero la vida bajó, emitimos partículas de daño
        else if (!isDestroyed && currentHealth < _lastObservedHealth - _healthEpsilon)
        {
            ParticleEffectPlayer.PlayInPlace(_damageParticles, transform.position);
        }

        _lastObservedHealth = currentHealth;
        _wasDestroyedLastFrame = isDestroyed;
    }
}
