using System.Collections;
using UnityEngine;

/// <summary>
/// Observa el estado de salud, combate y locomoción de un enemigo para reproducir audios de
/// ataque (Attack/Shoot), recarga (Reload), daño (TakeDamage), muerte (Death) y pasos (Movement),
/// sin interferir en la simulación.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAudioPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private CharacterBase _characterBase;

    [SerializeField]
    private MonoBehaviour _combatControllerSource;

    [Header("Configuration")]
    [SerializeField]
    private EnemyAudioConfig _audioConfig;

    [Header("Ranged Configuration")]
    [SerializeField, Min(0f)]
    [Tooltip("Retardo en segundos antes de disparar el sonido de Reload tras un disparo a distancia.")]
    private float _reloadDelaySeconds = 0.25f;

    [Header("Movement Configuration")]
    [SerializeField]
    [Tooltip("Habilita la reproducción de pasos basada en temporizador en LateUpdate. Desmarcar si se usan AnimationEvents para evitar sonidos duplicados.")]
    private bool _enableTimerFootsteps = false;

    [SerializeField, Min(0.1f)]
    [Tooltip("Intervalo en segundos entre cada sonido de paso mientras el enemigo se desplaza (solo si _enableTimerFootsteps está activo).")]
    private float _stepInterval = 0.35f;

    private ICombatController _combatController;
    private IMovementState _movementState;
    private float _lastObservedHealth;
    private bool _isInitialized;
    private bool _isDead;
    private bool _isSubscribed;
    private float _stepTimer;
    private Coroutine _reloadCoroutine;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        InitializeHealthTracking();
        _stepTimer = 0f;
    }

    private void OnDisable()
    {
        Unsubscribe();
        _isInitialized = false;
        _isDead = false;
        if (_reloadCoroutine != null)
        {
            StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = null;
        }
    }

    /// <summary>
    /// Reproduce cualquier sonido configurado en el EnemyAudioConfig mediante su clave (ej: "Step", "Attack", "TakeDamage").
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

        if (_combatControllerSource != null)
        {
            _combatController = _combatControllerSource as ICombatController;
        }

        if (_combatController == null)
        {
            _combatController = GetComponentInParent<ICombatController>();
        }

        if (_movementState == null)
        {
            _movementState = GetComponentInParent<IMovementState>();
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed) return;

        if (_combatController != null)
        {
            _combatController.AttackPerformed += OnAttackPerformed;
            _isSubscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed) return;

        if (_combatController != null)
        {
            _combatController.AttackPerformed -= OnAttackPerformed;
            _isSubscribed = false;
        }
    }

    private void OnAttackPerformed(AttackPerformedEvent attackEvent)
    {
        if (_audioConfig == null || AudioManager.Instance == null) return;

        if (attackEvent.AttackType == AttackType.Melee)
        {
            if (_audioConfig.TryGetClip("Attack", out var attackClip))
            {
                AudioManager.Instance.PlaySfx(attackClip, transform.position);
            }
        }
        else if (attackEvent.AttackType == AttackType.Ranged)
        {
            if (_audioConfig.TryGetClip("Shoot", out var shootClip))
            {
                AudioManager.Instance.PlaySfx(shootClip, transform.position);
            }

            if (_audioConfig.TryGetClip("Reload", out var reloadClip))
            {
                if (_reloadCoroutine != null)
                {
                    StopCoroutine(_reloadCoroutine);
                }
                _reloadCoroutine = StartCoroutine(PlayDelayedReload(reloadClip, _reloadDelaySeconds));
            }
        }
    }

    private IEnumerator PlayDelayedReload(CustomClip reloadClip, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(reloadClip, transform.position);
        }

        _reloadCoroutine = null;
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

        // 2. Verificación de daño
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
                PlayAudio("Step");
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
