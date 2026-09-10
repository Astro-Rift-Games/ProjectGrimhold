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
    [SerializeField, Min(0.1f)]
    [Tooltip("Intervalo en segundos entre cada sonido de paso mientras el jugador se desplaza.")]
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
                if (_audioConfig.TryGetClip("Death", out var clip))
                {
                    AudioManager.Instance.PlaySfx(clip, transform.position);
                }
            }
            return;
        }

        // 2. Verificación de recepción de daño (TakeDamage)
        float currentHealth = _characterBase.Health;
        if (currentHealth < _lastObservedHealth - 0.001f) // Health epsilon
        {
            if (_audioConfig.TryGetClip("TakeDamage", out var clip))
            {
                AudioManager.Instance.PlaySfx(clip, transform.position);
            }
        }
        _lastObservedHealth = currentHealth;

        // 3. Verificación de locomoción (Movement)
        if (_movementState != null && _movementState.IsMoving)
        {
            _stepTimer -= Time.deltaTime;
            if (_stepTimer <= 0f)
            {
                _stepTimer = _stepInterval;
                if (_audioConfig.TryGetClip("Movement", out var stepClip))
                {
                    AudioManager.Instance.PlaySfx(stepClip, transform.position);
                }
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
