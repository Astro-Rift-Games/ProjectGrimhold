using UnityEngine;

/// <summary>
/// Observa el estado del personaje jugador (PlayerCharacter) para reproducir efectos de sonido
/// de daño (TakeDamage), muerte (Death) y locomoción (Movement) sin acoplarse a la simulación ni a la red.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAudioPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private CharacterBase _characterBase;

    [SerializeField]
    private MonoBehaviour _movementStateSource;

    [Header("Configuration")]
    [SerializeField]
    private PlayerAudioConfig _audioConfig;

    [Header("Movement Configuration")]
    [SerializeField]
    [Tooltip("Habilita la reproducción de pasos basada en temporizador en LateUpdate. Desmarcar si se usan AnimationEvents para evitar sonidos duplicados.")]
    private bool _enableTimerFootsteps = false;

    [SerializeField, Min(0.1f)]
    [Tooltip("Intervalo en segundos entre cada sonido de paso mientras el jugador se desplaza (solo si _enableTimerFootsteps está activo).")]
    private float _stepInterval = 0.35f;

    private IMovementState _movementState;
    private float _lastObservedHealth;
    private bool _isInitialized;
    private bool _isDead;
    private float _stepTimer;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        InitializeHealthTracking();
        _stepTimer = 0f;
    }

    private void OnDisable()
    {
        _isInitialized = false;
        _isDead = false;
    }

    /// <summary>
    /// Reproduce cualquier sonido configurado en el PlayerAudioConfig mediante su clave (ej: "Movement", "TakeDamage").
    /// Útil para llamadas directas desde listeners de eventos de animación (AnimationEvents).
    /// </summary>
    public void PlayAudio(string soundKey)
    {
        if (AudioManager.Instance == null || _audioConfig == null || string.IsNullOrEmpty(soundKey))
        {
            return;
        }

        if (_audioConfig.TryGetClip(soundKey, out CustomClip clip))
        {
            AudioManager.Instance.PlaySfx(clip, transform.position);
        }
    }

    private void CacheDependencies()
    {
        if (_characterBase == null)
        {
            _characterBase = GetComponentInParent<CharacterBase>();
        }

        if (_movementStateSource != null)
        {
            _movementState = _movementStateSource as IMovementState;
        }

        if (_movementState == null)
        {
            _movementState = GetComponentInParent<IMovementState>();
        }
    }

    private void LateUpdate()
    {
        if (_characterBase == null || _audioConfig == null || AudioManager.Instance == null) return;

        if (!_isInitialized)
        {
            if (_characterBase.Object != null && _characterBase.Object.IsValid)
            {
                _lastObservedHealth = _characterBase.Health;
                _isInitialized = true;
                _isDead = !_characterBase.IsAlive;
            }
            return;
        }

        // 1. Verificación de muerte
        if (!_characterBase.IsAlive)
        {
            if (!_isDead)
            {
                _isDead = true;
                PlayAudio("Death");
            }
            return;
        }

        // 2. Verificación de recepción de daño (TakeDamage)
        float currentHealth = _characterBase.Health;
        if (currentHealth < _lastObservedHealth - 0.001f) // Health epsilon
        {
            PlayAudio("TakeDamage");
        }
        _lastObservedHealth = currentHealth;

        // 3. Verificación de locomoción por temporizador (opcional si se usan AnimationEvents)
        if (_enableTimerFootsteps && _movementState != null && _movementState.IsMoving)
        {
            _stepTimer -= Time.deltaTime;
            if (_stepTimer <= 0f)
            {
                _stepTimer = _stepInterval;
                PlayAudio("Movement");
            }
        }
        else
        {
            _stepTimer = 0f;
        }
    }

    private void InitializeHealthTracking()
    {
        if (_characterBase != null && _characterBase.Object != null && _characterBase.Object.IsValid)
        {
            _lastObservedHealth = _characterBase.Health;
            _isInitialized = true;
            _isDead = !_characterBase.IsAlive;
        }
        else
        {
            _isInitialized = false;
        }
    }
}
